using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProtoPipes
{
    class Program
    {
        private static ProtoClient _client;

        private static ProtoServer _server;

        private static SentinelServer _sentinelServer;

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
                        RunSentinel(Process.GetCurrentProcess().Id, Guid.NewGuid(), token);
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

        private static void RunSentinel(int pid, Guid serverToken, CancellationToken cancellationToken)
        {
            _sentinelServer = new SentinelServer(pid, serverToken);
            _sentinelServer.Run(cancellationToken);
        }

        private static void RunClient(CancellationToken cancellationToken, int? serverPid)
        {
            if (!serverPid.HasValue)
            {
                serverPid = AskUserToWhichServerToConnect();
            }

            _client = new ProtoClient(cancellationToken, serverPid);
            _client.Start();
        }

        private static int AskUserToWhichServerToConnect()
        {
            using (var cts = new CancellationTokenSource())
            {
                var token = cts.Token;
                var servers = new ConcurrentDictionary<int, Guid>();

                const int waitSeconds = 5;
                const int maxTries = 5;
                var trie = 0;

                while (trie < maxTries)
                {
                    Parallel.Invoke(() =>
                    {
                        var sentinelClient = new SentinelClient(servers);
                        sentinelClient.Run(token);
                    }, () =>
                    {
                        var sentinelClient = new SentinelClient(servers);
                        sentinelClient.Run(token);
                    }, () =>
                    {
                        var sentinelClient = new SentinelClient(servers);
                        sentinelClient.Run(token);
                    });

                    trie++;
                }

                Console.WriteLine("Looking for running servers...");
                Thread.Sleep(TimeSpan.FromSeconds(waitSeconds));
                cts.Cancel();
                var snapshot = servers.ToDictionary(p => p.Key, p => p.Value);
                if (snapshot.Count == 0)
                {
                    Console.WriteLine("No instances of running servers found. Exiting.");
                    Environment.Exit(0);
                }

                int selectedIdx;
                
                var keys = snapshot.Keys.ToList();

                while (true)
                {
                    Console.WriteLine("Please select an instance of the server:");
                    for (var idx = 0; idx < keys.Count; idx++)
                    {
                        Console.WriteLine($"{idx + 1}: {keys[idx]} - {snapshot[keys[idx]]}");
                    }


                    var selection = Console.ReadLine();

                    if (!int.TryParse(selection, out selectedIdx)) continue;

                    if (1 <= selectedIdx && selectedIdx <= keys.Count + 1)
                    {
                        Console.WriteLine(
                            $"You chose wisely: {keys[selectedIdx - 1]} - {snapshot[keys[selectedIdx - 1]]}!");
                        break;
                    }
                    Console.WriteLine("Please try again...");
                }

                return keys[selectedIdx - 1];

            }
        }

        private static void RunServer(CancellationToken cancellationToken)
        {
            _server = new ProtoServer(cancellationToken);
            _server.Start();
        }
    }
}
