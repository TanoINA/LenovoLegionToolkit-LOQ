using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Listeners;

public class ThermalModeListener(
    WindowsPowerModeController windowsPowerModeController,
    WindowsPowerPlanController windowsPowerPlanController,
    PowerModeListener powerModeListener)
    : AbstractWMIListener<ThermalModeListener.ChangedEventArgs, ThermalModeState, int>(WMI.LenovoGameZoneThermalModeEvent.Listen)
{
    public class ChangedEventArgs(ThermalModeState state) : EventArgs
    {
        public ThermalModeState State { get; } = state;
    }

    private readonly object _suppressionLock = new();
    private ThermalModeState? _suppressedState;
    private DateTime _suppressionExpiresUtc = DateTime.MinValue;

    public void SuppressNext(ThermalModeState expectedState, TimeSpan lifetime)
    {
        lock (_suppressionLock)
        {
            _suppressedState = expectedState;
            _suppressionExpiresUtc = DateTime.UtcNow + lifetime;
            Log.Instance.Trace($"ThermalModeListener: Suppressing expected WMI event {expectedState} until {_suppressionExpiresUtc:HH:mm:ss.fff}");
        }
    }

    public void SuppressNext(ThermalModeState expectedState) => SuppressNext(expectedState, TimeSpan.FromMilliseconds(1000));

    public void SuppressNext() => SuppressNext(ThermalModeState.Balance, TimeSpan.FromMilliseconds(1000));

    private bool ConsumeSuppression(ThermalModeState actualState)
    {
        lock (_suppressionLock)
        {
            if (_suppressedState == actualState && DateTime.UtcNow < _suppressionExpiresUtc)
            {
                _suppressedState = null;
                _suppressionExpiresUtc = DateTime.MinValue;
                return true;
            }
            return false;
        }
    }

    protected override ThermalModeState GetValue(int value)
    {
        var state = (ThermalModeState)value;

        if (!Enum.IsDefined(state))
        {
            Log.Instance.Trace($"Unknown value received: {value}");

            state = ThermalModeState.Unknown;
        }

        return state;
    }

    protected override ChangedEventArgs GetEventArgs(ThermalModeState value) => new(value);

    protected override async Task OnChangedAsync(ThermalModeState state)
    {
        if (ConsumeSuppression(state))
        {
            Log.Instance.Trace($"ThermalModeListener: Suppressed expected WMI event for {state}.");
            return;
        }

        Log.Instance.Trace($"ThermalModeListener.OnChangedAsync: state={state}");

        if (state == ThermalModeState.Unknown)
            return;

        var powerModeState = state switch
        {
            ThermalModeState.Quiet => PowerModeState.Quiet,
            ThermalModeState.Balance => PowerModeState.Balance,
            ThermalModeState.Performance => PowerModeState.Performance,
            ThermalModeState.Extreme => PowerModeState.Extreme,
            ThermalModeState.GodMode => PowerModeState.GodMode,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };

        if (powerModeListener.IsRecentlyProcessed(powerModeState, TimeSpan.FromMilliseconds(500)))
        {
            Log.Instance.Trace($"ThermalModeListener: state {state} already processed recently by PowerModeListener. Skipping redundant apply.");
            return;
        }

        Log.Instance.Trace($"ThermalModeListener mapping: ThermalMode={state} -> PowerMode={powerModeState}");

        await windowsPowerModeController.SetPowerModeAsync(powerModeState).ConfigureAwait(false);
        await windowsPowerPlanController.SetPowerPlanAsync(powerModeState).ConfigureAwait(false);
    }
}
