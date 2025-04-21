using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.IO.Pipes;
using System.Reflection.Metadata;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;
using System.IO.Pipelines;

namespace node
{

    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        static async Task Main(string[] args)
        {
            Guid pipeGuid = new();
            uint parentPid = 0;
            if (!(args.Length == 2 && Guid.TryParse(args[0], out pipeGuid) && uint.TryParse(args[1], out parentPid)))
            {
                Console.WriteLine("Please provide a pipe GUID and a PID as the first argument.");
                return;
            }

            NodeState state = new(TimeSpan.FromMinutes(1));
            NodeRpc rpc = new(state);
            var publicServer = await StartPublicNodeServerAsync(rpc);
            var publicUrl = publicServer.Urls.First();

            UiService service = new(state, "publicUrl");
            var pipeStreams = new ConcurrentDictionary<string, NamedPipeServerStream>();
            var privateServer = await StartGrpcWebServerAsync(service, pipeGuid, parentPid, pipeStreams);

            await service.ShutdownEvent.WaitAsync();
            await publicServer.StopAsync();
            await privateServer.StopAsync();
        }

        private static async Task<WebApplication> StartPublicNodeServerAsync(NodeRpc rpc)
        {
            var builder = WebApplication.CreateBuilder();

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
            builder.Services.AddGrpcReflection();

            var app = builder.Build();
            IWebHostEnvironment env = app.Environment;
#if DEBUG
            app.MapGrpcReflectionService();
#endif

            app.UseRouting();
            app.UseCors(policyName);
            await app.StartAsync();
            return app;
        }

        private static async Task<WebApplication> StartGrpcWebServerAsync(UiService service, Guid pipeGuid, uint parentPid,
            ConcurrentDictionary<string, NamedPipeServerStream> pipeStreams)
        {
            var builder = WebApplication.CreateBuilder();

            builder.Services.AddGrpc();
            const string policyName = "AllowLocal";
            builder.Services.AddCors(o => o.AddPolicy(policyName, policy =>
            {
                policy.SetIsOriginAllowed(origin => new Uri(origin).IsLoopback)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding");
            }));

            builder.Services.AddSingleton(service);
            builder.Services.AddGrpcReflection();

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
                            await stream.WaitForConnectionAsync();

                            if (GetNamedPipeClientProcessId(stream.SafePipeHandle, out var clientPid))
                            {
#if !DEBUG
                                if (!IsAncestor(parentPid, clientPid))
#else
                                if (false)
#endif
                                {
                                    Console.WriteLine($"Unauthorized PID: {clientPid}. Disconnecting.");
                                    await stream.DisposeAsync(); // forcefully kill connection
                                }
                                else
                                {
                                    Console.WriteLine($"Authorized PID: {clientPid}");
                                }
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
                options.ListenLocalhost(42069, o =>
                {
                    o.Protocols = HttpProtocols.Http2;
                });
#endif
            });
            var app = builder.Build();

            IWebHostEnvironment env = app.Environment;
#if DEBUG
            app.MapGrpcReflectionService();
#endif

            app.UseRouting();
            app.UseCors(policyName);
            app.MapGrpcService<UiService>().RequireCors(policyName);

            await app.StartAsync();
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
