using common;
using Fs;
using Google.Protobuf;
using Moq;
using System;
using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace unit_tests.common
{
    class FilesystemUtilsTests
    {
        private static readonly Bogus.Faker faker = new();

        [Test]
        public void TestGetFileObject_Returns()
        {
            var contents = faker.Random.Bytes(1024);
            var fs = new MockFileSystem(new Dictionary<string, MockFileData>() { { "path", new(contents) } }, new MockFileSystemOptions());
            var result = FilesystemUtils.GetFileObject(fs, "path", 1);
            ValidateFile(contents, result, "path");
        }

        private static void ValidateFile(byte[] contents, FileSystemObject result, string name)
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Name, Is.EqualTo(name));
                Assert.That(result.TypeCase, Is.EqualTo(FileSystemObject.TypeOneofCase.File));
                Assert.That(result.File, Is.Not.Null);
                Assert.That(result.File.Hashes.ChunkSize, Is.EqualTo(1));

                Assert.That(result.File.Size, Is.EqualTo(contents.Length));
                Assert.That(result.File.Hashes.Hash, Has.Count.EqualTo(contents.Length));
                for (int i = 0; i < contents.Length; i++)
                {
                    Assert.That(HashUtils.GetHash([contents[i]]), Is.EqualTo(result.File.Hashes.Hash[i]));
                }
            }
        }

        [Test]
        public void TestGetRecursiveDirectoryObject_Returns()
        {
            var contents = faker.Random.Bytes(1024);
            var fs = new MockFileSystem(
                new Dictionary<string, MockFileData>()
                {
                    {"root_dir/subdir/file", new(contents) }
                },
                new MockFileSystemOptions() { CurrentDirectory = "C:/" });

            List<Fs.FileSystemObject> objects = [];
            Action<ByteString, string, Fs.FileSystemObject> appender = (hash, path, obj) =>
            {
                objects.Add(obj);
            };

            var result = FilesystemUtils.GetRecursiveDirectoryObject(fs, new Mock<INativeMethods>().Object, "./root_dir", 1, appender);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(objects, Has.Count.EqualTo(3));
                var root = objects.Find(o => o.Name == "root_dir");
                var subdir = objects.Find(o => o.Name == "subdir");
                var file = objects.Find(o => o.Name == "file");
                Assert.That(root, Is.Not.Null);
                Assert.That(subdir, Is.Not.Null);
                Assert.That(file, Is.Not.Null);

                ValidateFile(contents, file, "file");
                Assert.That(root.Directory.Entries, Does.Contain(HashUtils.GetHash(subdir)));
                Assert.That(subdir.Directory.Entries, Does.Contain(HashUtils.GetHash(file)));
            }
        }
    }
}
