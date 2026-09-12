using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Win32;
using Windows.Win32.Graphics.Dxgi;
using Windows.Win32.Graphics.Direct3D11;
using Windows.Win32.System.Power;
using Windows.Win32.UI.WindowsAndMessaging;
using System.Runtime.InteropServices;
using LenovoLegionToolkit.Lib.Features.Hybrid;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.System;

public sealed class DgpuAwakeManager : IAsyncDisposable, IDisposable
{
    private readonly ApplicationSettings _settings;
    private readonly PowerStateListener _powerStateListener;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private ID3D11Device? _d3dDevice;
    private ID3D11DeviceContext? _d3dContext;
    private bool _isDisposed;
    private bool _isActive;
    private int _isPulsing;
    private CancellationTokenSource? _pulseCts;
    private Task? _initializationTask;
    private Task? _pulseTask;
    private DateTime _lastAdapterChangeHandled = DateTime.MinValue;
    private static readonly TimeSpan AdapterChangeDebounce = TimeSpan.FromMilliseconds(500);

    public void CancelPulse()
    {
        try
        {
            _pulseCts?.Cancel();
        }
        catch { }
    }

    public DgpuAwakeManager(ApplicationSettings settings, PowerStateListener powerStateListener)
    {
        _settings = settings;
        _powerStateListener = powerStateListener;

        _powerStateListener.Changed += PowerStateListener_Changed;

        _initializationTask = Task.Run(async () =>
        {
            try
            {
                await UpdateStateAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Instance.Trace($"Failed to initialize dGPU awake manager state.", ex);
            }
        });
    }

    private async void PowerStateListener_Changed(object? sender, PowerStateListener.ChangedEventArgs e)
    {
        // async void event handler: any unhandled exception propagates to the
        // SynchronizationContext and can crash the process. Wrap defensively.
        try
        {
            if (_isDisposed)
                return;

            if (e.PowerStateEvent == PowerStateEvent.Suspend)
            {
                Log.Instance.Trace($"System suspending: tearing down dGPU awake manager.");
                CancelPulse();
                await StopInternalAsync().ConfigureAwait(false);
            }
            else if (e.PowerStateEvent == PowerStateEvent.Resume)
            {
                Log.Instance.Trace($"System resumed: restoring dGPU awake manager.");
                await UpdateStateAsync().ConfigureAwait(false);
            }
            else if (e.PowerAdapterStateChanged)
            {
                // Debounce transient ACLineStatus spikes (e.g. 255) that fire rapid AC change events.
                var now = DateTime.UtcNow;
                if (now - _lastAdapterChangeHandled < AdapterChangeDebounce)
                {
                    Log.Instance.Trace($"Power adapter state change debounced (transient spike suppressed).");
                    return;
                }
                _lastAdapterChangeHandled = now;

                Log.Instance.Trace($"Power adapter state changed: updating dGPU awake manager.");
                var acStatus = await Power.IsPowerAdapterConnectedAsync().ConfigureAwait(false);
                if (acStatus != PowerAdapterStatus.Connected)
                {
                    CancelPulse();
                }
                await UpdateStateAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            // Most likely an ObjectDisposedException if the manager is being torn down
            // concurrently (race between this handler and DisposeAsync/Dispose). Log and
            // swallow: the suspend/resume/AC event is transient and will be re-evaluated
            // on the next state change or on the next UpdateStateAsync call.
            Log.Instance.Trace($"dGPU awake manager power-state handler failed.", ex);
        }
    }

    public async Task UpdateStateAsync()
    {
        if (_isDisposed) return;

        var keepAwake = _settings.Store.KeepDgpuAwake;

        if (keepAwake)
        {
            var hybridModeFeature = IoCContainer.Resolve<HybridModeFeature>();
            var state = await hybridModeFeature.GetStateAsync().ConfigureAwait(false);

            if (state is HybridModeState.OnIGPUOnly or HybridModeState.UMA)
            {
                keepAwake = false;
            }
            else if (state == HybridModeState.OnAuto)
            {
                var acStatus = await Power.IsPowerAdapterConnectedAsync().ConfigureAwait(false);
                if (acStatus != PowerAdapterStatus.Connected)
                {
                    keepAwake = false;
                }
            }
        }

        if (keepAwake)
        {
            await StartInternalAsync().ConfigureAwait(false);
        }
        else
        {
            await StopInternalAsync().ConfigureAwait(false);
        }
    }

    public async Task PulseDgpuAsync(TimeSpan duration)
    {
        if (_isDisposed) return;
        if (_isActive) return;
        if (Interlocked.CompareExchange(ref _isPulsing, 1, 0) != 0) return;

        var cts = new CancellationTokenSource();
        _pulseCts = cts;

        try
        {
            var pulseTask = Task.Run(async () =>
            {
                Log.Instance.Trace($"Pulsing dGPU awake for {duration.TotalSeconds}s...");
                ID3D11Device? device = null;
                ID3D11DeviceContext? context = null;
                try
                {
                    if (cts.IsCancellationRequested) return;

                    PInvoke.CreateDXGIFactory2(0, typeof(IDXGIFactory6).GUID, out object factoryObj);
                    if (factoryObj is not IDXGIFactory6 factory)
                    {
                        Log.Instance.Trace($"Failed to create IDXGIFactory6.");
                        return;
                    }

                    IDXGIAdapter1? dgpuAdapter = null;
                    try
                    {
                        factory.EnumAdapterByGpuPreference(0, DXGI_GPU_PREFERENCE.DXGI_GPU_PREFERENCE_HIGH_PERFORMANCE, typeof(IDXGIAdapter1).GUID, out object adapterObj);
                        dgpuAdapter = (IDXGIAdapter1)adapterObj;
                    }
                    catch { }

                    if (dgpuAdapter == null)
                    {
                        Marshal.ReleaseComObject(factory);
                        return;
                    }

                    try
                    {
                        unsafe
                        {
                            PInvoke.D3D11CreateDevice(
                                dgpuAdapter,
                                Windows.Win32.Graphics.Direct3D.D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_UNKNOWN,
                                Windows.Win32.Foundation.HMODULE.Null,
                                Windows.Win32.Graphics.Direct3D11.D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                                null,
                                0,
                                7u,
                                out device,
                                null,
                                out context);
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(dgpuAdapter);
                        Marshal.ReleaseComObject(factory);
                    }

                    await Task.Delay(duration, cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    Log.Instance.Trace($"dGPU pulse awake cancelled.");
                }
                catch (Exception ex)
                {
                    Log.Instance.Trace($"Failed during dGPU pulse awake.", ex);
                }
                finally
                {
                    if (context != null)
                    {
                        Marshal.ReleaseComObject(context);
                    }
                    if (device != null)
                    {
                        Marshal.ReleaseComObject(device);
                    }
                    Log.Instance.Trace($"dGPU pulse awake completed.");
                }
            }, cts.Token);
            _pulseTask = pulseTask;
            await pulseTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        finally
        {
            _pulseCts = null;
            cts.Dispose();
            Interlocked.Exchange(ref _isPulsing, 0);
        }
    }

    private async Task StartInternalAsync()
    {
        CancelPulse();
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            // Re-check after acquiring the lock: UpdateStateAsync's initial _isDisposed
            // check is subject to a TOCTOU race with DisposeAsync/Dispose, which could
            // dispose the lock (and dGPU state) between the check and this point.
            if (_isDisposed) return;
            if (_isActive) return;

            Log.Instance.Trace($"Attempting to keep dGPU awake...");
            CreateD3D11Device();
            _isActive = true;
            Log.Instance.Trace($"dGPU awake manager started.");

            if (_settings.Store.LockedPStateId >= 0)
            {
                _ = IoCContainer.Resolve<Controllers.GPUController>().ApplyPStateAsync(_settings.Store.LockedPStateId);
            }
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"Failed to initialize D3D11 dummy device for dGPU awake: {ex.Message}");
            DisposeD3D11Device();
            _isActive = false;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task StopInternalAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            // Re-check after acquiring the lock: same TOCTOU window as StartInternalAsync.
            if (_isDisposed) return;
            if (!_isActive) return;

            DisposeD3D11Device();
            _isActive = false;
            Log.Instance.Trace($"dGPU awake manager stopped.");
        }
        finally
        {
            _lock.Release();
        }
    }

    private unsafe void CreateD3D11Device()
    {
        PInvoke.CreateDXGIFactory2(0, typeof(IDXGIFactory6).GUID, out object factoryObj);
        if (factoryObj is not IDXGIFactory6 factory)
        {
            Log.Instance.Trace($"Failed to create IDXGIFactory6.");
            throw new Exception("Failed to create IDXGIFactory6.");
        }

        IDXGIAdapter1? dgpuAdapter = null;
        try
        {
            factory.EnumAdapterByGpuPreference(0, DXGI_GPU_PREFERENCE.DXGI_GPU_PREFERENCE_HIGH_PERFORMANCE, typeof(IDXGIAdapter1).GUID, out object adapterObj);
            dgpuAdapter = (IDXGIAdapter1)adapterObj;
        }
        catch
        {
        }

        if (dgpuAdapter == null)
        {
            Marshal.ReleaseComObject(factory);
            Log.Instance.Trace($"Failed to find any HighPerformance adapter.");
            throw new Exception("No suitable hardware adapter found.");
        }

        try
        {
            PInvoke.D3D11CreateDevice(
                dgpuAdapter, 
                Windows.Win32.Graphics.Direct3D.D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_UNKNOWN,
                Windows.Win32.Foundation.HMODULE.Null,
                Windows.Win32.Graphics.Direct3D11.D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                null, 
                0,    
                7u,   
                out _d3dDevice,
                null,
                out _d3dContext);
        }
        finally
        {
            Marshal.ReleaseComObject(dgpuAdapter);
            Marshal.ReleaseComObject(factory);
        }
    }

    private void DisposeD3D11Device()
    {
        if (_d3dContext != null)
        {
            Marshal.ReleaseComObject(_d3dContext);
            _d3dContext = null;
        }

        if (_d3dDevice != null)
        {
            Marshal.ReleaseComObject(_d3dDevice);
            _d3dDevice = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        CancelPulse();
        _powerStateListener.Changed -= PowerStateListener_Changed;

        await ObserveTaskAsync(_initializationTask).ConfigureAwait(false);
        await ObserveTaskAsync(_pulseTask).ConfigureAwait(false);

        await StopInternalAsync().ConfigureAwait(false);
        _lock.Dispose();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        CancelPulse();
        _powerStateListener.Changed -= PowerStateListener_Changed;

        ObserveTaskAsync(_initializationTask).GetAwaiter().GetResult();
        ObserveTaskAsync(_pulseTask).GetAwaiter().GetResult();
        StopInternalAsync().GetAwaiter().GetResult();
        _lock.Dispose();
    }

    private static async Task ObserveTaskAsync(Task? task)
    {
        if (task is null) return;
        try { await task.ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Log.Instance.Trace($"dGPU awake lifecycle task failed.", ex); }
    }
}
