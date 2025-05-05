using Fs;
using Google.Protobuf;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Tracker;

namespace common
{
    public static partial class FilesystemUtils
    {
        public static Fs.FileSystemObject GetFileObject(IFileSystem fs, string path, int chunkSize)
        {
            ArgumentNullException.ThrowIfNull(fs);
            Debug.Assert(fs.File.Exists(path), $"file not found: {path}");

            var info = fs.FileInfo.New(path);

            var obj = new Fs.FileSystemObject
            {
                File = new Fs.File(),
                Name = info.Name
            };

            obj.File.Hashes = new Fs.ChunkHashes();
            obj.File.Size = info.Length;
            obj.File.Hashes.ChunkSize = chunkSize;

            using var stream = fs.FileStream.New(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
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

        public static string? GetLinkTarget(string path,
            INativeMethods nativeMethods)
        {
            ArgumentNullException.ThrowIfNull(nativeMethods);
            using var handle = nativeMethods.GetFileHandle(path);
            if (handle.IsInvalid)
            {
                return null;
            }

            byte[]? buffer = nativeMethods.GetReparsePoint(handle);
            if (buffer == null)
            {
                return null;
            }

            int tag = BitConverter.ToInt32(buffer, 0);
            if (tag != INativeMethods.IO_REPARSE_TAG_SYMLINK)
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

            var target = GetLinkTarget(path, new NativeMethods());
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
            foreach (var hash in hashes.Order(new ByteStringComparer()).Distinct())
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

        public static ByteString GetRecursiveDirectoryObject(IFileSystem fs, string path, int chunkSize, Action<ByteString, string, Fs.FileSystemObject> appendHashPathObj)
        {
            ArgumentNullException.ThrowIfNull(fs);
            void Add(ByteString hash, string path, Fs.FileSystemObject obj)
            {
                appendHashPathObj(hash, path, obj);
            }

            ObjectWithHash GetInternal(IDirectoryInfo info)
            {
                List<ObjectWithHash> children = [];

                foreach (var file in info.GetFiles())
                {
                    var obj = GetLinkTarget(file.FullName, new NativeMethods()) == null
                        ? GetFileObject(fs, file.FullName, chunkSize)
                        : GetLinkObject(file.FullName);

                    var hash = HashUtils.GetHash(obj);

                    children.Add(new ObjectWithHash { Hash = hash, Object = obj });
                    Add(hash, file.FullName, obj);
                }

                foreach (var dir in info.GetDirectories())
                {
                    if (GetLinkTarget(dir.FullName, new NativeMethods()) != null)
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

            return GetInternal(fs.DirectoryInfo.New(path)).Hash;
        }
    }
}
