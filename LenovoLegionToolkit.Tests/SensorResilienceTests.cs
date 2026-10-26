using System.Management;
using LenovoLegionToolkit.Lib.Controllers.Sensors;

namespace LenovoLegionToolkit.Tests;

[TestClass]
public class SensorResilienceTests
{
    [TestMethod]
    public async Task TransientTemperatureFailure_PreservesOtherSensorReadings()
    {
        var controller = new FakeSensorsController
        {
            CpuTemperature = () => Task.FromException<int>(new ManagementException("Transient WMI failure"))
        };

        var data = await controller.GetDataAsync();

        Assert.AreEqual(-1, data.CPU.Temperature);
        Assert.AreEqual(1234, data.CPU.FanSpeed);
        Assert.AreEqual(65, data.GPU.Temperature);
        Assert.AreEqual(2345, data.GPU.FanSpeed);
        Assert.AreEqual(45, data.PCH.Temperature);
    }

    [TestMethod]
    public async Task MaximumFanSpeed_TransientFailure_IsRetriedThenCached()
    {
        var calls = 0;
        var controller = new FakeSensorsController
        {
            CpuMaxFanSpeed = () => ++calls == 1
                ? Task.FromException<int>(new ManagementException("Transient WMI failure"))
                : Task.FromResult(5400)
        };

        var failed = await controller.GetDataAsync();
        var recovered = await controller.GetDataAsync();
        var cached = await controller.GetDataAsync();

        Assert.AreEqual(-1, failed.CPU.MaxFanSpeed);
        Assert.AreEqual(5400, recovered.CPU.MaxFanSpeed);
        Assert.AreEqual(5400, cached.CPU.MaxFanSpeed);
        Assert.AreEqual(2, calls);
    }

    private sealed class FakeSensorsController : AbstractSensorsController
    {
        public Func<Task<int>> CpuTemperature { get; set; } = () => Task.FromResult(70);
        public Func<Task<int>> GpuTemperature { get; set; } = () => Task.FromResult(65);
        public Func<Task<int>> PchTemperature { get; set; } = () => Task.FromResult(45);
        public Func<Task<int>> CpuMaxFanSpeed { get; set; } = () => Task.FromResult(6000);

        public FakeSensorsController() : base(null!) { }

        public override Task<bool> IsSupportedAsync() => Task.FromResult(true);

        protected override Task<GPUInfo> GetGPUInfoAsync() => Task.FromResult(GPUInfo.Empty);

        protected override Task<int> GetCpuMaxCoreClockAsync() => Task.FromResult(5000);

        protected override Task<int> GetCpuCurrentTemperatureAsync() => CpuTemperature();

        protected override Task<int> GetGpuCurrentTemperatureAsync() => GpuTemperature();

        protected override Task<int> GetPchCurrentTemperatureAsync() => PchTemperature();

        protected override Task<int> GetCpuCurrentFanSpeedAsync() => Task.FromResult(1234);

        protected override Task<int> GetGpuCurrentFanSpeedAsync() => Task.FromResult(2345);

        protected override Task<int> GetCpuMaxFanSpeedAsync() => CpuMaxFanSpeed();

        protected override Task<int> GetGpuMaxFanSpeedAsync() => Task.FromResult(6100);
    }
}
