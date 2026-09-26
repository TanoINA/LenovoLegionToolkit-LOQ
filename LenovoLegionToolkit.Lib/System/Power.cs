using System.Threading.Tasks;
using Windows.Win32;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.System;

public static class Power
{
    private static PowerAdapterStatus _lastReportedStatus = PowerAdapterStatus.Connected;

    public static async Task<PowerAdapterStatus> IsPowerAdapterConnectedAsync()
    {
        if (!PInvoke.GetSystemPowerStatus(out var sps))
            return _lastReportedStatus;

        // ACLineStatus: 0 = Offline, 1 = Online, 255 = Unknown (transient during power plan reloads).
        // Retain the last known status to prevent false Disconnected triggers.
        if (sps.ACLineStatus == 255)
            return _lastReportedStatus;

        var adapterConnected = sps.ACLineStatus == 1;
        if (!adapterConnected)
            return _lastReportedStatus = PowerAdapterStatus.Disconnected;

        // Non-Legion series (IdeaPad, LOQ, YOGA, ThinkBook, ...) lack GameZone charger reporting;
        // querying it there yields spurious ConnectedLowWattage flips during CPU/GPU power shifts.
        var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
        var acFitForOc = true;
        var chargingNormally = true;
        if (mi.LegionSeries <= LegionSeries.Legion_Legacy)
        {
            acFitForOc = await IsAcFitForOc().ConfigureAwait(false) ?? true;
            chargingNormally = await IsChargingNormally().ConfigureAwait(false) ?? true;
        }

        return _lastReportedStatus = (adapterConnected, acFitForOc && chargingNormally) switch
        {
            (true, false) => PowerAdapterStatus.ConnectedLowWattage,
            (true, _) => PowerAdapterStatus.Connected,
            (false, _) => PowerAdapterStatus.Disconnected,
        };
    }

    public static bool IsBatterySaverEnabled()
    {
        if (!PInvoke.GetSystemPowerStatus(out var sps))
            return false;

        return sps.SystemStatusFlag == 1;
    }

    public static async Task RestartAsync()
    {
        Log.Instance.Trace($"Restarting...");

        await CMD.RunAsync("shutdown", "/r /t 0").ConfigureAwait(false);
    }

    private static async Task<bool?> IsAcFitForOc()
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

    private static async Task<bool?> IsChargingNormally()
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
