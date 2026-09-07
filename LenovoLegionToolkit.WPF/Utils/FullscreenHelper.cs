using System;
using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.System.Threading;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.WPF.Utils;

public static class FullscreenHelper
{
    public static unsafe bool IsAnyApplicationFullscreen()
    {
        try
        {
            var desktopWindowHandle = PInvoke.GetDesktopWindow();
            var shellWindowHandle = PInvoke.GetShellWindow();

            var foregroundWindowHandle = PInvoke.GetForegroundWindow();
            if (foregroundWindowHandle == HWND.Null)
                return false;
            if (foregroundWindowHandle == desktopWindowHandle)
                return false;
            if (foregroundWindowHandle == shellWindowHandle)
                return false;

            if (!PInvoke.GetWindowRect(foregroundWindowHandle, out var appBounds))
                return false;

            var hMonitor = PInvoke.MonitorFromWindow(foregroundWindowHandle, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
            if (hMonitor == HMONITOR.Null)
                return false;

            MONITORINFO monitorInfo = new() { cbSize = (uint)sizeof(MONITORINFO) };
            if (!PInvoke.GetMonitorInfo(hMonitor, &monitorInfo))
                return false;

            var screenBounds = monitorInfo.rcMonitor;
            var screenWidth = screenBounds.right - screenBounds.left;
            var screenHeight = screenBounds.bottom - screenBounds.top;

            var coversFullScreen = (appBounds.bottom - appBounds.top >= screenHeight) &&
                                   (appBounds.right - appBounds.left >= screenWidth);
            if (!coversFullScreen)
                return false;

            var processId = 0u;
            _ = PInvoke.GetWindowThreadProcessId(foregroundWindowHandle, &processId);
            if (processId == 0)
                return false;

            try
            {
                var hProcess = PInvoke.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
                if (hProcess != HANDLE.Null && hProcess.Value != null)
                {
                    try
                    {
                        char* buffer = stackalloc char[1024];
                        uint size = 1024;
                        if (PInvoke.QueryFullProcessImageName(hProcess, PROCESS_NAME_FORMAT.PROCESS_NAME_NATIVE, buffer, &size))
                        {
                            var imageName = new string(buffer, 0, (int)size);
                            return !imageName.EndsWith("explorer.exe", StringComparison.OrdinalIgnoreCase);
                        }
                    }
                    finally
                    {
                        PInvoke.CloseHandle(hProcess);
                    }
                }
            }
            catch
            {
                // Fallback
            }

            try
            {
                using var process = Process.GetProcessById((int)processId);
                return process.ProcessName != "explorer";
            }
            catch
            {
                return false;
            }
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"Couldn't check if application is full screen.", ex);

            return false;
        }
    }
}
