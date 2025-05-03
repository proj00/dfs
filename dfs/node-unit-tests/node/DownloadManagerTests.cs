using common;
using common_test;
using Fs;
using Google.Protobuf;
using Microsoft.AspNetCore.Components.Forms;
using Moq;
using node;
using Node;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace unit_tests.node
{
    using ChunkAction = Func<ByteString, FileChunk, bool>;

    public class DownloadManagerTests
    {
        Mock<IPersistentCache<ByteString, Ui.Progress>> progress = new();
        Mock<IPersistentCache<ByteString, Node.FileChunk>> chunks = new();
        private static Bogus.Faker faker = new();

        [SetUp]
        public void Setup()
        {
        }

        [TearDown]
        public void TearDown()
        {
            progress.Reset();
            chunks.Reset();
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
            // arrange
            var obj = GenerateObject();
            Dictionary<ByteString, FileChunk> added = new(new ByteStringComparer());
            chunks.Setup(self => self.SetAsync(It.IsAny<ByteString>(), It.IsAny<FileChunk>()))
                .Callback((ByteString k, FileChunk v) =>
            {
                added[k] = v;
            }).Returns(Task.CompletedTask);

            // act
            using (var manager = new DownloadManager("path", 20000, progress.Object, chunks.Object))
            {
                await manager.AddNewFileAsync(obj, new Uri(faker.Internet.Url()), faker.System.DirectoryPath());
            }

            // assert
            progress.Verify(self => self.SetAsync(obj.Hash, new() { Current = 0, Total = obj.Object.File.Size }), Times.Once());
            chunks.Verify(self => self.SetAsync(It.IsAny<ByteString>(), It.IsAny<FileChunk>()),
                Times.Exactly(obj.Object.File.Hashes.Hash.Count));
            using (Assert.EnterMultipleScope())
            {
                foreach (var k in obj.Object.File.Hashes.Hash)
                {
                    Assert.That(added.ContainsKey(ByteString.CopyFrom(obj.Hash.Concat(k).ToArray())));
                }
            }
        }

        [Test]
        public async Task PauseFile_RequestHandledAsync()
        {
            // arrange
            var obj = GenerateObject();
            Dictionary<ByteString, FileChunk> added = new(new ByteStringComparer());
            chunks.Setup(self => self.SetAsync(It.IsAny<ByteString>(), It.IsAny<FileChunk>()))
                .Callback((ByteString k, FileChunk v) =>
            {
                if (v.Status == DownloadStatus.Paused)
                    added[k] = v;
            }).Returns(Task.CompletedTask);


            int good = 0;
            int canceled = 0;
            // act
            using (var manager = new DownloadManager("path", 200000, progress.Object, chunks.Object))
            {
                manager.AddChunkUpdateCallback(async (chunk, token) =>
                {
                    Interlocked.Increment(ref good);

                    try
                    {
                        await Task.Delay(200000, token);
                    }
                    catch
                    {
                        Interlocked.Increment(ref canceled);
                        throw;
                    }
                    return chunk;
                });
                await manager.AddNewFileAsync(obj, new Uri(faker.Internet.Url()), faker.System.DirectoryPath());
                await manager.PauseDownloadAsync(obj);
            }

            TestContext.Out.WriteLine(good);
            TestContext.Out.WriteLine(canceled);
            TestContext.Out.WriteLine(added.Count);
            // assert
            progress.Verify(self => self.SetAsync(obj.Hash, new() { Current = 0, Total = obj.Object.File.Size }), Times.Once());
            Assert.That(added, Has.Count.EqualTo(canceled));
        }

        private static ObjectWithHash GenerateObject()
        {
            int fileSize = 1048576;
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
