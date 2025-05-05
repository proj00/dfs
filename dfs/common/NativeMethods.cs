using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace common
{
    public static partial class FilesystemUtils
    {
        public class NativeMethods : INativeMethods
        {
            public SafeFileHandle GetFileHandle(string path)
            {
                return CreateFile(path, 0, FileShare.ReadWrite | FileShare.Delete, IntPtr.Zero, FileMode.Open, FileAttributes.None, IntPtr.Zero);
            }

            public byte[]? GetReparsePoint(SafeFileHandle handle)
            {
                byte[] buffer = new byte[1024];
                if (!DeviceIoControl(handle, INativeMethods.FSCTL_GET_REPARSE_POINT, IntPtr.Zero, 0, buffer, buffer.Length, out _, IntPtr.Zero))
                {
                    return null;
                }
                return buffer;
            }

            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            private static extern SafeFileHandle CreateFile(
                string lpFileName,
                int dwDesiredAccess,
                FileShare dwShareMode,
                IntPtr lpSecurityAttributes,
                FileMode dwCreationDisposition,
                FileAttributes dwFlagsAndAttributes,
                IntPtr hTemplateFile);

            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            [return: MarshalAs(UnmanagedType.Bool)]
            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool DeviceIoControl(
                SafeFileHandle hDevice,
                int dwIoControlCode,
                IntPtr lpInBuffer,
                int nInBufferSize,
                byte[] lpOutBuffer,
                int nOutBufferSize,
                out int lpBytesReturned,
                IntPtr lpOverlapped);
        }
    }
