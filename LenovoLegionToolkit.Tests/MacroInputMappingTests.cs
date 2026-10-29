using System;
using System.Drawing;
using System.Reflection;
using LenovoLegionToolkit.Lib.Macro;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace LenovoLegionToolkit.Tests;

[TestClass]
public class MacroInputMappingTests
{
    [TestMethod]
    public void HorizontalWheel_UsesWheelFlagAndRecordedDelta()
    {
        var macroEvent = new MacroEvent
        {
            Source = MacroSource.Mouse,
            Direction = MacroDirection.HorizontalWheel,
            Key = unchecked((uint)-120)
        };

        var input = ToMouseInput(macroEvent, new Rectangle(0, 0, 1920, 1080));

        Assert.AreEqual(MOUSE_EVENT_FLAGS.MOUSEEVENTF_HWHEEL, input.Anonymous.mi.dwFlags);
        Assert.AreEqual(macroEvent.Key, input.Anonymous.mi.mouseData);
    }

    [TestMethod]
    public void AbsoluteMovement_UsesVirtualScreenOriginAndBounds()
    {
        var screenArea = new Rectangle(-1920, -100, 3840, 2160);
        var macroEvent = new MacroEvent
        {
            Source = MacroSource.Mouse,
            Direction = MacroDirection.Move,
            Point = new Point(screenArea.Left, screenArea.Top)
        };

        var topLeft = ToMouseInput(macroEvent, screenArea);
        var expectedFlags = MOUSE_EVENT_FLAGS.MOUSEEVENTF_MOVE |
                            MOUSE_EVENT_FLAGS.MOUSEEVENTF_ABSOLUTE |
                            MOUSE_EVENT_FLAGS.MOUSEEVENTF_VIRTUALDESK;

        Assert.AreEqual(expectedFlags, topLeft.Anonymous.mi.dwFlags);
        Assert.AreEqual(0, topLeft.Anonymous.mi.dx);
        Assert.AreEqual(0, topLeft.Anonymous.mi.dy);

        macroEvent = new MacroEvent
        {
            Source = MacroSource.Mouse,
            Direction = MacroDirection.Move,
            Point = new Point(screenArea.Right - 1, screenArea.Bottom - 1)
        };
        var bottomRight = ToMouseInput(macroEvent, screenArea);

        Assert.AreEqual(65535, bottomRight.Anonymous.mi.dx);
        Assert.AreEqual(65535, bottomRight.Anonymous.mi.dy);
    }

    private static INPUT ToMouseInput(MacroEvent macroEvent, Rectangle screenArea)
    {
        var playerType = typeof(MacroEvent).Assembly.GetType("LenovoLegionToolkit.Lib.Macro.Utils.MacroPlayer", throwOnError: true)!;
        var method = playerType.GetMethod("ToMouseInput", BindingFlags.NonPublic | BindingFlags.Static)
                     ?? throw new MissingMethodException(playerType.FullName, "ToMouseInput");

        return (INPUT)(method.Invoke(null, new object[] { macroEvent, screenArea })
                       ?? throw new InvalidOperationException("Input conversion returned null."));
    }
}
