using common_test;
using Google.Protobuf;
using Org.BouncyCastle.Utilities.Encoders;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace integration_tests
{
    [TestFixture]
    public class DownloadTests
    {
        private static Bogus.Faker faker = new();
        private static ConcurrentDictionary<int, ProcessContext> contexts = new();

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

            for (int i = 0; i < 3; i++)
            {
                contexts[i] = new();
                await contexts[i].Init();
            }
        }

        [OneTimeTearDown]
        public async Task TearDownAsync()
        {
            bool b = false;
            foreach (var (k, v) in contexts)
            {
                bool now = await v.StopAsync();
                b = now || b;
            }
            Assert.That(b, Is.False, "errors printed");
        }


        [Test, CancelAfter(40000)]
        public async Task PublishAndSearchTestAsync(CancellationToken token)
        {
            var ctx = contexts[0];
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

        private static async Task GenerateTestFile(StreamWriter file, int count = 1048576)
        {
            for (int i = 0; i < count; i++)
                await file.WriteLineAsync("hi");
        }

        [Test, CancelAfter(40000)]
        public async Task TestDownloadAsync(CancellationToken token)
        {
            var ctx = contexts[1];

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

            var outputPath = Path.Combine(ctx._tempDirectory, "output", Hex.ToHexString(parts.Data[1].Hash.ToByteArray()), "test.txt");
            var inputPath = Path.Combine(ctx._tempDirectory, "test1", "test.txt");

            var expected = await GetFileContents(inputPath);
            var actual = await GetFileContents(outputPath);
            Assert.That(actual, Is.EqualTo(expected), "file contents aren't equal");
        }

        [Test, CancelAfter(40000)]
        public async Task TestDownloadWithPauseResumeAsync(CancellationToken token)
        {
            var ctx = contexts[2];

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
