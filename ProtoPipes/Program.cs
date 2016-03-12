using System;
using System.Diagnostics;
using System.Threading;

namespace ProtoPipes
{
    class Program
    {
        private static ProtoClient _client;

        private static ProtoServer _server;

        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: ProtoPipes.exe [server|client {serverPID}]");
                Environment.Exit(2);
            }
            var type = args[0];

            int? serverPid = null;
            if (args.Length == 2)
            {
                serverPid = int.Parse(args[1]);
            }

            using (var cts = new CancellationTokenSource())
            {                
                var token = cts.Token;
                switch (type)
                {
                    case "server":
                        Console.WriteLine($"Running server with PID: {Process.GetCurrentProcess().Id}.");
                        RunServer(token);
                        break;
                    case "client":
                        Console.WriteLine($"Running client with PID: {Process.GetCurrentProcess().Id}.");
                        RunClient(token, serverPid);
                        break;
                    default:
                        Console.WriteLine("Usage: ProtoPipes.exe [server|client]");
                        Environment.Exit(2);
                        break;
                }

                Console.WriteLine("Press any key to quit.");
                Console.ReadKey();

                cts.Cancel();
            }
        }

        private static void RunClient(CancellationToken cancellationToken, int? serverPid)
        {
            _client = new ProtoClient(cancellationToken, serverPid);
            _client.Start();
        }

        private static void RunServer(CancellationToken cancellationToken)
        {
            _server = new ProtoServer(cancellationToken);
            _server.Start();
        }
    }
}
