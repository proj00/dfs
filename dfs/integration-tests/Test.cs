using common_test;
using Google.Protobuf;
using Grpc.Net.Client;
using node;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace integration_tests
{
    [TestFixture]
    public class Tests
    {
        private Process _processN1;
        private Process _processN2;
        private Process _processT;
        private Ui.Ui.UiClient n1Client;
        private Ui.Ui.UiClient n2Client;
        private TrackerWrapper trackerClient;
        private string _tempDirectory;
        private int testPort;
        private RefWrapper<bool> errorsPrinted = new RefWrapper<bool>(false);

        public Tests()
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
        }
        class RefWrapper<T>
        {
            public T Value { get; set; }
            public RefWrapper(T value) => Value = value;
        }

        private static readonly string NodeOutputPath = Path.GetFullPath(
        Path.Combine(TestContext.CurrentContext.TestDirectory,
        @"..\..\..\..\node\bin\Debug\net9.0-windows7.0\node.exe"));

        private static readonly string TrackerOutputPath = Path.GetFullPath(
            Path.Combine(TestContext.CurrentContext.TestDirectory,
            @"..\..\..\..\tracker\bin\Debug\net9.0-windows\tracker.exe"));

        [SetUp]
        public async Task SetUp()
        {
            errorsPrinted = new RefWrapper<bool>(false);
            try
            {
                ProcessHandling.KillSolutionProcesses([NodeOutputPath, TrackerOutputPath]);
            }
            catch (Exception e)
            {
                TestContext.Out.WriteLine(e.ToString());
            }
            _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDirectory);
            TestContext.Out.WriteLine(_tempDirectory);

            // Start processes with unique ports for each test
            testPort = FindFreePort();

            _processN1 = StartProcess(NodeOutputPath, $"{Guid.NewGuid().ToString()} {0} \"{_tempDirectory}\\n1\" {testPort}", errorsPrinted);
            _processN2 = StartProcess(NodeOutputPath, $"{Guid.NewGuid().ToString()} {0} \"{_tempDirectory}\\n2\" {testPort + 1}", errorsPrinted);
            _processT = StartProcess(TrackerOutputPath, $"\"{_tempDirectory}\\tracker\" {testPort + 2}", errorsPrinted);

            await WaitForPortAsync(testPort);
            await WaitForPortAsync(testPort + 1);
            await WaitForPortAsync(testPort + 2);

            n1Client = new Ui.Ui.UiClient(GrpcChannel.ForAddress(
                new Uri($"http://localhost:{testPort}")));
            n2Client = new Ui.Ui.UiClient(GrpcChannel.ForAddress(
                new Uri($"http://localhost:{testPort + 1}")));
            trackerClient = new TrackerWrapper(new Tracker.Tracker.TrackerClient(GrpcChannel.ForAddress(
                new Uri($"http://localhost:{testPort + 2}"))), $"http://localhost:{testPort + 2}");
        }

        private static async Task WaitForPortAsync(int port, int timeoutMs = 10000)
        {
            var stopWatch = Stopwatch.StartNew();
            while (stopWatch.ElapsedMilliseconds < timeoutMs)
            {
                try
                {
                    using var client = new TcpClient();
                    await client.ConnectAsync(IPAddress.Loopback, port);
                    return;
                }
                catch
                {
                    await Task.Delay(250);
                }
            }
            throw new TimeoutException($"Port {port} did not open in time.");
        }

        [TearDown]
        public async Task TearDown()
        {
            testPort = -1;
            try
            {
                await n1Client.ShutdownAsync(new RpcCommon.Empty());
                await n2Client.ShutdownAsync(new RpcCommon.Empty());
                Thread.Sleep(2000);
                ProcessHandling.KillSolutionProcesses([NodeOutputPath, TrackerOutputPath]);
            }
            catch { }
            _processN1?.Dispose();
            _processN2?.Dispose();
            _processT?.Dispose();
            try
            {
                ProcessHandling.KillSolutionProcesses([NodeOutputPath, TrackerOutputPath]);
                if (Directory.Exists(_tempDirectory) && !errorsPrinted.Value && !Debugger.IsAttached)
                {
                    Directory.Delete(_tempDirectory, recursive: true);
                }
            }
            catch { /* Ignore cleanup errors */ }
            var b = errorsPrinted.Value;
            errorsPrinted = new RefWrapper<bool>(false);
            Assert.That(b, Is.False, "errors printed");
        }

        private static Process StartProcess(string exePath, string arguments, RefWrapper<bool> errorsPrinted)
        {
            if (!File.Exists(exePath))
                throw new FileNotFoundException($"Executable not found: {exePath}");
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardErrorEncoding = Encoding.UTF8,
                    StandardOutputEncoding = Encoding.UTF8
                }
            };

            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data?.StartsWith("fail") ?? false)
                {
                    TestContext.Error.WriteLine(args.Data + "(DEBUG AND CHECK FULL)");
                    errorsPrinted.Value = true;

                }
                if (Debugger.IsAttached)
                {
                    if (args.Data == null) return;
                    else TestContext.Out.WriteLine(args.Data);
                }
            };

            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data == null) return;
                else { TestContext.Error.WriteLine(args.Data); errorsPrinted.Value = true; }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return process;
        }

        private static int FindFreePort()
        {
            using var listener = new System.Net.Sockets.TcpListener(
                System.Net.IPAddress.Loopback, 0);
            listener.Start();
            int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        [Test]
        public async Task PublishAndSearchTest()
        {
            var directory = Directory.CreateDirectory(
                Path.Combine(_tempDirectory, "test1"));
            using var file = File.CreateText(Path.Combine(directory.FullName, "test.txt"));
            for (int i = 0; i < 1024; i++)
            {
                await file.WriteLineAsync("hello world");
            }

            await file.FlushAsync();
            await TestContext.Out.WriteLineAsync(_tempDirectory);

            var resp = await n1Client.ImportObjectFromDiskAsync(new() { ChunkSize = 1024, Path = directory.FullName });
            Assert.That(resp, Is.Not.Null);
            Assert.That(Guid.TryParse(resp.Guid_, out _), Is.True);

            await n1Client.PublishToTrackerAsync(new() { ContainerGuid = resp.Guid_, TrackerUri = $"http://localhost:{testPort + 2}" });
            var resp2 = await trackerClient.SearchForObjects("(?s).*");
            Assert.That(resp2, Is.Not.Null);
            Assert.That(resp2.Count, Is.EqualTo(2));
            foreach (var a in resp2)
                Assert.That(a.Guid, Is.EqualTo(resp.Guid_));
            Assert.That(resp2[0].Object.Object.Name, Is.EqualTo("test1"));
            Assert.That(resp2[1].Object.Object.Name, Is.EqualTo("test.txt"));
        }

        [Test]
        public async Task TestDownload()
        {
            var directory = Directory.CreateDirectory(
                Path.Combine(_tempDirectory, "test1"));
            using var file = File.CreateText(Path.Combine(directory.FullName, "test.txt"));
            for (int i = 0; i < 1024; i++)
            {
                await file.WriteLineAsync("hello world");
            }
            await file.FlushAsync();
            await TestContext.Out.WriteLineAsync(_tempDirectory);

            var resp = await n1Client.ImportObjectFromDiskAsync(new() { ChunkSize = 1024, Path = directory.FullName });
            Assert.That(resp, Is.Not.Null);
            Assert.That(Guid.TryParse(resp.Guid_, out _), Is.True);
            var parts = await n1Client.GetContainerObjectsAsync(resp);

            await n1Client.PublishToTrackerAsync(new() { ContainerGuid = resp.Guid_, TrackerUri = $"http://localhost:{testPort + 2}" });
            Directory.CreateDirectory(Path.Combine(_tempDirectory, "output"));

            var res2 = await n2Client.DownloadContainerAsync(new()
            { ContainerGuid = resp.Guid_, DestinationDir = Path.Combine(_tempDirectory, "output"), MaxConcurrentChunks = 20, TrackerUri = trackerClient.GetUri() });

            var progress = new Ui.Progress();
            int delay = 50000;
            do
            {
                delay -= 500;
                if (delay <= 0)
                {
                    Assert.Fail("Download timed out");
                }
                await Task.Delay(2000);
                progress = await n2Client.GetDownloadProgressAsync(new() { Data = parts.Data[1].Hash });
                await TestContext.Out.WriteLineAsync($"{progress.Current} {progress.Total}");
            } while (progress.Current != progress.Total);
        }
        [Test]
        public async Task TestDownloadWithPauseResume()
        {
            var directory = Directory.CreateDirectory(
                Path.Combine(_tempDirectory, "test1"));
            using var file = File.CreateText(Path.Combine(directory.FullName, "test.txt"));
            for (int i = 0; i < 1024; i++)
            {
                await file.WriteLineAsync("hello world");
            }
            await file.FlushAsync();
            await TestContext.Out.WriteLineAsync(_tempDirectory);

            var resp = await n1Client.ImportObjectFromDiskAsync(new() { ChunkSize = 1024, Path = directory.FullName });
            Assert.That(resp, Is.Not.Null);
            Assert.That(Guid.TryParse(resp.Guid_, out _), Is.True);
            var parts = await n1Client.GetContainerObjectsAsync(resp);

            await n1Client.PublishToTrackerAsync(new() { ContainerGuid = resp.Guid_, TrackerUri = $"http://localhost:{testPort + 2}" });
            Directory.CreateDirectory(Path.Combine(_tempDirectory, "output"));

            var res2 = await n2Client.DownloadContainerAsync(new()
            { ContainerGuid = resp.Guid_, DestinationDir = Path.Combine(_tempDirectory, "output"), MaxConcurrentChunks = 20, TrackerUri = trackerClient.GetUri() });

            var progress = new Ui.Progress();
            int delay = 50000;
            do
            {
                delay -= 500;
                if (delay <= 0)
                {
                    Assert.Fail("Download timed out");
                }
                await Task.Delay(2000);
                await n2Client.PauseFileDownloadAsync(new() { Data = parts.Data[1].Hash });
                await n2Client.ResumeFileDownloadAsync(new() { Data = parts.Data[1].Hash });
                progress = await n2Client.GetDownloadProgressAsync(new() { Data = parts.Data[1].Hash });
                await TestContext.Out.WriteLineAsync($"{progress.Current} {progress.Total}");
            } while (progress.Current != progress.Total);
        }
    }
}
