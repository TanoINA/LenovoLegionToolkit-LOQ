using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;
using Windows.Win32;

namespace LenovoLegionToolkit.Lib.System;

public static class Power
{
    private static PowerAdapterStatus _lastReportedStatus = PowerAdapterStatus.Connected;

    public static async Task<PowerAdapterStatus> IsPowerAdapterConnectedAsync()
    {
        if (!PInvoke.GetSystemPowerStatus(out var sps))
        {
            return _lastReportedStatus;
        }

        // Win32 SYSTEM_POWER_STATUS: ACLineStatus: 0 = Offline, 1 = Online, 255 = Unknown.
        // During power mode switches or power plan reloads, ACLineStatus can momentarily return 255.
        // Retain the last known status to prevent false Disconnected triggers.
        if (sps.ACLineStatus == 255)
        {
            return _lastReportedStatus;
        }

        var adapterConnected = sps.ACLineStatus == 1;
        if (!adapterConnected)
        {
            _lastReportedStatus = PowerAdapterStatus.Disconnected;
            return PowerAdapterStatus.Disconnected;
        }

        var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
        var useWindowsPowerStatus = mi.LegionSeries >= LegionSeries.Legion_Legacy;
        // On unsupported models (LOQ, IdeaPad, etc.), low-wattage adapter detection is not supported in hardware.
        // Using transient battery discharge rate causes spurious ConnectedLowWattage flips during CPU/GPU power shifts.
        var chargingNormally = useWindowsPowerStatus ? true : await IsChargingNormallyLenovoAsync().ConfigureAwait(false) ?? true;

        var status = (adapterConnected, chargingNormally) switch
        {
            (true, false) => PowerAdapterStatus.ConnectedLowWattage,
            (true, _) => PowerAdapterStatus.Connected,
            _ => PowerAdapterStatus.Disconnected,
        };

        _lastReportedStatus = status;
        return status;
    }

    public static bool IsBatterySaverEnabled()
    {
        if (!PInvoke.GetSystemPowerStatus(out var systemPowerStatus))
        {
            return false;
        }

        return systemPowerStatus.SystemStatusFlag == 1;
    }

    public static async Task RestartAsync()
    {
        Log.Instance.Trace($"Restarting...");

        await CMD.RunAsync("shutdown", "/r /t 0").ConfigureAwait(false);
    }

    private static async Task<bool?> IsChargingNormallyLenovoAsync()
    {
        var acFitForOc = await IsAcFitForOcAsync().ConfigureAwait(false) ?? true;
        var chargingNormally = await IsChargingNormallyAsync().ConfigureAwait(false) ?? true;
        return acFitForOc && chargingNormally;
    }

    private static async Task<bool?> IsAcFitForOcAsync()
    {
        try
        {
            var result = await WMI.LenovoGameZoneData.IsACFitForOCAsync().ConfigureAwait(false);

            Log.Instance.Trace($"Mode = {result}");

            return result == 1;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<bool?> IsChargingNormallyAsync()
    {
        try
        {
            var result = await WMI.LenovoGameZoneData.GetPowerChargeModeAsync().ConfigureAwait(false);

            Log.Instance.Trace($"Mode = {result}");

            return result == 1;
        }
        catch
        {
            return null;
        }
    }
}
