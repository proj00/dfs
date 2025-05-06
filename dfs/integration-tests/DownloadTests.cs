using common_test;
using Google.Protobuf;
using Grpc.Net.Client;
using node;
using Org.BouncyCastle.Utilities.Encoders;
using System.Collections;
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
    public class DownloadTests
    {
        private static Bogus.Faker faker = new();
        private Process _processN1;
        private Process _processN2;
        private Process _processT;
        private Ui.Ui.UiClient n1Client;
        private Ui.Ui.UiClient n2Client;
        private TrackerWrapper trackerWrapper;
        private Tracker.Tracker.TrackerClient trackerClient;
        private string _tempDirectory;
        private RefWrapper errorsPrinted = new(false);
        int testPort1 = -1;
        int testPort2 = -1;
        int testPort3 = -1;

        public DownloadTests()
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
        }
        class RefWrapper
        {
            public bool Value { get; set; }
            public RefWrapper(bool value) { Value = value; }
        }

        private static readonly string Node1OutputPath = Path.GetFullPath(
        Path.Combine(TestContext.CurrentContext.TestDirectory,
        @"..\..\..\..\node\bin\Debug\net9.0-windows7.0\node.exe"));

        private static readonly string Node2OutputPath = Path.GetFullPath(
        Path.Combine(TestContext.CurrentContext.TestDirectory,
        @"..\..\..\..\node\bin\Debug\2-net9.0-windows7.0\node.exe"));

        private static readonly string TrackerOutputPath = Path.GetFullPath(
            Path.Combine(TestContext.CurrentContext.TestDirectory,
            @"..\..\..\..\tracker\bin\Debug\net9.0-windows\tracker.exe"));

        [SetUp]
        public async Task SetUpAsync()
        {
            errorsPrinted = new RefWrapper(false);
            try
            {
                ProcessHandling.KillSolutionProcesses([Node1OutputPath, Node2OutputPath, TrackerOutputPath]);
            }
            catch (Exception e)
            {
                await TestContext.Out.WriteLineAsync(e.ToString());
            }
            _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDirectory);
            await TestContext.Out.WriteLineAsync(_tempDirectory);

            // Start processes with unique ports for each test
            testPort1 = FindFreePort();
            testPort2 = FindFreePort();
            testPort3 = FindFreePort();

            _processN1 = StartProcess(1, Node1OutputPath, $"{Guid.NewGuid().ToString()} {0} \"{_tempDirectory}\\n1\" {testPort1}", errorsPrinted);
            _processN2 = StartProcess(2, Node2OutputPath, $"{Guid.NewGuid().ToString()} {0} \"{_tempDirectory}\\n2\" {testPort2}", errorsPrinted);
            _processT = StartProcess(3, TrackerOutputPath, $"\"{_tempDirectory}\\tracker\" {testPort3}", errorsPrinted);

            await WaitForPortAsync(testPort1);
            await WaitForPortAsync(testPort2);
            await WaitForPortAsync(testPort3);

            n1Client = new Ui.Ui.UiClient(GrpcChannel.ForAddress(
                new Uri($"http://localhost:{testPort1}")));
            n2Client = new Ui.Ui.UiClient(GrpcChannel.ForAddress(
                new Uri($"http://localhost:{testPort2}")));
            trackerClient = new Tracker.Tracker.TrackerClient(GrpcChannel.ForAddress(
                new Uri($"http://localhost:{testPort3}")));
            trackerWrapper = new TrackerWrapper(trackerClient, new Uri($"http://localhost:{testPort3}"));
        }

        private static async Task WaitForPortAsync(int port, int timeoutMs = 40000)
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
        public async Task TearDownAsync()
        {
            testPort1 = -1;
            testPort2 = -1;
            testPort3 = -1;

            try
            {
                await n1Client.ShutdownAsync(new RpcCommon.Empty());
                await n2Client.ShutdownAsync(new RpcCommon.Empty());
                await trackerClient.ShutdownAsync(new RpcCommon.Empty());
                await Task.Delay(3000);
                ProcessHandling.KillSolutionProcesses([Node1OutputPath, Node2OutputPath, TrackerOutputPath]);
            }
            catch { }
            _processN1?.Dispose();
            _processN2?.Dispose();
            _processT?.Dispose();
            try
            {
                ProcessHandling.KillSolutionProcesses([Node1OutputPath, Node2OutputPath, TrackerOutputPath]);
                if (Directory.Exists(_tempDirectory) && !errorsPrinted.Value && !Debugger.IsAttached)
                {
                    //Directory.Delete(_tempDirectory, recursive: true);
                }
            }
            catch { /* Ignore cleanup errors */ }
            var b = errorsPrinted.Value;
            Assert.That(b, Is.False, "errors printed");
        }

        private Process StartProcess(int id, string exePath, string arguments, RefWrapper errorsPrinted)
        {
            if (!File.Exists(exePath))
                throw new FileNotFoundException($"Executable not found: {exePath}");

            var guid = Guid.NewGuid().ToString("N");
            var covDir = Path.Combine("..", "..", "..", "cov");
            var info = Directory.CreateDirectory(covDir);
            var coverageOutput = Path.Combine(covDir, $"{id}-{guid}-coverage.opencover.xml");

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardErrorEncoding = Encoding.UTF8,
                StandardOutputEncoding = Encoding.UTF8
            };

            var process = new Process { StartInfo = psi };
            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                    TestContext.Out.WriteLine(args.Data);
            };
            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    TestContext.Error.WriteLine(args.Data);
                    errorsPrinted.Value = true;
                }
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

        [Test, CancelAfter(50000)]
        public async Task PublishAndSearchTestAsync(CancellationToken token)
        {
            var directory = Directory.CreateDirectory(
                Path.Combine(_tempDirectory, "test1"));
            var filePath = Path.Combine(directory.FullName, "test.txt");
            using var file = File.CreateText(filePath);

            int fileSize = 1024 * 1024 * 10;
            await GenerateTestFile(file, fileSize);

            await file.FlushAsync(token);
            await TestContext.Out.WriteLineAsync(_tempDirectory);

            var resp = await n1Client.ImportObjectToContainerAsync(new() { ChunkSize = (fileSize / 1024) / 3, Path = directory.FullName }, null, null, token);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(resp, Is.Not.Null);
                Assert.That(Guid.TryParse(resp.Guid_, out _), Is.True);
            }

            await n1Client.PublishToTrackerAsync(new() { ContainerGuid = resp.Guid_, TrackerUri = $"http://localhost:{testPort3}" }, null, null, token);
            var resp2 = await trackerWrapper.SearchForObjects("(?s).*", token);
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

        [Test, CancelAfter(60000)]
        public async Task TestDownloadAsync(CancellationToken token)
        {
            var directory = Directory.CreateDirectory(
                Path.Combine(_tempDirectory, "test1"));
            var filePath = Path.Combine(directory.FullName, "test.txt");
            using var file = File.CreateText(filePath);

            int fileSize = 1024 * 1024 * 20;
            await GenerateTestFile(file, fileSize);
            await TestContext.Out.WriteLineAsync($"File generated {new FileInfo(filePath).Length} bytes");

            await file.FlushAsync(token);
            await TestContext.Out.WriteLineAsync(_tempDirectory);

            var resp = await n1Client.ImportObjectToContainerAsync(new() { ChunkSize = 1024 * 1024, Path = directory.FullName }, cancellationToken: token);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(resp, Is.Not.Null);
                Assert.That(Guid.TryParse(resp.Guid_, out _), Is.True);
            }
            var parts = await n1Client.GetContainerObjectsAsync(resp, cancellationToken: token);

            await n1Client.PublishToTrackerAsync(new() { ContainerGuid = resp.Guid_, TrackerUri = $"http://localhost:{testPort3}" }, cancellationToken: token);
            Directory.CreateDirectory(Path.Combine(_tempDirectory, "output"));

            var res2 = await n2Client.DownloadContainerAsync(new()
            { ContainerGuid = resp.Guid_, DestinationDir = Path.Combine(_tempDirectory, "output"), MaxConcurrentChunks = 20, TrackerUri = trackerWrapper.GetUri().ToString() },
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
                progress = await n2Client.GetDownloadProgressAsync(new() { Data = parts.Data[1].Hash }, cancellationToken: token);
                await TestContext.Out.WriteLineAsync($"{progress.Current} {progress.Total}");
            } while (progress.Current != progress.Total);

            var outputPath = Path.Combine(_tempDirectory, "output", Hex.ToHexString(parts.Data[1].Hash.ToByteArray()), "test.txt");
            var inputPath = Path.Combine(_tempDirectory, "test1", "test.txt");

            var expected = await GetFileContents(inputPath);
            var actual = await GetFileContents(outputPath);
            Assert.That(actual, Is.EqualTo(expected), "file contents aren't equal");
        }

        [Test, CancelAfter(80000)]
        public async Task TestDownloadWithPauseResumeAsync(CancellationToken token)
        {
            var directory = Directory.CreateDirectory(
                Path.Combine(_tempDirectory, "test1"));
            using var file = File.CreateText(Path.Combine(directory.FullName, "test.txt"));

            int fileSize = 1024 * 1024 * 4;
            await GenerateTestFile(file, fileSize);

            await file.FlushAsync(token);
            await TestContext.Out.WriteLineAsync(_tempDirectory);

            var resp = await n1Client.ImportObjectToContainerAsync(new() { ChunkSize = fileSize / 1024, Path = directory.FullName }, cancellationToken: token);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(resp, Is.Not.Null);
                Assert.That(Guid.TryParse(resp.Guid_, out _), Is.True);
            }
            var parts = await n1Client.GetContainerObjectsAsync(resp, cancellationToken: token);

            await n1Client.PublishToTrackerAsync(new() { ContainerGuid = resp.Guid_, TrackerUri = $"http://localhost:{testPort3}" }, cancellationToken: token);
            Directory.CreateDirectory(Path.Combine(_tempDirectory, "output"));

            var res2 = await n2Client.DownloadContainerAsync(new()
            { ContainerGuid = resp.Guid_, DestinationDir = Path.Combine(_tempDirectory, "output"), MaxConcurrentChunks = 20, TrackerUri = trackerWrapper.GetUri().ToString() }, cancellationToken: token);

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
                await n2Client.PauseFileDownloadAsync(new() { Data = parts.Data[1].Hash }, cancellationToken: token);
                await n2Client.ResumeFileDownloadAsync(new() { Data = parts.Data[1].Hash }, cancellationToken: token);
                progress = await n2Client.GetDownloadProgressAsync(new() { Data = parts.Data[1].Hash }, cancellationToken: token);
                await TestContext.Out.WriteLineAsync($"{progress.Current} {progress.Total}");
            } while (progress.Current != progress.Total);

            var outputPath = Path.Combine(_tempDirectory, "output", Hex.ToHexString(parts.Data[1].Hash.ToByteArray()), "test.txt");
            var inputPath = Path.Combine(_tempDirectory, "test1", "test.txt");

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
