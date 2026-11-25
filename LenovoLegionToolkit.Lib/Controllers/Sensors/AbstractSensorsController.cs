using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Features.Hybrid;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;
using NvAPIWrapper.Native;
using NvAPIWrapper.Native.GPU;
using Windows.Win32;
using Windows.Win32.System.Power;

namespace LenovoLegionToolkit.Lib.Controllers.Sensors;

public abstract class AbstractSensorsController(GPUController gpuController) : ISensorsController
{
    protected readonly struct GPUInfo(
        int utilization,
        int coreClock,
        int maxCoreClock,
        int memoryClock,
        int maxMemoryClock,
        int temperature,
        int maxTemperature)
    {
        public static readonly GPUInfo Empty = new(-1, -1, -1, -1, -1, -1, -1);

        public int Utilization { get; } = utilization;
        public int CoreClock { get; } = coreClock;
        public int MaxCoreClock { get; } = maxCoreClock;
        public int MemoryClock { get; } = memoryClock;
        public int MaxMemoryClock { get; } = maxMemoryClock;
        public int Temperature { get; } = temperature;
        public int MaxTemperature { get; } = maxTemperature;
    }

    #region P / Invoke for GetSystemTimes
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;
    }

    private static ulong ToUInt64(FILETIME ft)
    {
        return ((ulong)ft.dwHighDateTime << 32) | ft.dwLowDateTime;
    }
    #endregion

    private readonly SafePerformanceCounter _percentProcessorPerformanceCounter = new("Processor Information", "% Processor Performance", "_Total");
    private readonly Lock _cpuCalcLock = new();

    private ulong _prevIdleTime;
    private ulong _prevKernelTime;
    private ulong _prevUserTime;

    private int _cachedCpuUtilization;
    private long _lastCpuCalculationTick;
    private const long MinUpdateIntervalTicks = 500 * 10000; // 500ms (1ms = 10,000 ticks)

    protected int? _cpuBaseClockCache;
    protected int? _cpuMaxCoreClockCache;
    protected int? _cpuMaxFanSpeedCache;
    protected int? _gpuMaxFanSpeedCache;
    protected readonly record struct SystemFanDefinition(int SensorId, int FanId, CapabilityID SpeedCapability, int DisplayNumber);

    private readonly ConcurrentDictionary<(int SensorId, int FanId), int> _systemMaxFanSpeeds = new();
    protected virtual IReadOnlyList<SystemFanDefinition> SystemFanDefinitions => [];
    public IReadOnlyList<FanData> SystemFans { get; private set; } = [];

    public abstract Task<bool> IsSupportedAsync();

    public async Task PrepareAsync()
    {
        _percentProcessorPerformanceCounter.Reset();
        _percentProcessorPerformanceCounter.NextValue();

        lock (_cpuCalcLock)
        {
            if (GetSystemTimes(out var idle, out var kernel, out var user))
            {
                _prevIdleTime = ToUInt64(idle);
                _prevKernelTime = ToUInt64(kernel);
                _prevUserTime = ToUInt64(user);
            }

            _lastCpuCalculationTick = DateTime.UtcNow.Ticks;
        }

        await Task.Delay(500).ConfigureAwait(false);

        _percentProcessorPerformanceCounter.NextValue();

        GetCpuUtilization(100);
    }


    public virtual async Task<SensorsData> GetDataAsync()
    {
        const int genericMaxUtilization = 100;
        const int genericMaxTemperature = 100;

        var cpuUtilization = GetCpuUtilization(genericMaxUtilization);

        var cpuMaxCoreClock = await GetCachedSensorValueAsync(GetCpuMaxCoreClockAsync,
            _cpuMaxCoreClockCache, value => _cpuMaxCoreClockCache = value).ConfigureAwait(false);
        var cpuCoreClock = GetCpuCoreClock();
        var cpuCurrentTemperature = await QuerySensorValueAsync(GetCpuCurrentTemperatureAsync).ConfigureAwait(false);
        var cpuCurrentFanSpeed = await QuerySensorValueAsync(GetCpuCurrentFanSpeedAsync).ConfigureAwait(false);
        var cpuMaxFanSpeed = await GetCachedSensorValueAsync(GetCpuMaxFanSpeedAsync,
            _cpuMaxFanSpeedCache, value => _cpuMaxFanSpeedCache = value).ConfigureAwait(false);

        var gpuInfo = await QueryGpuInfoAsync().ConfigureAwait(false);
        var gpuCurrentTemperature = gpuInfo.Temperature >= 0
            ? gpuInfo.Temperature
            : await QuerySensorValueAsync(GetGpuCurrentTemperatureAsync).ConfigureAwait(false);
        var gpuMaxTemperature = gpuInfo.MaxTemperature >= 0 ? gpuInfo.MaxTemperature : genericMaxTemperature;
        var gpuCurrentFanSpeed = await QuerySensorValueAsync(GetGpuCurrentFanSpeedAsync).ConfigureAwait(false);
        var gpuMaxFanSpeed = await GetCachedSensorValueAsync(GetGpuMaxFanSpeedAsync,
            _gpuMaxFanSpeedCache, value => _gpuMaxFanSpeedCache = value).ConfigureAwait(false);

        var motherboardTemperature = await QuerySensorValueAsync(GetMotherboardTemperatureAsync).ConfigureAwait(false);
        var systemFans = await GetSystemFansAsync().ConfigureAwait(false);

        var cpu = new SensorData(cpuUtilization,
            genericMaxUtilization,
            cpuCoreClock,
            cpuMaxCoreClock,
            -1,
            -1,
            cpuCurrentTemperature,
            genericMaxTemperature,
            cpuCurrentFanSpeed,
            cpuMaxFanSpeed);

        var gpu = new SensorData(gpuInfo.Utilization,
            genericMaxUtilization,
            gpuInfo.CoreClock,
            gpuInfo.MaxCoreClock,
            gpuInfo.MemoryClock,
            gpuInfo.MaxMemoryClock,
            gpuCurrentTemperature,
            gpuMaxTemperature,
            gpuCurrentFanSpeed,
            gpuMaxFanSpeed);

        return new SensorsData(cpu, gpu, motherboardTemperature, systemFans);
    }

    private async Task<GPUInfo> QueryGpuInfoAsync()
    {
        try
        {
            return await GetGPUInfoAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // GPU power-state/NVAPI failures must not discard valid CPU and PCH data.
            Log.Instance.Trace($"GPU sensor query failed.", ex);
            return GPUInfo.Empty;
        }
    }

    private static async Task<int> GetCachedSensorValueAsync(Func<Task<int>> query, int? cachedValue, Action<int> updateCache)
    {
        if (cachedValue is > 0)
            return cachedValue.Value;

        var value = await QuerySensorValueAsync(query).ConfigureAwait(false);
        if (value > 0)
            updateCache(value);

        return value;
    }

    private static async Task<int> QuerySensorValueAsync(Func<Task<int>> query)
    {
        try
        {
            return await query().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ManagementException or COMException or InvalidOperationException)
        {
            // A single malformed/transient reading invalidates only that metric. Do not
            // discard otherwise healthy CPU, GPU and PCH readings from the same refresh.
            Log.Instance.Trace($"Sensor query failed. [query={query.Method.Name}]", ex);
            return -1;
        }
    }

    public virtual async Task<FanSpeedTable> GetFanSpeedsAsync()
    {
        var cpuFanSpeed = await QuerySensorValueAsync(GetCpuCurrentFanSpeedAsync).ConfigureAwait(false);
        var gpuFanSpeed = await QuerySensorValueAsync(GetGpuCurrentFanSpeedAsync).ConfigureAwait(false);
        var systemFans = await GetSystemFansAsync().ConfigureAwait(false);
        return new FanSpeedTable(cpuFanSpeed, gpuFanSpeed, systemFans);
    }

    protected async Task<IReadOnlyList<FanData>> GetSystemFansAsync()
    {
        var definitions = SystemFanDefinitions;
        var fans = new FanData[definitions.Count];
        for (var i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];
            var speed = -1;
            try
            {
                speed = await GetSystemFanSpeedAsync(definition).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Instance.Trace($"Error reading system fan speed. [fanId={definition.FanId}]", ex);
            }

            var key = (definition.SensorId, definition.FanId);
            if (!_systemMaxFanSpeeds.TryGetValue(key, out var maxSpeed))
            {
                maxSpeed = -1;
                try
                {
                    maxSpeed = await GetSystemFanMaxSpeedAsync(definition).ConfigureAwait(false);
                    if (maxSpeed > 0)
                        _systemMaxFanSpeeds.TryAdd(key, maxSpeed);
                }
                catch (Exception ex)
                {
                    Log.Instance.Trace($"Error reading system fan maximum speed. [fanId={definition.FanId}]", ex);
                }
            }

            fans[i] = new FanData(definition.FanId, definition.DisplayNumber, speed, maxSpeed);
        }

        var snapshot = Array.AsReadOnly(fans);
        SystemFans = snapshot;
        return snapshot;
    }

    protected virtual Task<int> GetSystemFanSpeedAsync(SystemFanDefinition definition) => Task.FromResult(-1);

    protected virtual Task<int> GetSystemFanMaxSpeedAsync(SystemFanDefinition definition) => Task.FromResult(-1);

    protected abstract Task<int> GetCpuCurrentTemperatureAsync();

    protected abstract Task<int> GetGpuCurrentTemperatureAsync();

    protected virtual Task<int> GetMotherboardTemperatureAsync() => Task.FromResult(-1);

    protected abstract Task<int> GetCpuCurrentFanSpeedAsync();

    protected abstract Task<int> GetGpuCurrentFanSpeedAsync();

    protected abstract Task<int> GetCpuMaxFanSpeedAsync();

    protected abstract Task<int> GetGpuMaxFanSpeedAsync();

    protected static unsafe int GetCpuBaseClock()
    {
        var ptr = IntPtr.Zero;
        try
        {
            PInvoke.GetSystemInfo(out var systemInfo);

            var numberOfProcessors = Math.Min(32, (int)systemInfo.dwNumberOfProcessors);
            var infoSize = Marshal.SizeOf<PROCESSOR_POWER_INFORMATION>();
            var infosSize = numberOfProcessors * infoSize;

            ptr = Marshal.AllocHGlobal(infosSize);

            var result = PInvoke.CallNtPowerInformation(POWER_INFORMATION_LEVEL.ProcessorInformation,
                null,
                0,
                ptr.ToPointer(),
                (uint)infosSize);
            if (result != 0)
                return 0;

            var infos = new PROCESSOR_POWER_INFORMATION[numberOfProcessors];

            for (var i = 0; i < infos.Length; i++)
                infos[i] = Marshal.PtrToStructure<PROCESSOR_POWER_INFORMATION>(IntPtr.Add(ptr, i * infoSize));

            return (int)infos.Select(p => p.MaxMhz).Max();
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    protected virtual async Task<int> GetCpuMaxCoreClockAsync()
    {
        try
        {
            return await WMI.LenovoGameZoneData.GetCPUFrequencyAsync().ConfigureAwait(false);
        }
        catch (ManagementException)
        {
            return GetCpuBaseClock();
        }
    }

    protected virtual async Task<GPUInfo> GetGPUInfoAsync()
    {
        var hybridModeFeature = IoCContainer.Resolve<HybridModeFeature>();
        if (hybridModeFeature.ShouldKeepDGPUAsleep())
        {
            Log.Instance.Trace($"dGPU eject is being ensured — skipping NVAPI query.");
            return GPUInfo.Empty;
        }

        if (gpuController.IsSupported())
            await gpuController.StartAsync().ConfigureAwait(false);

        if (await gpuController.GetLastKnownStateAsync().ConfigureAwait(false) is GPUState.PoweredOff)
            return GPUInfo.Empty;

        try
        {
            NVAPI.Initialize();

            var gpu = NVAPI.GetGPU();
            if (gpu is null)
                return GPUInfo.Empty;

            var utilization = Math.Min(100, Math.Max(gpu.UsageInformation.GPU.Percentage, gpu.UsageInformation.VideoEngine.Percentage));

            var currentCoreClock = (int)gpu.CurrentClockFrequencies.GraphicsClock.Frequency / 1000;
            var currentMemoryClock = (int)gpu.CurrentClockFrequencies.MemoryClock.Frequency / 1000;

            var maxCoreClock = (int)gpu.BoostClockFrequencies.GraphicsClock.Frequency / 1000;
            var maxMemoryClock = (int)gpu.BoostClockFrequencies.MemoryClock.Frequency / 1000;

            switch (gpu.MemoryInformation.RAMType)
            {
                case GPUMemoryType.GDDR5:
                case GPUMemoryType.GDDR5X:
                    currentMemoryClock /= 2;
                    maxMemoryClock /= 2;
                    break;
                case GPUMemoryType.GDDR6:
                case GPUMemoryType.GDDR6X:
                    currentMemoryClock /= 4;
                    maxMemoryClock /= 4;
                    break;
                case GPUMemoryType.GDDR7:
                    currentMemoryClock /= 8;
                    maxMemoryClock /= 8;
                    break;
                default:
                    break;
            }

            var states = GPUApi.GetPerformanceStates20(gpu.Handle);
            var maxCoreClockOffset = states.Clocks[PerformanceStateId.P0_3DPerformance][0].FrequencyDeltaInkHz.DeltaValue / 1000;
            var maxMemoryClockOffset = states.Clocks[PerformanceStateId.P0_3DPerformance][1].FrequencyDeltaInkHz.DeltaValue / 1000;

            var temperatureSensor = gpu.ThermalInformation.ThermalSensors.FirstOrDefault();
            var currentTemperature = temperatureSensor?.CurrentTemperature ?? -1;
            var maxTemperature = temperatureSensor?.DefaultMaximumTemperature ?? -1;

            return new(utilization,
                currentCoreClock,
                maxCoreClock + maxCoreClockOffset,
                currentMemoryClock,
                maxMemoryClock + maxMemoryClockOffset,
                currentTemperature,
                maxTemperature);
        }
        catch
        {
            return GPUInfo.Empty;
        }
    }

    protected int GetCpuUtilization(int maxUtilization)
    {
        lock (_cpuCalcLock)
        {
            long currentTick = DateTime.UtcNow.Ticks;

            if (currentTick - _lastCpuCalculationTick < MinUpdateIntervalTicks)
            {
                return _cachedCpuUtilization;
            }

            if (!GetSystemTimes(out var idleHandle, out var kernelHandle, out var userHandle))
            {
                return _cachedCpuUtilization;
            }

            ulong currentIdle = ToUInt64(idleHandle);
            ulong currentKernel = ToUInt64(kernelHandle);
            ulong currentUser = ToUInt64(userHandle);

            ulong idleDelta = currentIdle - _prevIdleTime;
            ulong kernelDelta = currentKernel - _prevKernelTime;
            ulong userDelta = currentUser - _prevUserTime;

            ulong totalSys = kernelDelta + userDelta;

            if (totalSys > 0)
            {
                _prevIdleTime = currentIdle;
                _prevKernelTime = currentKernel;
                _prevUserTime = currentUser;
                _lastCpuCalculationTick = currentTick;

                var usagePercent = (double)(totalSys - idleDelta) / totalSys * 100.0;
                var rounded = (int)Math.Round(usagePercent);

                _cachedCpuUtilization = Math.Min(Math.Max(rounded, 0), maxUtilization);
            }

            return _cachedCpuUtilization;
        }
    }

    protected int GetCpuCoreClock()
    {
        var baseClock = _cpuBaseClockCache ??= GetCpuBaseClock();
        var perfValue = _percentProcessorPerformanceCounter.NextValue();
        if (float.IsNaN(perfValue) || perfValue <= 0)
            return -1;

        var clock = (int)(baseClock * (perfValue / 100f));
        if (clock < 1)
            return -1;
        return clock;
    }
}
