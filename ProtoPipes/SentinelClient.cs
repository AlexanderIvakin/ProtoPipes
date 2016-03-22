using System;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProtoPipes
{
    public class SentinelClient: IDisposable
    {
        private readonly ConcurrentDictionary<int, Guid> _sentinelServers;

        private CancellationTokenSource _stopTokenSource;

        public SentinelClient(ConcurrentDictionary<int, Guid> sentinelServers)
        {
            _sentinelServers = sentinelServers;
        }

        public Task Run()
        {
            _stopTokenSource?.Dispose();

            _stopTokenSource = new CancellationTokenSource();

            return Task.Factory.StartNew(async () => await ListenLoop(_stopTokenSource.Token), _stopTokenSource.Token);
        }

        public Task Run(CancellationToken cancellationToken)
        {
            _stopTokenSource?.Dispose();

            _stopTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

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
                    var receivedServierToken = Guid.Parse(split[1].Trim());
#if DEBUG
                    Console.WriteLine($"SentinelClient received: {receivedPid} - {receivedServierToken}.");
#endif
                    _sentinelServers.TryAdd(receivedPid, receivedServierToken);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            if (_stopTokenSource != null)
            {
                _stopTokenSource.Dispose();
                _stopTokenSource = null;
            }
        }
    }
}