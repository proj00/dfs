using Grpc.Core;

namespace tracker
{
    internal class Program
    {
        static void Main(string[] args)
        {
            GrpcEnvironment.SetLogger(new Grpc.Core.Logging.LogLevelFilterLogger(
                new Grpc.Core.Logging.ConsoleLogger(),
                Grpc.Core.Logging.LogLevel.Debug));

            TrackerRpc rpc = new();
            var server = new Grpc.Core.Server
            {
                Services = { Tracker.Tracker.BindService(rpc) },
                Ports = { new ServerPort("localhost", 50330, ServerCredentials.Insecure) }
            };

            Console.WriteLine("Running...");

            server.Start();

            foreach (var port in server.Ports)
            {
                Console.WriteLine($"Server is listening on {port.Host}:{port.BoundPort}");
            }

            Console.ReadKey();
            server.ShutdownAsync().Wait();
        }
    }
}
