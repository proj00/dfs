using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace common
{
    public static class FilesystemUtils
    {
        public static Fs.FileSystemObject GetFileObject(string path, int chunkSize)
        {
            var obj = new Fs.FileSystemObject();
            obj.File = new Fs.File();

            var info = new FileInfo(path);

            obj.Name = info.Name;
            obj.File.Size = info.Length;
            obj.File.Hashes.ChunkSize = chunkSize;

            using var stream = new FileStream(path, FileMode.Open);
            var buffer = new byte[chunkSize];
            for (int i = 0; i < obj.File.Size / chunkSize + obj.File.Size % chunkSize; i++)
            {
                int actualRead = stream.Read(buffer, 0, chunkSize);
            }

            return obj;
        }
    }
}
