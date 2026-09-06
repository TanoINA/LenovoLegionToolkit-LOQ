using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;
using Windows.Win32;

namespace LenovoLegionToolkit.Lib.System;

public static class Power
{
    public static async Task<PowerAdapterStatus> IsPowerAdapterConnectedAsync()
    {
        if (!PInvoke.GetSystemPowerStatus(out var sps))
        {
            return PowerAdapterStatus.Connected;
        }

        var adapterConnected = sps.ACLineStatus == 1;
        if (!adapterConnected)
        {
            return PowerAdapterStatus.Disconnected;
        }

        var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
        var useWindowsPowerStatus = mi.LegionSeries >= LegionSeries.Legion_Legacy && mi.LegionSeries != LegionSeries.LOQ;
        var chargingNormally = useWindowsPowerStatus ? !(Battery.IsDischarging() ?? false) : await IsChargingNormallyLenovoAsync().ConfigureAwait(false) ?? true;

        var status = (adapterConnected, chargingNormally) switch
        {
            (true, false) => PowerAdapterStatus.ConnectedLowWattage,
            (true, _) => PowerAdapterStatus.Connected,
            _ => PowerAdapterStatus.Disconnected,
        };

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
