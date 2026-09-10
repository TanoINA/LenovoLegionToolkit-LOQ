using System;
using System.Diagnostics;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Controllers.GodMode;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Messaging;
using LenovoLegionToolkit.Lib.Messaging.Messages;
using LenovoLegionToolkit.Lib.Overclocking.Amd;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Listeners;

public class PowerModeListener(
    GodModeController godModeController,
    WindowsPowerModeController windowsPowerModeController,
    WindowsPowerPlanController windowsPowerPlanController)
    : AbstractWMIListener<PowerModeListener.ChangedEventArgs, PowerModeState, int>(WMI.LenovoGameZoneSmartFanModeEvent.Listen), INotifyingListener<PowerModeListener.ChangedEventArgs, PowerModeState>
{
    public class ChangedEventArgs(PowerModeState state) : EventArgs
    {
        public PowerModeState State { get; } = state;
    }

    private readonly ThreadSafeCounter _suppressCounter = new();
    private readonly object _stateLock = new();
    private DateTime _lastProcessedTime = DateTime.MinValue;
    private PowerModeState? _lastProcessedValue;

    public void SuppressNext()
    {
        Log.Instance.Trace($"PowerModeListener: Suppressing next...");
        _suppressCounter.Increment();
    }

    public bool IsRecentlyProcessed(PowerModeState value, TimeSpan window)
    {
        lock (_stateLock)
        {
            return _lastProcessedValue == value && (DateTime.UtcNow - _lastProcessedTime) < window;
        }
    }

    protected override PowerModeState GetValue(int value)
    {
        var result = (PowerModeState)(value - 1);
        return result;
    }

    protected override ChangedEventArgs GetEventArgs(PowerModeState value) => new(value);

    protected override async Task OnChangedAsync(PowerModeState value)
    {
        if (!_suppressCounter.Decrement())
        {
            Log.Instance.Trace($"PowerModeListener: Suppressed WMI event for {value}.");
            return;
        }

        lock (_stateLock)
        {
            var now = DateTime.UtcNow;
            if (_lastProcessedValue == value && (now - _lastProcessedTime).TotalMilliseconds < 300)
            {
                Log.Instance.Trace($"PowerModeListener: Debounced duplicate event {value} within {(now - _lastProcessedTime).TotalMilliseconds:F0}ms.");
                return;
            }
            _lastProcessedValue = value;
            _lastProcessedTime = now;
        }

        PublishNotification(value);
        Log.Instance.Trace($"PowerModeListener.OnChangedAsync (WMI event path): value={value}");
        var sw = Stopwatch.StartNew();
        await ChangeDependenciesAsync(value).ConfigureAwait(false);
        Log.Instance.Trace($"ChangeDependenciesAsync completed [elapsed={sw.ElapsedMilliseconds}ms]");
    }

    public async Task NotifyAsync(PowerModeState value)
    {
        lock (_stateLock)
        {
            _lastProcessedValue = value;
            _lastProcessedTime = DateTime.UtcNow;
        }

        Log.Instance.Trace($"PowerModeListener.NotifyAsync (explicit path): value={value}");
        var sw = Stopwatch.StartNew();
        await ChangeDependenciesAsync(value).ConfigureAwait(false);
        Log.Instance.Trace($"ChangeDependenciesAsync completed [elapsed={sw.ElapsedMilliseconds}ms]");
        RaiseChanged(value);
    }

    protected override async Task<bool> CanStartAsync()
    {
        var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
        return Compatibility.IsLegion(mi.LegionSeries);
    }

    private readonly global::System.Threading.SemaphoreSlim _dependenciesLock = new(1, 1);

    private async Task ChangeDependenciesAsync(PowerModeState value)
    {
        await _dependenciesLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var sw = Stopwatch.StartNew();

            if (value is PowerModeState.GodMode)
            {
                Log.Instance.Trace($"Delaying GodMode apply...");
                await Task.Delay(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);

                Log.Instance.Trace($"Calling GodModeController.ApplyStateAsync...");
                var godSw = Stopwatch.StartNew();
                await godModeController.ApplyStateAsync().ConfigureAwait(false);
                Log.Instance.Trace($"ApplyStateAsync completed [elapsed={godSw.ElapsedMilliseconds}ms]");
            }
            else
            {
                Log.Instance.Trace($"Delaying restore defaults in other power mode...");
                await Task.Delay(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);

                Log.Instance.Trace($"Calling GodModeController.RestoreDefaultsInOtherPowerModeAsync({value})...");
                var restSw = Stopwatch.StartNew();
                await godModeController.RestoreDefaultsInOtherPowerModeAsync(value).ConfigureAwait(false);
                Log.Instance.Trace($"RestoreDefaultsInOtherPowerModeAsync completed [elapsed={restSw.ElapsedMilliseconds}ms]");
            }

            await windowsPowerModeController.SetPowerModeAsync(value).ConfigureAwait(false);
            await windowsPowerPlanController.SetPowerPlanAsync(value).ConfigureAwait(false);

            var gpuOverclockController = IoCContainer.Resolve<GPUOverclockController>();
            Log.Instance.Trace($"Checking GPUOverclock IsSupportedAsync...");
            if (await gpuOverclockController.IsSupportedAsync().ConfigureAwait(false))
            {
                Log.Instance.Trace($"GPU overclock supported, scheduling re-apply after 1s");
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                    await gpuOverclockController.EnsureOverclockIsAppliedAsync().ConfigureAwait(false);
                });
            }

            var amdOverclockingController = IoCContainer.Resolve<AmdOverclockingController>();
            if (amdOverclockingController.IsActive() && !amdOverclockingController.AllowInAllPowerModes)
            {
                Log.Instance.Trace($"Applying AMD OC default profile...");
                await amdOverclockingController.ApplyDefaultProfileAsync().ConfigureAwait(false);
            }

            Log.Instance.Trace($"ChangeDependenciesAsync total [elapsed={sw.ElapsedMilliseconds}ms]");
        }
        finally
        {
            _dependenciesLock.Release();
        }
    }

    private static void PublishNotification(PowerModeState value)
    {
        switch (value)
        {
            case PowerModeState.Quiet:
                MessagingCenter.Publish(new NotificationMessage(NotificationType.PowerModeQuiet, value.GetDisplayName()));
                break;
            case PowerModeState.Balance:
                MessagingCenter.Publish(new NotificationMessage(NotificationType.PowerModeBalance, value.GetDisplayName()));
                break;
            case PowerModeState.Performance:
                MessagingCenter.Publish(new NotificationMessage(NotificationType.PowerModePerformance, value.GetDisplayName()));
                break;
            case PowerModeState.Extreme:
                MessagingCenter.Publish(new NotificationMessage(NotificationType.PowerModeExtreme, value.GetDisplayName()));
                break;
            case PowerModeState.GodMode:
                MessagingCenter.Publish(new NotificationMessage(NotificationType.PowerModeGodMode, value.GetDisplayName()));
                break;
        }
    }
}
