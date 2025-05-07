using common_test;
using Grpc.Net.Client;
using node;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace integration_tests
{
    public class ProcessContext
    {
        public Process _processN1;
        public Process _processN2;
        public Process _processT;
        public Ui.Ui.UiClient n1Client;
        public Ui.Ui.UiClient n2Client;
        public TrackerWrapper trackerWrapper;
        public Tracker.Tracker.TrackerClient trackerClient;
        public string _tempDirectory;
        public RefWrapper errorsPrinted = new(false);
        public int testPort1 = -1;
        public int testPort2 = -1;
        public int testPort3 = -1;

        public ProcessContext()
        {
            errorsPrinted = new RefWrapper(false);

            _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDirectory);
            TestContext.Out.WriteLine(_tempDirectory);
        }

        public async Task Init()
        {
            // Start processes with unique ports for each test
            testPort1 = FindFreePort();
            testPort2 = FindFreePort();
            testPort3 = FindFreePort();

            _processN1 = StartProcess(1, Node1OutputPath, $"{Guid.NewGuid().ToString()} {0} \"{_tempDirectory}\\n1\" {testPort1}", errorsPrinted);
            //_processN2 = StartProcess(2, Node2OutputPath, $"{Guid.NewGuid().ToString()} {0} \"{_tempDirectory}\\n2\" {testPort2}", errorsPrinted);
            _processT = StartProcess(3, TrackerOutputPath, $"\"{_tempDirectory}\\tracker\" {testPort3}", errorsPrinted);

            await WaitForPortAsync(testPort1);
            //await WaitForPortAsync(testPort2);
            await WaitForPortAsync(testPort3);

            n1Client = new Ui.Ui.UiClient(GrpcChannel.ForAddress(
                new Uri($"http://localhost:{testPort1}")));
            //n2Client = new Ui.Ui.UiClient(GrpcChannel.ForAddress(
            //new Uri($"http://localhost:{testPort2}")));
            trackerClient = new Tracker.Tracker.TrackerClient(GrpcChannel.ForAddress(
                new Uri($"http://localhost:{testPort3}")));
            trackerWrapper = new TrackerWrapper(trackerClient, new Uri($"http://localhost:{testPort3}"));
        }

        public static readonly string Node1OutputPath = Path.GetFullPath(
        Path.Combine(TestContext.CurrentContext.TestDirectory,
        @"..\..\..\..\node\bin\Debug\net9.0-windows7.0\node.exe"));

        public static readonly string Node2OutputPath = Path.GetFullPath(
        Path.Combine(TestContext.CurrentContext.TestDirectory,
        @"..\..\..\..\node\bin\Debug\2-net9.0-windows7.0\node.exe"));

        public static readonly string TrackerOutputPath = Path.GetFullPath(
            Path.Combine(TestContext.CurrentContext.TestDirectory,
            @"..\..\..\..\tracker\bin\Debug\net9.0-windows\tracker.exe"));

        private static int FindFreePort()
        {
            using var listener = new System.Net.Sockets.TcpListener(
                System.Net.IPAddress.Loopback, 0);
            listener.Start();
            int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
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

        public async Task<bool> StopAsync()
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
            return errorsPrinted.Value;
        }
        private static Process StartProcess(int id, string exePath, string arguments, RefWrapper errorsPrinted)
        {
            if (!File.Exists(exePath))
                throw new FileNotFoundException($"Executable not found: {exePath}");

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

    }
}
