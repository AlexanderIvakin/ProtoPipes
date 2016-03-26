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

        private static ProtoCommandServer _commandServer;

        private static ProtoCommandClient _commandClient;

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

            var pid = Process.GetCurrentProcess().Id;

            using (var cts = new CancellationTokenSource())
            {
                var token = cts.Token;
                switch (type)
                {
                    case "server":
                        Console.WriteLine($"Running server with PID: {pid}.");
                        RunSentinel(pid, Guid.NewGuid(), token);
                        RunCommandServer(cts);
                        RunServer(token);
                        break;
                    case "client":
                        Console.WriteLine($"Running client with PID: {pid}.");
                        Console.CancelKeyPress += OnCancelKeyPress;
                        RunClient(token, serverPid).ConfigureAwait(false);
                        break;
                    default:
                        Console.WriteLine("Usage: ProtoPipes.exe [server|client]");
                        Environment.Exit(2);
                        break;
                }

                Console.WriteLine("Press 'x' key to quit.");
                while (true)
                {
                    var cki = Console.ReadKey();
                    if (cki.KeyChar == 'x') break;
                }

                cts.Cancel();
            }
        }

        private static void OnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Console.WriteLine("Sending 'Stop all' to the server");
            _commandClient.StopAll();
            Console.WriteLine("Exiting...");
        }

        private static void RunCommandServer(CancellationTokenSource cts)
        {
            _commandServer = new ProtoCommandServer();
            _commandServer.StopAll += (s, e) => { cts.Cancel(); };
            _commandServer.GetTime += (s, e) => { Console.WriteLine(DateTime.Now); };
            _commandServer.Run(cts.Token);
        }

        private static void RunSentinel(int pid, Guid serverToken, CancellationToken cancellationToken)
        {
            _sentinelServer = new SentinelServer(pid, serverToken);
            _sentinelServer.Run(cancellationToken);
        }

        private static void StopSentinel()
        {
            _sentinelServer.Stop();
        }

        private static async Task RunClient(CancellationToken cancellationToken, int? serverPid)
        {
            var pid = serverPid ?? AskUserToWhichServerToConnect();

            _client = new ProtoClient(pid);
            await _client.Run(cancellationToken);

            _commandClient = new ProtoCommandClient(pid);
            await _commandClient.Connect(cancellationToken);
            await Task.Factory.StartNew(async () =>
             {
                 while (true)
                 {
                     await Task.Delay(TimeSpan.FromSeconds(5));
                     _commandClient.GetTime();
                 }
             }, cancellationToken);
        }

        private static void StopClient()
        {
            _client.Stop();
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

                        token.Register(() => sentinelClient.Dispose());

                        sentinelClient.Run(token);
                    }, () =>
                    {
                        var sentinelClient = new SentinelClient(servers);

                        token.Register(() => sentinelClient.Dispose());

                        sentinelClient.Run(token);
                    }, () =>
                    {
                        var sentinelClient = new SentinelClient(servers);

                        token.Register(() => sentinelClient.Dispose());

                        sentinelClient.Run(token);
                    });

                    trie++;
                }

                Console.WriteLine("Looking for running servers...");

                Task.Delay(TimeSpan.FromSeconds(waitSeconds), token).Wait(token);

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
            _server = new ProtoServer();
            _server.Run(cancellationToken);
        }

        private static void StopServer()
        {
            _server.Stop();
        }
    }
}
