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
        private readonly ConcurrentDictionary<int, ServerInfo> _sentinelServers;

        private CancellationTokenSource _stopTokenSource;

        public SentinelClient(ConcurrentDictionary<int, ServerInfo> sentinelServers)
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
            const int connectTimeout = 500;

            using (var clientStream = new NamedPipeClientStream(".", "protosentinel", PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.WriteThrough))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                clientStream.Connect(connectTimeout);

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
                if (split.Length > 2)
                {                   
                    int receivedPid;
                    if (!int.TryParse(split[0].Trim(), out receivedPid)) return;

                    Guid receivedNotificationServerToken;
                    if (!Guid.TryParse(split[1].Trim(), out receivedNotificationServerToken)) return;

                    Guid receivedCommandServerToken;
                    if (!Guid.TryParse(split[2].Trim(), out receivedCommandServerToken)) return;
#if DEBUG
                    Console.WriteLine($"SentinelClient received: {receivedPid} - {receivedServierToken}.");
#endif
                    _sentinelServers.TryAdd(receivedPid, 
                        new ServerInfo(receivedPid, receivedNotificationServerToken, receivedCommandServerToken));
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