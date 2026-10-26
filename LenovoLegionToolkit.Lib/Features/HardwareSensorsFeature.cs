using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers.Sensors;
using LenovoLegionToolkit.Lib.Messaging;
using LenovoLegionToolkit.Lib.Messaging.Messages;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Features;

public class HardwareSensorsFeature(ApplicationSettings settings, OsdSettings osdSettings, SensorsGroupController sensorsGroupController) : IFeature<HardwareSensorsState>
{
    public Task<bool> IsSupportedAsync() => Task.FromResult(PawnIOHelper.GetPawnIOState() == PawnIOState.Installed);

    public Task<HardwareSensorsState[]> GetAllStatesAsync() => Task.FromResult(Enum.GetValues<HardwareSensorsState>());

    public Task<HardwareSensorsState> GetStateAsync()
    {
        var state = settings.Store.EnableHardwareSensors
            ? HardwareSensorsState.On
            : HardwareSensorsState.Off;
        return Task.FromResult(state);
    }

    public async Task SetStateAsync(HardwareSensorsState state)
    {
        var enabled = state == HardwareSensorsState.On;
        if (enabled && !sensorsGroupController.IsLibreHardwareMonitorInitialized())
        {
            var initialState = await sensorsGroupController.IsSupportedAsync().ConfigureAwait(false);
            var initialized = initialState is LibreHardwareMonitorInitialState.Initialized or LibreHardwareMonitorInitialState.Success;
            // Preserve an existing opt-in across transient reinitialization failures;
            // only reject a fresh enable request that could not initialize successfully.
            enabled = initialized || settings.Store.EnableHardwareSensors;
        }

        settings.Store.EnableHardwareSensors = enabled;
        settings.SynchronizeStore();

        if (!enabled)
        {
            MessagingCenter.Publish(new OsdChangedMessage(ToggleState.Off, setPreference: false));
        }
        else if (osdSettings.Store.ShowOsd)
        {
            MessagingCenter.Publish(new OsdChangedMessage(ToggleState.On));
        }

        MessagingCenter.Publish(new SensorDashboardSwappedMessage());
    }
}
