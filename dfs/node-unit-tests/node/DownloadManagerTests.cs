using common;
using common_test;
using Fs;
using Google.Protobuf;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using Moq;
using node;
using Node;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace unit_tests.node
{
    using ChunkAction = Func<ByteString, FileChunk, bool>;
    using ProgressCallback = Func<Ui.Progress?, Ui.Progress>;
    public class DownloadManagerTests
    {
        Mock<IPersistentCache<ByteString, Ui.Progress>> progress = new();
        Mock<IPersistentCache<ByteString, Node.FileChunk>> chunks = new();
        private static Bogus.Faker faker = new();
        private ILogger logger;
        private ILoggerFactory factory;
        [SetUp]
        public void Setup()
        {
            factory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddConsole()
                    .SetMinimumLevel(LogLevel.Debug);
            });
            logger = factory.CreateLogger("test");
        }

        [TearDown]
        public void TearDown()
        {
            factory.Dispose();
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
                    chunks.Object, logger
                    ));
            else
                Assert.Throws<ArgumentException>(() => new DownloadManager(path, 1,
                    progress.Object,
                    chunks.Object, logger
                    ));
        }

        [Test]
        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_ThrowsOnInvalidTaskCapacity(int capacity)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DownloadManager("dir", capacity,
                progress.Object,
                chunks.Object, logger
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
                chunks.Object, logger
                ));
        }

        [Test]
        public async Task AddFile_RequestHandledAsync()
        {
            // arrange
            var obj = MockFsUtils.GenerateObject(faker);
            ConcurrentDictionary<ByteString, FileChunk> added = new(new ByteStringComparer());
            chunks.Setup(self => self.SetAsync(It.IsAny<ByteString>(), It.IsAny<FileChunk>()))
                .Callback((ByteString k, FileChunk v) =>
            {
                added[k] = v;
            }).Returns(Task.CompletedTask);

            ConcurrentDictionary<ByteString, ByteString> completed = [];
            chunks.Setup(self => self.Remove(It.IsAny<ByteString>()))
                .Callback((ByteString k) =>
                {
                    completed[k] = k;
                }).Returns(Task.CompletedTask);

            // act
            await using (var manager = new DownloadManager("path", 20000, progress.Object, chunks.Object, logger))
            {
                manager.AddChunkUpdateCallback(async (chunk, token) =>
                {
                    chunk.Status = DownloadStatus.Complete;
                    return await Task.FromResult(chunk);
                });
                await manager.AddNewFileAsync(obj, new Uri(faker.Internet.Url()), faker.System.DirectoryPath());
            }

            // assert

            // did we mark the file as in progress?
            progress.Verify(self => self.SetAsync(obj.Hash, new() { Current = 0, Total = obj.Object.File.Size }), Times.Once());

            // did we enqueue all chunks?
            chunks.Verify(self => self.SetAsync(It.IsAny<ByteString>(), It.IsAny<FileChunk>()),
                Times.Exactly(obj.Object.File.Hashes.Hash.Count));
            using (Assert.EnterMultipleScope())
            {
                foreach (var k in obj.Object.File.Hashes.Hash)
                {
                    Assert.That(added.ContainsKey(ByteString.CopyFrom(obj.Hash.Concat(k).ToArray())));
                }
            }

            // did we cleanup all chunks after completion?
            chunks.Verify(self => self.Remove(It.IsAny<ByteString>()),
                Times.Exactly(obj.Object.File.Hashes.Hash.Count));
            using (Assert.EnterMultipleScope())
            {
                foreach (var k in obj.Object.File.Hashes.Hash)
                {
                    Assert.That(completed.ContainsKey(ByteString.CopyFrom(obj.Hash.Concat(k).ToArray())));
                }
            }
        }

        [Test]
        public async Task PauseFile_RequestHandledAsync()
        {
            // arrange
            var obj = MockFsUtils.GenerateObject(faker);
            ConcurrentDictionary<ByteString, FileChunk> added = new(new ByteStringComparer());
            chunks.Setup(self => self.SetAsync(It.IsAny<ByteString>(), It.IsAny<FileChunk>()))
                .Callback((ByteString k, FileChunk v) =>
            {
                if (v.Status == DownloadStatus.Paused)
                    added[k] = v;
            }).Returns(Task.CompletedTask);


            int good = 0;
            int canceled = 0;
            // act
            await using (var manager = new DownloadManager("path", 200000, progress.Object, chunks.Object, logger))
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

                using (var source = new CancellationTokenSource(2000))
                {
                    try
                    {
                        await manager.PauseDownloadAsync(obj, source.Token);
                    }
                    catch (OperationCanceledException e)
                    {
                        TestContext.Error.WriteLine(e);
                        throw;
                    }
                }
            }

            // assert
            progress.Verify(self => self.SetAsync(obj.Hash, new() { Current = 0, Total = obj.Object.File.Size }), Times.Once());

            // did we start all downloads?
            Assert.That(added, Has.Count.EqualTo(obj.Object.File.Hashes.Hash.Count));

            // did we cancel all downloads?
            Assert.That(added, Has.Count.EqualTo(canceled));

            // was the download callback called?
            Assert.That(added, Has.Count.EqualTo(good));
        }

        [Test]
        [NonParallelizable]
        [TestCase(500)]
        public async Task ResumeFile_RequestsHandledAsync(int downloadTime)
        {
            // arrange
            var obj = MockFsUtils.GenerateObject(faker, true);
            ConcurrentDictionary<ByteString, FileChunk> added = new(new ByteStringComparer());

            chunks.Setup(self => self.SetAsync(It.IsAny<ByteString>(), It.IsAny<FileChunk>()))
                .Callback((ByteString k, FileChunk v) =>
            {
                added[k] = v;
            }).Returns(Task.CompletedTask);

            Ui.Progress p = new();
            System.Threading.Lock @lock = new();

            progress.Setup(self => self.MutateAsync(It.IsAny<ByteString>(), It.IsAny<ProgressCallback>(), It.IsAny<bool>()))
                .Callback((ByteString h, ProgressCallback action, bool _) =>
                {
                    lock (@lock)
                        action(p);
                })
                .Returns(Task.CompletedTask);

            int completed = 0;
            chunks.Setup(self => self.Remove(It.IsAny<ByteString>()))
                .Callback((ByteString k) =>
                {
                    Interlocked.Increment(ref completed);
                    added.TryRemove(k, out _);
                }).Returns(Task.CompletedTask);

            chunks.Setup(self => self.ForEach(It.IsAny<ChunkAction>()))
                .Callback((ChunkAction action) =>
                {
                    foreach (var (k, v) in added)
                    {
                        if (!action(k, v))
                        {
                            break;
                        }
                    }
                }).Returns(Task.CompletedTask);


            int good = 0;
            int canceled = 0;
            // act
            await using (var manager = new DownloadManager("path", 200000, progress.Object, chunks.Object, logger))
            {
                manager.AddChunkUpdateCallback(async (chunk, token) =>
                {
                    Interlocked.Increment(ref good);

                    try
                    {
                        await Task.Delay(downloadTime, token);
                        await manager.UpdateFileProgressAsync(ByteString.Empty, 1);
                        chunk.Status = DownloadStatus.Complete;
                    }
                    catch
                    {
                        Interlocked.Increment(ref canceled);
                        throw;
                    }
                    return chunk;
                });
                await manager.AddNewFileAsync(obj, new Uri(faker.Internet.Url()), faker.System.DirectoryPath());
                using (var source = new CancellationTokenSource(2000))
                {
                    try
                    {
                        await manager.PauseDownloadAsync(obj, source.Token);
                    }
                    catch (OperationCanceledException e)
                    {
                        TestContext.Error.WriteLine(e);
                        throw;
                    }
                }

                using (var source = new CancellationTokenSource(2000))
                {
                    try
                    {
                        await manager.ResumeDownloadAsync(obj, source.Token);
                    }
                    catch (OperationCanceledException e)
                    {
                        TestContext.Error.WriteLine(e);
                        throw;
                    }
                }
            }

            // assert
            progress
                .Verify(self => self.MutateAsync(It.IsAny<ByteString>(), It.IsAny<ProgressCallback>(), It.IsAny<bool>()),
                Times.Exactly(obj.Object.File.Hashes.Hash.Count));

            progress.Verify(self => self.SetAsync(obj.Hash, new() { Current = 0, Total = obj.Object.File.Size }), Times.Once());
            Assert.That(p.Current, Is.EqualTo(obj.Object.File.Hashes.Hash.Count));
            TestContext.Out.WriteLine($"canceled: {canceled}, good {good}, completed {completed}");

            // did we cancel the download after pausing?
            Assert.That(canceled, Is.EqualTo(obj.Object.File.Hashes.Hash.Count));

            // did we start all downloads (after a file was added and after the file download was resumed)?
            Assert.That(good, Is.EqualTo(2 * obj.Object.File.Hashes.Hash.Count));

            // did we complete the download after resuming?
            Assert.That(completed, Is.EqualTo(obj.Object.File.Hashes.Hash.Count));

            // did we clean the mess up?
            Assert.That(added, Has.Count.EqualTo(0));
        }
    }
}
