using Microsoft.Win32.SafeHandles;

namespace common
{
    public interface INativeMethods
    {
        public SafeFileHandle GetFileHandle(string path);
        public byte[]? GetReparsePoint(SafeFileHandle handle);
#pragma warning disable CA1707 // Identifiers should not contain underscores
        public const int FSCTL_GET_REPARSE_POINT = 0x000900A8;
        public const int IO_REPARSE_TAG_SYMLINK = unchecked((int)0xA000000C);
#pragma warning restore CA1707 // Identifiers should not contain underscores
    }
}
