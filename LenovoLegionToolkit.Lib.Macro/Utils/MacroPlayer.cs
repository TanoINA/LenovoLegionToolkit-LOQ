using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Macro.Utils;

internal class MacroPlayer : IDisposable
{
    private const int MAGIC_NUMBER = 1337;

    private readonly ThreadSafeBool _isPlayingInterruptableSequence = new();
    private readonly SemaphoreSlim _playbackLock = new(1, 1);

    private Task _playTask = Task.CompletedTask;
    private CancellationTokenSource _cancellationTokenSource = new();
    private bool _disposed;

    public void InterruptIfNeeded(KBDLLHOOKSTRUCT kbStruct)
    {
        if (_disposed || !_isPlayingInterruptableSequence.Value)
            return;
        if (kbStruct.flags != 0)
            return;
        if (kbStruct.dwExtraInfo == MAGIC_NUMBER)
            return;

        try
        {
            var cancellationTokenSource = _cancellationTokenSource;
            if (!cancellationTokenSource.IsCancellationRequested)
                cancellationTokenSource.Cancel();
        }
        catch (ObjectDisposedException) { }
    }

    public async Task StartPlayingAsync(MacroSequence sequence)
    {
        await _playbackLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
                return;

            var previousCancellationTokenSource = _cancellationTokenSource;
            await previousCancellationTokenSource.CancelAsync().ConfigureAwait(false);
            try { await _playTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Log.Instance.Trace($"Previous macro playback failed.", ex); }
            previousCancellationTokenSource.Dispose();

            var cancellationTokenSource = new CancellationTokenSource();
            _cancellationTokenSource = cancellationTokenSource;
            _playTask = Task.Run(() => PlayAsync(sequence, cancellationTokenSource.Token), CancellationToken.None);
        }
        finally
        {
            _playbackLock.Release();
        }
    }

    private async Task PlayAsync(MacroSequence sequence, CancellationToken token)
    {
        var heldEvents = new List<MacroEvent>();
        var screenArea = SystemInformation.VirtualScreen;
        _isPlayingInterruptableSequence.Value = sequence.InterruptOnOtherKey;

        try
        {
            for (var i = 0; i < sequence.RepeatCount; i++)
            {
                foreach (var macroEvent in sequence.Events ?? [])
                {
                    if (!sequence.IgnoreDelays)
                        await Task.Delay(macroEvent.Delay, token).ConfigureAwait(false);

                    token.ThrowIfCancellationRequested();

                    try
                    {
                        var input = ToInput(macroEvent, screenArea);
                        var result = PInvoke.SendInput(MemoryMarshal.CreateSpan(ref input, 1), Marshal.SizeOf<INPUT>());
                        if (result == 0)
                            PInvokeExtensions.ThrowIfWin32Error($"Failed to send input. Return code was {result}.");

                        UpdateHeldInputs(heldEvents, macroEvent);
                    }
                    catch (Exception ex)
                    {
                        Log.Instance.Trace($"Failed to send input for event {macroEvent}", ex);
                    }
                }
            }
        }
        finally
        {
            _isPlayingInterruptableSequence.Value = false;
            ReleaseHeldInputs(heldEvents, screenArea);
        }
    }

    private static void UpdateHeldInputs(List<MacroEvent> heldEvents, MacroEvent macroEvent)
    {
        if (macroEvent.Direction == MacroDirection.Down)
        {
            heldEvents.Add(macroEvent);
            return;
        }

        if (macroEvent.Direction != MacroDirection.Up)
            return;

        for (var i = heldEvents.Count - 1; i >= 0; i--)
        {
            if (heldEvents[i].Source != macroEvent.Source || heldEvents[i].Key != macroEvent.Key)
                continue;

            heldEvents.RemoveAt(i);
            break;
        }
    }

    private static void ReleaseHeldInputs(List<MacroEvent> heldEvents, Rectangle screenArea)
    {
        for (var i = heldEvents.Count - 1; i >= 0; i--)
        {
            var heldEvent = heldEvents[i];
            var releaseEvent = new MacroEvent
            {
                Source = heldEvent.Source,
                Direction = MacroDirection.Up,
                Key = heldEvent.Key,
                Point = heldEvent.Point
            };

            try
            {
                var input = ToInput(releaseEvent, screenArea);
                var result = PInvoke.SendInput(MemoryMarshal.CreateSpan(ref input, 1), Marshal.SizeOf<INPUT>());
                if (result == 0)
                    PInvokeExtensions.ThrowIfWin32Error($"Failed to release macro input. Return code was {result}.");
            }
            catch (Exception ex)
            {
                Log.Instance.Trace($"Failed to release input for event {releaseEvent}", ex);
            }
        }
    }

    public void Dispose()
    {
        _playbackLock.Wait();
        try
        {
            if (_disposed)
                return;

            _disposed = true;
            _cancellationTokenSource.Cancel();
            try { _playTask.GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Log.Instance.Trace($"Macro playback failed during disposal.", ex); }
            _cancellationTokenSource.Dispose();
        }
        finally
        {
            _playbackLock.Release();
        }
    }

    private static INPUT ToInput(MacroEvent macroEvent, Rectangle screenArea) => macroEvent.Source switch
    {
        MacroSource.Keyboard => ToKeyboardInput(macroEvent),
        MacroSource.Mouse => ToMouseInput(macroEvent, screenArea),
        MacroSource.Unknown => throw new ArgumentException(null, nameof(macroEvent)),
        _ => throw new ArgumentOutOfRangeException(nameof(macroEvent))
    };

    private static INPUT ToKeyboardInput(MacroEvent macroEvent) => new()
    {
        type = INPUT_TYPE.INPUT_KEYBOARD,
        Anonymous = new INPUT._Anonymous_e__Union
        {
            ki = new KEYBDINPUT
            {
                wVk = (VIRTUAL_KEY)macroEvent.Key,
                dwFlags = macroEvent.Direction switch
                {
                    MacroDirection.Up => KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP,
                    _ => 0
                },
                dwExtraInfo = MAGIC_NUMBER
            }
        }
    };

    private static INPUT ToMouseInput(MacroEvent macroEvent, Rectangle screenArea) => new()
    {
        type = INPUT_TYPE.INPUT_MOUSE,
        Anonymous = new INPUT._Anonymous_e__Union
        {
            mi = new MOUSEINPUT
            {
                dwFlags = (macroEvent.Direction, macroEvent.Key) switch
                {
                    (MacroDirection.Up, 1) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTUP,
                    (MacroDirection.Down, 1) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTDOWN,
                    (MacroDirection.Up, 2) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTUP,
                    (MacroDirection.Down, 2) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTDOWN,
                    (MacroDirection.Up, 3) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_MIDDLEUP,
                    (MacroDirection.Down, 3) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_MIDDLEDOWN,
                    (MacroDirection.Up, > 0xFF) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_XUP,
                    (MacroDirection.Down, > 0xFF) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_XDOWN,
                    (MacroDirection.Wheel, _) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_WHEEL,
                    (MacroDirection.HorizontalWheel, _) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_HWHEEL,
                    (MacroDirection.Move, _) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_MOVE | MOUSE_EVENT_FLAGS.MOUSEEVENTF_ABSOLUTE | MOUSE_EVENT_FLAGS.MOUSEEVENTF_VIRTUALDESK,
                    _ => 0
                },
                mouseData = (macroEvent.Direction, macroEvent.Key) switch
                {
                    (MacroDirection.Up, >= 0xFF) => macroEvent.Key >> 16,
                    (MacroDirection.Down, >= 0xFF) => macroEvent.Key >> 16,
                    (MacroDirection.Wheel, _) => macroEvent.Key,
                    (MacroDirection.HorizontalWheel, _) => macroEvent.Key,
                    _ => 0
                },
                dx = macroEvent.Direction switch
                {
                    MacroDirection.Move => NormalizeCoordinate(macroEvent.Point.X, screenArea.Left, screenArea.Width),
                    _ => 0
                },
                dy = macroEvent.Direction switch
                {
                    MacroDirection.Move => NormalizeCoordinate(macroEvent.Point.Y, screenArea.Top, screenArea.Height),
                    _ => 0
                },
                dwExtraInfo = MAGIC_NUMBER
            }
        }
    };

    private static int NormalizeCoordinate(int point, int origin, int extent)
    {
        if (extent <= 1)
            return 0;

        var value = (int)(65535.0f * ((point - origin) / (float)(extent - 1)) + 0.5f);
        return Math.Clamp(value, 0, 65535);
    }
}
