using System;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProtoPipes
{
    public class SentinelClient
    {
        private readonly ConcurrentDictionary<int, string> _sentinelServers;

        public SentinelClient(ConcurrentDictionary<int, string> sentinelServers)
        {
            _sentinelServers = sentinelServers;
        }

        public Task Run(CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew(async () => await ListenLoop(cancellationToken), cancellationToken);
        }

        private async Task ListenLoop(CancellationToken cancellationToken)
        {
            using (var clientStream = new NamedPipeClientStream(".", "protosentinel", PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.WriteThrough))
            {
                clientStream.Connect();

                clientStream.ReadMode = PipeTransmissionMode.Message;

                const int bufferLength = 1024;
                var buffer = new byte[bufferLength];
                var sb = new StringBuilder();

                while (clientStream.IsConnected
                    && !cancellationToken.IsCancellationRequested)
                {
                    do
                    {
                        var bytesRead = await clientStream.ReadAsync(buffer, 0, bufferLength, cancellationToken);
                        sb.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                    } while (!clientStream.IsMessageComplete);
                }

                var msg = sb.ToString();                

                var split = msg.Split(':');
                if (split.Length > 1)
                {                   
                    var receivedPid = int.Parse(split[0].Trim());
                    var receivedServerMsg = split[1].Trim();
                    Console.WriteLine($"SentinelClient received: {receivedPid} - {receivedServerMsg}.");
                    _sentinelServers.TryAdd(receivedPid, receivedServerMsg);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
        }
    }
}