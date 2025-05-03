using common;
using common_test;
using Fs;
using Google.Protobuf;
using Moq;
using node;
using Node;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace unit_tests.node
{
    public class DownloadManagerTests
    {
        Mock<IPersistentCache<ByteString, Ui.Progress>> progress;
        Mock<IPersistentCache<ByteString, Node.FileChunk>> chunks;
        DownloadManager manager;
        private static Bogus.Faker faker = new();

        [SetUp]
        public void Setup()
        {
            progress = new();
            chunks = new();
            manager = new("path", 20000, progress.Object, chunks.Object);
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void Constructor_ThrowsOnInvalidDbPath(string? path)
        {
            if (path == null)
                Assert.Throws<ArgumentNullException>(() => new DownloadManager(path, 1,
                    progress.Object,
                    chunks.Object
                    ));
            else
                Assert.Throws<ArgumentException>(() => new DownloadManager(path, 1,
                    progress.Object,
                    chunks.Object
                    ));
        }

        [Test]
        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_ThrowsOnInvalidTaskCapacity(int capacity)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DownloadManager("dir", capacity,
                progress.Object,
                chunks.Object
                    ));
        }

        [Test]
        public void Constructor_Returns()
        {
            Assert.DoesNotThrow(() => new DownloadManager
                (
                "dir",
                1,
                progress.Object,
                chunks.Object
                ));
        }

        [Test]
        public async Task AddFile_RequestHandledAsync()
        {
            var obj = GenerateObject();
            //progress.Setup(self => self.SetAsync)
            await manager.AddNewFileAsync(obj, new Uri(faker.Internet.Url()), "dest");
        }

        private static ObjectWithHash GenerateObject()
        {
            int fileSize = faker.System.Random.Int(1024, 1048576);
            var obj = new Fs.FileSystemObject()
            {
                Name = faker.System.FileName(),
                File = new()
                {
                    Size = fileSize,
                    Hashes = new()
                    {
                        ChunkSize = 1024,
                        Hash =
                        {
                            Enumerable.Range(0, fileSize / 1024 + fileSize % 1024)
                            .Select(a => HashUtils.GetHash(faker.System.Random.Bytes((a + 1) * 10)))
                        }
                    }
                }

            };

            return new() { Hash = HashUtils.GetHash(obj), Object = obj };
        }
    }
}
