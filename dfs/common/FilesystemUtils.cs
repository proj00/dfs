using Fs;
using Google.Protobuf;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Tracker;

namespace common
{
    public static partial class FilesystemUtils
    {
        public static Fs.FileSystemObject GetFileObject(string path, int chunkSize)
        {
            Debug.Assert(System.IO.File.Exists(path), $"file not found: {path}");

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
            long chunkCount = obj.File.Size / chunkSize;
            chunkCount += obj.File.Size % chunkSize == 0 ? 0 : 1;
            Debug.Assert(chunkCount > 0);
            for (int i = 0; i < chunkCount; i++)
            {
                int actualRead = stream.Read(buffer, 0, chunkSize);
                if (actualRead < chunkSize)
                {
                    Array.Fill<byte>(buffer, 0, actualRead, chunkSize - actualRead);
                }

                var hash = HashUtils.GetHash(buffer.AsSpan(0, actualRead));
                obj.File.Hashes.Hash.Add(hash);
            }
            Debug.Assert(obj.File.Hashes.Hash.Count > 0);
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

            if (targetPath.StartsWith(@"\\?\", System.StringComparison.Ordinal))
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
                throw new FileNotFoundException("GetLinkObject didn't receive symlink");
            }

            obj.Link = new();
            obj.Link.TargetPath = target;
            return obj;
        }

        public static Fs.FileSystemObject GetDirectoryObject(string path, IReadOnlyList<ByteString> hashes)
        {
            var obj = new Fs.FileSystemObject
            {
                Name = Path.GetFileName(path)
            };

            obj.Directory = new Fs.Directory();
            foreach (var hash in hashes.Order().Distinct())
            {
                obj.Directory.Entries.Add(hash);
            }

            return obj;
        }

        public static IReadOnlyDictionary<ByteString, ObjectWithHash> RemoveObjectFromTree(IReadOnlyList<ObjectWithHash> tree, ByteString treeRoot, ByteString target, ByteString parent)
        {
            ArgumentNullException.ThrowIfNull(tree);
            Dictionary<ByteString, ObjectWithHash> diff = [];
            Dictionary<ByteString, ObjectWithHash> lookup = new(new ByteStringComparer());
            foreach (var obj in tree)
            {
                lookup[obj.Hash] = obj;
            }

            _ = Traverse(treeRoot, target, parent, diff, lookup, true);
            return diff;
        }

        public static IReadOnlyDictionary<ByteString, ObjectWithHash> AddObjectToTree(IReadOnlyList<ObjectWithHash> tree, ByteString treeRoot, ByteString target, ByteString parent)
        {
            ArgumentNullException.ThrowIfNull(tree);
            Dictionary<ByteString, ObjectWithHash> diff = [];
            Dictionary<ByteString, ObjectWithHash> lookup = new(new ByteStringComparer());
            foreach (var obj in tree)
            {
                lookup[obj.Hash] = obj;
            }

            _ = Traverse(treeRoot, target, parent, diff, lookup, false);
            return diff;
        }

        private static ByteString Traverse(ByteString hash, ByteString target, ByteString parent, Dictionary<ByteString, ObjectWithHash> diff, Dictionary<ByteString, ObjectWithHash> lookup, bool remove)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(parent);

            var current = lookup[hash];
            if (current.Object.TypeCase != FileSystemObject.TypeOneofCase.Directory)
            {
                return hash;
            }

            List<ByteString> entries = [];
            foreach (var entry in current.Object.Directory.Entries)
            {
                if (remove && entry == target && hash == parent)
                {
                    continue;
                }

                var child = Traverse(entry, target, parent, diff, lookup, remove);
                entries.Add(child);
            }
            if (!remove && hash == parent)
            {
                entries.Add(target);
            }

            var obj = GetDirectoryObject(current.Object.Name, entries);
            var h = new ObjectWithHash() { Hash = HashUtils.GetHash(obj), Object = obj };
            if (current.Hash != h.Hash)
            {
                diff[current.Hash] = h;
            }
            return h.Hash;
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
