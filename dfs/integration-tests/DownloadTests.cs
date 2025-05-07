using common;
using common_test;
using Google.Protobuf;
using Org.BouncyCastle.Utilities.Encoders;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace integration_tests
{
    [TestFixture]
    public class DownloadTests
    {
        static JsonFormatter formatter = new JsonFormatter(JsonFormatter.Settings.Default.WithIndentation());
        private static Bogus.Faker faker = new();
        private static ConcurrentQueue<ProcessContext> contexts = new();
        private static ConcurrentQueue<ProcessContext> disposedContexts = new();

        public DownloadTests()
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
        }

        [OneTimeSetUp]
        public async Task SetUpAsync()
        {
            try
            {
                ProcessHandling.KillSolutionProcesses([ProcessContext.Node1OutputPath, ProcessContext.Node2OutputPath, ProcessContext.TrackerOutputPath]);
            }
            catch (Exception e)
            {
                await TestContext.Out.WriteLineAsync(e.ToString());
            }

            for (int i = 0; i < 11; i++)
            {
                var ctx = new ProcessContext();
                contexts.Enqueue(ctx);
                await ctx.Init();
            }
        }

        [OneTimeTearDown]
        public async Task TearDownAsync()
        {
            bool b = false;
            while (contexts.TryDequeue(out ProcessContext? ctx))
            {
                bool now = await ctx.StopAsync();
                b = now || b;
            }
            while (disposedContexts.TryDequeue(out ProcessContext? ctx))
            {
                bool now = await ctx.StopAsync();
                b = now || b;
            }
            Assert.That(b, Is.False, "errors printed");
        }

        [Test, CancelAfter(40000), NonParallelizable]
        public async Task PublishAndSearchTestAsync(CancellationToken token)
        {
            bool fetched = contexts.TryDequeue(out ProcessContext? ctx);
            Assert.That(ctx, Is.Not.Null);
            Assert.That(fetched, Is.True);
            disposedContexts.Enqueue(ctx);

            var directory = Directory.CreateDirectory(
                Path.Combine(ctx._tempDirectory, "test1"));
            var filePath = Path.Combine(directory.FullName, "test.txt");
            using var file = File.CreateText(filePath);

            int fileSize = 1024 * 1024 * 10;
            await GenerateTestFile(file, fileSize);

            await file.FlushAsync(token);
            await TestContext.Out.WriteLineAsync(ctx._tempDirectory);

            var resp = await ctx.n1Client.ImportObjectToContainerAsync(new() { ChunkSize = (fileSize / 1024) / 3, Path = directory.FullName }, null, null, token);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(resp, Is.Not.Null);
                Assert.That(Guid.TryParse(resp.Guid_, out _), Is.True);
            }

            await ctx.n1Client.PublishToTrackerAsync(new() { ContainerGuid = resp.Guid_, TrackerUri = $"http://localhost:{ctx.testPort3}" }, null, null, token);
            var resp2 = await ctx.trackerWrapper.SearchForObjects("(?s).*", token);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(resp2, Is.Not.Null);
                Assert.That(resp2, Has.Count.EqualTo(2));
                foreach (var a in resp2)
                    Assert.That(a.Guid, Is.EqualTo(resp.Guid_));
                Assert.That(resp2[0].Object.Object.Name, Is.EqualTo("test1"));
                Assert.That(resp2[1].Object.Object.Name, Is.EqualTo("test.txt"));
            }
        }

        [Test, CancelAfter(40000), NonParallelizable]
        public async Task TestRenameAsync([Values] bool renameFolder, CancellationToken token)
        {
            bool fetched = contexts.TryDequeue(out ProcessContext? ctx);
            Assert.That(ctx, Is.Not.Null);
            Assert.That(fetched, Is.True);
            disposedContexts.Enqueue(ctx);

            var directory = Directory.CreateDirectory(
                Path.Combine(ctx._tempDirectory, "test1"));
            var subdir = Directory.CreateDirectory(
                Path.Combine(directory.FullName, "folder"));
            var filePath = Path.Combine(subdir.FullName, "test.txt");
            using var file = File.CreateText(filePath);

            int fileSize = 1024 * 1024 * 1;
            await GenerateTestFile(file, fileSize);

            await file.FlushAsync(token);
            await TestContext.Out.WriteLineAsync(ctx._tempDirectory);

            var resp = await ctx.n1Client.ImportObjectToContainerAsync(new() { ChunkSize = fileSize, Path = directory.FullName }, null, null, token);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(resp, Is.Not.Null);
                Assert.That(Guid.TryParse(resp.Guid_, out _), Is.True);
            }

            var objects = await ctx.n1Client.GetContainerObjectsAsync
                (
                resp,
                cancellationToken: token
                );

            await ctx.n1Client.PublishToTrackerAsync(new() { ContainerGuid = resp.Guid_, TrackerUri = $"http://localhost:{ctx.testPort3}" }, null, null, token);

            int id = renameFolder ? 0 : 1;

            var s = await ctx.n1Client.ApplyFsOperationAsync(new Ui.FsOperation
            {
                ContainerGuid = resp.Guid_,
                Type = Ui.OperationType.Rename,
                Parent = new RpcCommon.Hash { Data = objects.Data[id].Hash },
                Target = new RpcCommon.Hash { Data = objects.Data[id + 1].Hash },
                NewName = "hello.txt",
                TrackerUri = $"http://localhost:{ctx.testPort3}",

            }, cancellationToken: token);

            objects = await ctx.n1Client.GetContainerObjectsAsync
                (
                resp,
                cancellationToken: token
                );

            Assert.That(objects.Data[id + 1].Object.Name == "hello.txt");

            var resp2 = await ctx.trackerWrapper.SearchForObjects("(?s).*", token);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(resp2, Is.Not.Null);
                Assert.That(resp2, Has.Count.EqualTo(objects.Data.Count));
                Assert.That(resp2.Select(a => a.Object), Is.EqualTo(objects.Data));
            }
        }

        [Test, CancelAfter(40000), NonParallelizable]
        public async Task TestMoveAsync([Values] bool moveFolder, CancellationToken token)
        {
            bool fetched = contexts.TryDequeue(out ProcessContext? ctx);
            Assert.That(ctx, Is.Not.Null);
            Assert.That(fetched, Is.True);
            disposedContexts.Enqueue(ctx);

            var directory = Directory.CreateDirectory(
                Path.Combine(ctx._tempDirectory, "test1"));
            Directory.CreateDirectory(
                Path.Combine(directory.FullName, "folder"));
            Directory.CreateDirectory(
                Path.Combine(directory.FullName, "folder", "subfolder"));
            var filePath = Path.Combine(directory.FullName, "folder", "subfolder", "test.txt");
            using var file = File.CreateText(filePath);

            int fileSize = 1024 * 1024 * 1;
            await GenerateTestFile(file, fileSize);

            await file.FlushAsync(token);
            await TestContext.Out.WriteLineAsync(ctx._tempDirectory);

            var resp = await ctx.n1Client.ImportObjectToContainerAsync(new() { ChunkSize = fileSize, Path = directory.FullName }, null, null, token);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(resp, Is.Not.Null);
                Assert.That(Guid.TryParse(resp.Guid_, out _), Is.True);
            }

            var objects = await ctx.n1Client.GetContainerObjectsAsync
                (
                resp,
                cancellationToken: token
                );

            await ctx.n1Client.PublishToTrackerAsync(new() { ContainerGuid = resp.Guid_, TrackerUri = $"http://localhost:{ctx.testPort3}" }, null, null, token);

            int id = moveFolder ? 1 : 2;

            await ctx.n1Client.ApplyFsOperationAsync(new Ui.FsOperation
            {
                ContainerGuid = resp.Guid_,
                Type = Ui.OperationType.Move,
                Parent = new RpcCommon.Hash { Data = objects.Data[id].Hash },
                Target = new RpcCommon.Hash { Data = objects.Data[id + 1].Hash },
                NewParent = new RpcCommon.Hash { Data = objects.Data[0].Hash },
                TrackerUri = $"http://localhost:{ctx.testPort3}",

            }, cancellationToken: token);

            var newObjects = await ctx.n1Client.GetContainerObjectsAsync
                (
                resp,
                cancellationToken: token
                );

            using (Assert.EnterMultipleScope())
            {
                Assert.That(newObjects.Data.Contains(objects.Data[id + 1]));
                Assert.That(!newObjects.Data.Contains(objects.Data[0]));
                Assert.That(!newObjects.Data.Contains(objects.Data[1]));
                if (moveFolder)
                {
                    Assert.That(newObjects.Data.Contains(objects.Data[3]));
                }
                else
                {
                    Assert.That(!newObjects.Data.Contains(objects.Data[2]));
                }
            }

            var resp2 = await ctx.trackerWrapper.SearchForObjects("(?s).*", token);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(resp2, Is.Not.Null);
                Assert.That(resp2, Has.Count.EqualTo(objects.Data.Count));
                Assert.That(resp2.Select(a => a.Object), Is.EqualTo(newObjects.Data));
            }
        }

        [Test, CancelAfter(40000), NonParallelizable, Ignore("flaky")]
        public async Task TestCopyAsync([Values] bool copyFolder, CancellationToken token)
        {
            bool fetched = contexts.TryDequeue(out ProcessContext? ctx);
            Assert.That(ctx, Is.Not.Null);
            Assert.That(fetched, Is.True);
            disposedContexts.Enqueue(ctx);

            var directory = Directory.CreateDirectory(
                Path.Combine(ctx._tempDirectory, "test1"));
            Directory.CreateDirectory(
                Path.Combine(directory.FullName, "folder"));
            Directory.CreateDirectory(
                Path.Combine(directory.FullName, "subfolder"));
            var filePath = Path.Combine(directory.FullName, "folder", "test.txt");
            using var file = File.CreateText(filePath);

            int fileSize = 1024 * 1024 * 1;
            await GenerateTestFile(file, fileSize);

            await file.FlushAsync(token);
            await TestContext.Out.WriteLineAsync(ctx._tempDirectory);

            var resp = await ctx.n1Client.ImportObjectToContainerAsync(new() { ChunkSize = fileSize, Path = directory.FullName }, null, null, token);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(resp, Is.Not.Null);
                Assert.That(Guid.TryParse(resp.Guid_, out _), Is.True);
            }

            var objects = await ctx.n1Client.GetContainerObjectsAsync
                (
                resp,
                cancellationToken: token
                );

            await ctx.n1Client.PublishToTrackerAsync(new() { ContainerGuid = resp.Guid_, TrackerUri = $"http://localhost:{ctx.testPort3}" }, null, null, token);

            int id = copyFolder ? 1 : 2;

            await ctx.n1Client.ApplyFsOperationAsync(new Ui.FsOperation
            {
                ContainerGuid = resp.Guid_,
                Type = Ui.OperationType.Copy,
                Parent = new RpcCommon.Hash { Data = objects.Data[id].Hash },
                Target = new RpcCommon.Hash { Data = objects.Data[id + 1].Hash },
                NewParent = new RpcCommon.Hash { Data = objects.Data[3].Hash },
                TrackerUri = $"http://localhost:{ctx.testPort3}",

            }, cancellationToken: token);

            var newObjects = await ctx.n1Client.GetContainerObjectsAsync
                (
                resp,
                cancellationToken: token
                );

            using (Assert.EnterMultipleScope())
            {
                Assert.That(newObjects.Data.Contains(objects.Data[id + 1]));
                Assert.That(!newObjects.Data.Contains(objects.Data[0]));
                Assert.That(newObjects.Data.Contains(objects.Data[1]));
            }

            var resp2 = await ctx.trackerWrapper.SearchForObjects("(?s).*", token);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(resp2, Is.Not.Null);
                Assert.That(resp2, Has.Count.EqualTo(objects.Data.Count));
                Assert.That(resp2.Select(a => a.Object), Is.EqualTo(newObjects.Data));
            }
        }

        [Test, CancelAfter(40000), NonParallelizable]
        public async Task TestDeleteAsync([Values] bool deleteFolder, CancellationToken token)
        {
            bool fetched = contexts.TryDequeue(out ProcessContext? ctx);
            Assert.That(ctx, Is.Not.Null);
            Assert.That(fetched, Is.True);
            disposedContexts.Enqueue(ctx);

            var directory = Directory.CreateDirectory(
                Path.Combine(ctx._tempDirectory, "test1"));
            var subdir = Directory.CreateDirectory(
                Path.Combine(directory.FullName, "folder"));
            var filePath = Path.Combine(subdir.FullName, "test.txt");
            using var file = File.CreateText(filePath);

            int fileSize = 1024 * 1024 * 1;
            await GenerateTestFile(file, fileSize);

            await file.FlushAsync(token);
            await TestContext.Out.WriteLineAsync(ctx._tempDirectory);

            var resp = await ctx.n1Client.ImportObjectToContainerAsync(new() { ChunkSize = fileSize, Path = directory.FullName }, null, null, token);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(resp, Is.Not.Null);
                Assert.That(Guid.TryParse(resp.Guid_, out _), Is.True);
            }

            var objects = await ctx.n1Client.GetContainerObjectsAsync
                (
                resp,
                cancellationToken: token
                );
            var initial = objects.Data.Count - (deleteFolder ? 2 : 1);

            await ctx.n1Client.PublishToTrackerAsync(new() { ContainerGuid = resp.Guid_, TrackerUri = $"http://localhost:{ctx.testPort3}" }, null, null, token);

            int id = deleteFolder ? 0 : 1;

            var s = await ctx.n1Client.ApplyFsOperationAsync(new Ui.FsOperation
            {
                ContainerGuid = resp.Guid_,
                Type = Ui.OperationType.Delete,
                Parent = new RpcCommon.Hash { Data = objects.Data[id].Hash },
                Target = new RpcCommon.Hash { Data = objects.Data[id + 1].Hash },
                TrackerUri = $"http://localhost:{ctx.testPort3}",

            }, cancellationToken: token);

            objects = await ctx.n1Client.GetContainerObjectsAsync
                (
                resp,
                cancellationToken: token
                );

            var resp2 = await ctx.trackerWrapper.SearchForObjects("(?s).*", token);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(resp2, Is.Not.Null);
                Assert.That(resp2, Has.Count.EqualTo(initial));
                Assert.That(resp2.Select(a => a.Object), Is.EqualTo(objects.Data));
            }
        }

        private static async Task GenerateTestFile(StreamWriter file, int count = 1048576)
        {
            for (int i = 0; i < count; i++)
                await file.WriteLineAsync(faker.Random.AlphaNumeric(2));
        }

        [Test, CancelAfter(60000), NonParallelizable]
        public async Task TestDownloadAsync(CancellationToken token)
        {
            bool fetched = contexts.TryDequeue(out ProcessContext? ctx);
            Assert.That(ctx, Is.Not.Null);
            Assert.That(fetched, Is.True);
            disposedContexts.Enqueue(ctx);

            var directory = Directory.CreateDirectory(
                Path.Combine(ctx._tempDirectory, "test1"));
            var filePath = Path.Combine(directory.FullName, "test.txt");
            using var file = File.CreateText(filePath);

            int fileSize = 1024 * 1024 * 20;
            await GenerateTestFile(file, fileSize);
            await TestContext.Out.WriteLineAsync($"File generated {new FileInfo(filePath).Length} bytes");

            await file.FlushAsync(token);
            await TestContext.Out.WriteLineAsync(ctx._tempDirectory);

            var resp = await ctx.n1Client.ImportObjectToContainerAsync(new() { ChunkSize = 1024 * 1024, Path = directory.FullName }, cancellationToken: token);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(resp, Is.Not.Null);
                Assert.That(Guid.TryParse(resp.Guid_, out _), Is.True);
            }
            var parts = await ctx.n1Client.GetContainerObjectsAsync(resp, cancellationToken: token);

            await ctx.n1Client.PublishToTrackerAsync(new() { ContainerGuid = resp.Guid_, TrackerUri = $"http://localhost:{ctx.testPort3}" }, cancellationToken: token);
            Directory.CreateDirectory(Path.Combine(ctx._tempDirectory, "output"));

            var timer = new Stopwatch();
            timer.Start();
            var res2 = await ctx.n2Client.DownloadContainerAsync(new()
            { ContainerGuid = resp.Guid_, DestinationDir = Path.Combine(ctx._tempDirectory, "output"), MaxConcurrentChunks = 20, TrackerUri = ctx.trackerWrapper.GetUri().ToString() },
            cancellationToken: token);

            var progress = new Ui.Progress();
            int delay = 50000;
            do
            {
                delay -= 500;
                if (delay <= 0)
                {
                    break;
                }
                await Task.Delay(2000, token);
                progress = await ctx.n2Client.GetDownloadProgressAsync(new() { Data = parts.Data[1].Hash }, cancellationToken: token);
                await TestContext.Out.WriteLineAsync($"{progress.Current} {progress.Total}");
            } while (progress.Current != progress.Total);
            timer.Stop();

            double mbps = (double)(progress.Total / 131072) / timer.Elapsed.TotalSeconds;
            TestContext.Out.WriteLine($"got {progress.Total / 1048576.0} MB, time {timer.Elapsed.TotalSeconds,5}s, download speed: {mbps,5} Mbps");

            var outputPath = Path.Combine(ctx._tempDirectory, "output", Hex.ToHexString(parts.Data[1].Hash.ToByteArray()), "test.txt");
            var inputPath = Path.Combine(ctx._tempDirectory, "test1", "test.txt");

            var expected = await GetFileContents(inputPath);
            var actual = await GetFileContents(outputPath);
            Assert.That(actual, Is.EqualTo(expected), "file contents aren't equal");
        }

        [Test, CancelAfter(60000), NonParallelizable]
        public async Task TestDownloadWithPauseResumeAsync(CancellationToken token)
        {
            bool fetched = contexts.TryDequeue(out ProcessContext? ctx);
            Assert.That(ctx, Is.Not.Null);
            Assert.That(fetched, Is.True);
            disposedContexts.Enqueue(ctx);

            var directory = Directory.CreateDirectory(
                Path.Combine(ctx._tempDirectory, "test1"));
            using var file = File.CreateText(Path.Combine(directory.FullName, "test.txt"));

            int fileSize = 1024 * 1024 * 4;
            await GenerateTestFile(file, fileSize);

            await file.FlushAsync(token);
            await TestContext.Out.WriteLineAsync(ctx._tempDirectory);

            var resp = await ctx.n1Client.ImportObjectToContainerAsync(new() { ChunkSize = fileSize / 1024, Path = directory.FullName }, cancellationToken: token);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(resp, Is.Not.Null);
                Assert.That(Guid.TryParse(resp.Guid_, out _), Is.True);
            }
            var parts = await ctx.n1Client.GetContainerObjectsAsync(resp, cancellationToken: token);

            await ctx.n1Client.PublishToTrackerAsync(new() { ContainerGuid = resp.Guid_, TrackerUri = $"http://localhost:{ctx.testPort3}" }, cancellationToken: token);
            Directory.CreateDirectory(Path.Combine(ctx._tempDirectory, "output"));

            var res2 = await ctx.n2Client.DownloadContainerAsync(new()
            { ContainerGuid = resp.Guid_, DestinationDir = Path.Combine(ctx._tempDirectory, "output"), MaxConcurrentChunks = 20, TrackerUri = ctx.trackerWrapper.GetUri().ToString() }, cancellationToken: token);

            var progress = new Ui.Progress();
            int delay = 50000;
            do
            {
                delay -= 500;
                if (delay <= 0)
                {
                    Assert.Fail("Download timed out");
                }
                await Task.Delay(5000, token);
                await ctx.n2Client.PauseFileDownloadAsync(new() { Data = parts.Data[1].Hash }, cancellationToken: token);
                await ctx.n2Client.ResumeFileDownloadAsync(new() { Data = parts.Data[1].Hash }, cancellationToken: token);
                progress = await ctx.n2Client.GetDownloadProgressAsync(new() { Data = parts.Data[1].Hash }, cancellationToken: token);
                await TestContext.Out.WriteLineAsync($"{progress.Current} {progress.Total}");
            } while (progress.Current != progress.Total);

            var outputPath = Path.Combine(ctx._tempDirectory, "output", Hex.ToHexString(parts.Data[1].Hash.ToByteArray()), "test.txt");
            var inputPath = Path.Combine(ctx._tempDirectory, "test1", "test.txt");

            var expected = await GetFileContents(inputPath);
            var actual = await GetFileContents(outputPath);
            Assert.That(actual, Is.EqualTo(expected), "file contents aren't equal");
        }

        private static async Task<byte[]> GetFileContents(string path)
        {
            var info = new FileInfo(path);
            using var stream = new FileStream
                    (
                        path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite,
                        bufferSize: 4096,
                        FileOptions.Asynchronous |
                        FileOptions.WriteThrough
                    );

            var buffer = new byte[info.Length];
            await RandomAccess.ReadAsync(stream.SafeFileHandle, buffer, 0);
            return buffer;
        }
    }
}
