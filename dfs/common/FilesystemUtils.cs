using Google.Protobuf;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Tracker;

namespace common
{
    public static class FilesystemUtils
    {
        public static Fs.FileSystemObject GetFileObject(string path, int chunkSize)
        {
            var info = new FileInfo(path);

            var obj = new Fs.FileSystemObject
            {
                File = new Fs.File(),
                Name = info.Name
            };

            obj.File.Hashes = new Fs.ChunkHashes();
            obj.File.Size = info.Length;
            obj.File.Hashes.ChunkSize = chunkSize;

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var buffer = new byte[chunkSize];
            for (int i = 0; i < obj.File.Size / chunkSize + (obj.File.Size % chunkSize == 0 ? 0 : 1); i++)
            {
                int actualRead = stream.Read(buffer, 0, chunkSize);
                if (actualRead < chunkSize)
                {
                    Array.Fill<byte>(buffer, 0, actualRead, chunkSize - actualRead);
                }

                var hash = HashUtils.GetHash(buffer);
                obj.File.Hashes.Hash.Add(hash);
            }

            return obj;
        }

        public static string? GetLinkTarget(string path)
        {
            using var handle = CreateFile(path, 0, FileShare.ReadWrite | FileShare.Delete, IntPtr.Zero, FileMode.Open, FileAttributes.None, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                return null;
            }

            var buffer = new byte[1024];
            int returned = 0;
            if (!DeviceIoControl(handle, FSCTL_GET_REPARSE_POINT, IntPtr.Zero, 0, buffer, buffer.Length, out returned, IntPtr.Zero))
            {
                return null;
            }

            int tag = BitConverter.ToInt32(buffer, 0);
            if (tag != IO_REPARSE_TAG_SYMLINK)
            {
                return null;
            }

            int targetOffset = BitConverter.ToInt16(buffer, 8);
            int targetLength = BitConverter.ToInt16(buffer, 10);
            string targetPath = Encoding.Unicode.GetString(buffer, targetOffset + 20, targetLength);

            if (targetPath.StartsWith(@"\\?\"))
            {
                targetPath = targetPath.Substring(4);
            }

            return targetPath;
        }

        public static Fs.FileSystemObject GetLinkObject(string path)
        {
            var obj = new Fs.FileSystemObject();
            obj.Name = Path.GetFileName(path);

            var target = GetLinkTarget(path);
            if (target == null)
            {
                throw new Exception("GetLinkObject didn't receive symlink");
            }

            obj.Link = new Fs.Link();
            obj.Link.TargetPath = target;
            return obj;
        }

        public static Fs.FileSystemObject GetDirectoryObject(string path, List<ByteString> hashes)
        {
            var obj = new Fs.FileSystemObject
            {
                Name = Path.GetFileName(path)
            };

            hashes.Sort(new HashUtils.ByteStringComparer());
            obj.Directory = new Fs.Directory();
            foreach (var hash in hashes.Distinct())
            {
                obj.Directory.Entries.Add(hash);
            }

            return obj;
        }

        public static ByteString GetRecursiveDirectoryObject(string path, int chunkSize, Action<ByteString, string, Fs.FileSystemObject> appendHashPathObj)
        {
            void Add(ByteString hash, string path, Fs.FileSystemObject obj)
            {
                appendHashPathObj(hash, path, obj);
            }

            ObjectWithHash GetInternal(DirectoryInfo info)
            {
                List<ObjectWithHash> children = [];

                foreach (var file in info.GetFiles())
                {
                    var obj = GetLinkTarget(file.FullName) == null ? GetFileObject(file.FullName, chunkSize) : GetLinkObject(file.FullName);
                    var hash = HashUtils.GetHash(obj);

                    children.Add(new ObjectWithHash { Hash = hash, Object = obj });
                    Add(hash, file.FullName, obj);
                }

                foreach (var dir in info.GetDirectories())
                {
                    if (GetLinkTarget(dir.FullName) != null)
                    {
                        var obj = GetLinkObject(dir.FullName);
                        var hash = HashUtils.GetHash(obj);

                        children.Add(new ObjectWithHash { Hash = hash, Object = obj });
                        Add(hash, dir.FullName, obj);

                        continue;
                    }

                    var o = GetInternal(dir);
                    children.Add(o);
                    Add(o.Hash, dir.FullName, o.Object);
                }

                var current = GetDirectoryObject(info.FullName, children.Select(a => a.Hash).ToList());
                var currentHash = HashUtils.GetHash(current);

                Add(currentHash, info.FullName, current);
                return new ObjectWithHash { Hash = currentHash, Object = current };
            }

            return GetInternal(new DirectoryInfo(path)).Hash;
        }

        private const int FSCTL_GET_REPARSE_POINT = 0x000900A8;
        private const int IO_REPARSE_TAG_SYMLINK = unchecked((int)0xA000000C);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern SafeFileHandle CreateFile(
            string lpFileName,
            int dwDesiredAccess,
            FileShare dwShareMode,
            IntPtr lpSecurityAttributes,
            FileMode dwCreationDisposition,
            FileAttributes dwFlagsAndAttributes,
            IntPtr hTemplateFile);

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
