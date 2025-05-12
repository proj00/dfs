using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using common;
using Microsoft.VisualStudio.Threading;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net;
using System.Diagnostics;
using System.IO;

namespace node
{

    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        static async Task Main(string[] args)
        {
#if DEBUG
            LogLevel level = LogLevel.Debug;
            string IPstring = "localhost";
#else
            string IPstring = GetLocalIPv4() ?? "localhost";
            LogLevel level = LogLevel.Information;
#endif
            AppDomain.CurrentDomain.SetData("REGEX_DEFAULT_MATCH_TIMEOUT", TimeSpan.FromMilliseconds(100));
            (string logPath, ILoggerFactory loggerFactory) = InternalLogger.CreateLoggerFactory(args.Length >= 3 ? args[2] + "\\logs" : "logs", level);

            ILogger logger = loggerFactory.CreateLogger("Main");

            Guid pipeGuid = new();
            uint parentPid = 0;

            if (!(args.Length >= 2 && Guid.TryParse(args[0], out pipeGuid) && uint.TryParse(args[1], out parentPid)))
            {
                logger.LogError("Please provide a pipe GUID and a PID as the first argument.");
                return;
            }

            using NodeState state = new(TimeSpan.FromMinutes(1), loggerFactory, logPath, args.Length >= 3 ? args[2] : Path.Combine("./db", Guid.NewGuid().ToString()));

            int debugPort = args.Length >= 4 ? int.Parse(args[3]) : 42069;

            NodeRpc rpc = new(state);
            var publicServer = await StartPublicNodeServerAsync(rpc, loggerFactory);
            var publicUrl = new Uri(publicServer.Urls.First());

            Uri nodeURI = new($"http://{IPstring}:{publicUrl.Port}");
            state.Downloads.AddChunkUpdateCallback((chunk, token) => state.Objects.DownloadChunkAsync(chunk, nodeURI, token));
            UiService service = new(state, nodeURI);

            var pipeStreams = new ConcurrentDictionary<string, NamedPipeServerStream>();
            using CancellationTokenSource token = new();
            var privateServer = await StartGrpcWebServerAsync(service, pipeGuid, parentPid, pipeStreams, loggerFactory, token.Token, debugPort);

            await service.ShutdownEvent.WaitAsync();
            _ = token.CancelAsync();
            await publicServer.StopAsync();
            await privateServer.StopAsync();
        }

        public static string? GetLocalIPv4()
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up ||
                    ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                var ipProps = ni.GetIPProperties();
                foreach (UnicastIPAddressInformation ip in ipProps.UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(ip.Address))
                    {
                        return ip.Address.ToString();
                    }
                }
            }
            return null;
        }

        private static async Task<WebApplication> StartPublicNodeServerAsync(NodeRpc rpc, ILoggerFactory loggerFactory)
        {
            var builder = WebApplication.CreateBuilder();

            // Define CORS policy
            string policyName = "AllowAll";
            builder.Services.AddGrpc();
            builder.Services.AddCors(o => o.AddPolicy(policyName, policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding");
            }));

            builder.Services.AddSingleton(rpc);

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(0, o =>
                {
                    o.Protocols = HttpProtocols.Http2;
                });
            });

            var app = builder.Build();
            IWebHostEnvironment env = app.Environment;

            app.UseRouting();
            app.UseCors(policyName);

            app.MapGrpcService<NodeRpc>().RequireCors(policyName);

            await app.StartAsync();

            return app;
        }

        private static async Task<WebApplication> StartGrpcWebServerAsync(UiService service, Guid pipeGuid, uint parentPid,
    ConcurrentDictionary<string, NamedPipeServerStream> pipeStreams, ILoggerFactory loggerFactory, CancellationToken cancellationToken, int debugPort)
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.Services.AddGrpc();

            const string policyName = "AllowLocal";
            builder.Services.AddCors(o => o.AddPolicy(policyName, policy =>
            {
                policy.SetIsOriginAllowed(origin => new Uri(origin).IsLoopback)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding");
            }));

            builder.Services.AddSingleton(loggerFactory);
            builder.Services.AddLogging();
            builder.Services.AddSingleton(service);
#if DEBUG
            builder.Services.AddGrpcReflection();
#endif
            builder.WebHost.UseNamedPipes(options =>
            {
                options.CreateNamedPipeServerStream = (context) =>
                {
                    var stream = new NamedPipeServerStream(
                        context.NamedPipeEndPoint.PipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        System.IO.Pipes.PipeOptions.Asynchronous);

                    var connectionId = Guid.NewGuid().ToString();
                    pipeStreams[connectionId] = stream;

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            try
                            {
                                await stream.WaitForConnectionAsync(cancellationToken);
                            }
                            catch (OperationCanceledException)
                            {
                                return; // Exit if operation was cancelled
                            }

                            if (GetNamedPipeClientProcessId(stream.SafePipeHandle, out var clientPid))
                            {
#if !DEBUG
                        if (!IsAncestor(parentPid, clientPid))
                        {
                            loggerFactory.CreateLogger("init").LogError($"Unauthorized PID: {clientPid}. Disconnecting.");
                            await stream.DisposeAsync(); // forcefully disconnect
                        }
                        else
                        {
                            loggerFactory.CreateLogger("init").LogInformation($"Authorized PID: {clientPid}");
                        }
#endif
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error checking pipe PID: {ex}");
                        }
                    });

                    return stream;
                };
            }).ConfigureKestrel(options =>
            {
                options.ListenNamedPipe(pipeGuid.ToString(), o =>
                {
                    o.Protocols = HttpProtocols.Http2;
                });

#if DEBUG
                options.ListenLocalhost(debugPort, o =>
                {
                    o.Protocols = HttpProtocols.Http2;
                });
#endif
            });

            var app = builder.Build();
            app.UseRouting();
            app.UseCors(policyName);
#if DEBUG
            app.MapGrpcReflectionService();
#endif
            app.MapGrpcService<UiService>().RequireCors(policyName);

            await app.StartAsync(cancellationToken);
            return app;
        }

        static bool IsAncestor(uint ancestorPid, uint childPid)
        {
            try
            {
                uint currentPid = childPid;

                while (currentPid != 0)
                {
                    if (currentPid == ancestorPid)
                        return true;

                    currentPid = GetParentProcessId(currentPid);
                }
            }
            catch
            {
                // Could not find a process — probably exited
            }

            return false;
        }

        static uint GetParentProcessId(uint pid)
        {
            var handle = OpenProcess(ProcessAccessFlags.QueryInformation, false, (int)pid);
            if (handle == IntPtr.Zero)
                return 0;

            try
            {
                PROCESS_BASIC_INFORMATION pbi = new();
                int returnLength;
                NtQueryInformationProcess(handle, 0, ref pbi, Marshal.SizeOf(pbi), out returnLength);
                return (uint)pbi.InheritedFromUniqueProcessId.ToInt32();
            }
            finally
            {
                CloseHandle(handle);
            }
        }

        // Native structs and imports
        [StructLayout(LayoutKind.Sequential)]
        struct PROCESS_BASIC_INFORMATION
        {
            public IntPtr ExitStatus;
            public IntPtr PebBaseAddress;
            public IntPtr AffinityMask;
            public IntPtr BasePriority;
            public UIntPtr UniqueProcessId;
            public IntPtr InheritedFromUniqueProcessId;
        }

        [DllImport("ntdll.dll")]
        static extern int NtQueryInformationProcess(
            IntPtr processHandle, int processInformationClass,
            ref PROCESS_BASIC_INFORMATION processInformation,
            int processInformationLength, out int returnLength);

        [DllImport("kernel32.dll")]
        static extern IntPtr OpenProcess(ProcessAccessFlags access, bool inheritHandle, int processId);

        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr handle);

        [Flags]
        enum ProcessAccessFlags : uint
        {
            QueryInformation = 0x400
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetNamedPipeClientProcessId(SafeHandle Pipe, out uint ClientProcessId);

    }
}
