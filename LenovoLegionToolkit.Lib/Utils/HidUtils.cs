using System;
using System.Runtime.InteropServices;
using System.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace LenovoLegionToolkit.Lib.Utils;

public static class HidUtils
{
    private static readonly Lock IoLock = new();

    public static unsafe bool SetFeature<T>(SafeHandle handle, T data) where T : notnull
    {
        lock (IoLock)
        {
            if (handle.IsClosed || handle.IsInvalid)
                return false;

            var ptr = IntPtr.Zero;
            var handleAdded = false;
            try
            {
                handle.DangerousAddRef(ref handleAdded);

                int size;
                if (data is byte[] bytes)
                {
                    size = bytes.Length;
                    ptr = Marshal.AllocHGlobal(size);
                    Marshal.Copy(bytes, 0, ptr, size);
                }
                else
                {
                    size = Marshal.SizeOf<T>();
                    ptr = Marshal.AllocHGlobal(size);
                    Marshal.StructureToPtr(data, ptr, false);
                }

                return PInvoke.HidD_SetFeature(new HANDLE(handle.DangerousGetHandle()), ptr.ToPointer(), (uint)size);
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            finally
            {
                if (handleAdded)
                    handle.DangerousRelease();
                Marshal.FreeHGlobal(ptr);
            }
        }
    }

    public static unsafe bool GetFeature<T>(SafeHandle handle, out T result) where T : struct
    {
        lock (IoLock)
        {
            result = default;
            if (handle.IsClosed || handle.IsInvalid)
                return false;

            var ptr = IntPtr.Zero;
            var handleAdded = false;
            try
            {
                handle.DangerousAddRef(ref handleAdded);

                var size = Marshal.SizeOf<T>();
                ptr = Marshal.AllocHGlobal(size);
                Marshal.Copy(new byte[] { 7 }, 0, ptr, 1);

                if (!PInvoke.HidD_GetFeature(new HANDLE(handle.DangerousGetHandle()), ptr.ToPointer(), (uint)size))
                    return false;

                result = Marshal.PtrToStructure<T>(ptr);
                return true;
            }
            catch (ObjectDisposedException)
            {
                result = default;
                return false;
            }
            finally
            {
                if (handleAdded)
                    handle.DangerousRelease();
                Marshal.FreeHGlobal(ptr);
            }
        }
    }

    public static unsafe bool GetFeature(SafeHandle handle, out byte[] bytes, int size)
    {
        lock (IoLock)
        {
            bytes = [];
            if (handle.IsClosed || handle.IsInvalid)
                return false;

            var ptr = IntPtr.Zero;
            var handleAdded = false;
            try
            {
                handle.DangerousAddRef(ref handleAdded);

                ptr = Marshal.AllocHGlobal(size);
                Marshal.Copy(new byte[] { 7 }, 0, ptr, 1);

                if (!PInvoke.HidD_GetFeature(new HANDLE(handle.DangerousGetHandle()), ptr.ToPointer(), (uint)size))
                    return false;

                bytes = new byte[size];
                Marshal.Copy(ptr, bytes, 0, size);
                return true;
            }
            catch (ObjectDisposedException)
            {
                bytes = [];
                return false;
            }
            finally
            {
                if (handleAdded)
                    handle.DangerousRelease();
                Marshal.FreeHGlobal(ptr);
            }
        }
    }
}