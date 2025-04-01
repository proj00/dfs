using System;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Logging;
using common;

namespace tracker
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var serverCredentials = ServerCredentials.Insecure;
            FilesystemManager filesystemManager = new FilesystemManager();
            GrpcEnvironment.SetLogger(new LogLevelFilterLogger(
                new ConsoleLogger(),
                LogLevel.Debug));

            TrackerRpc rpc = new TrackerRpc(filesystemManager);
            var server = new Server
            {
                Services = { Tracker.Tracker.BindService(rpc) },
                Ports = { new ServerPort("localhost", 50330, serverCredentials) }
            };

            Console.WriteLine("Running...");
            server.Start();

            foreach (var port in server.Ports)
            {
                Console.WriteLine($"Server is listening on {port.Host}:{port.BoundPort}");
            }

            Console.WriteLine("Paspauskite bet kurį mygtuką, kad išjungtumėte serverį...");
            Console.ReadKey();

            await server.ShutdownAsync();
        }
    }
}
