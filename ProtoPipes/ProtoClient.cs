using System;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProtoPipes
{
    public class ProtoClient : IDisposable
    {
        private NamedPipeClientStream _clientStream;

        private readonly int _serverPid;

        public ProtoClient(int serverPid)
        {
            _serverPid = serverPid;
        }
        
        public Task Run(CancellationToken cancellationToken)
        {
            _clientStream?.Dispose();

            _clientStream = new NamedPipeClientStream(".", "protopipe", PipeDirection.InOut, 
                PipeOptions.Asynchronous | PipeOptions.WriteThrough);

            return Task.Factory.StartNew(async p => await ListenLoop((int?)p, cancellationToken),
                _serverPid, cancellationToken);                
        }

        private async Task ListenLoop(int? serverPid, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            _clientStream.Connect();

            _clientStream.ReadMode = PipeTransmissionMode.Message;

            const int bufferLength = 1024;
            var buffer = new byte[bufferLength];
            var sb = new StringBuilder();
            var doReconnect = false;

            while (_clientStream.IsConnected 
                && !cancellationToken.IsCancellationRequested)
            {
                do
                {
                    var bytesRead = await _clientStream.ReadAsync(buffer, 0, bufferLength, cancellationToken);
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                } while (!_clientStream.IsMessageComplete);

                var msg = sb.ToString();
                Console.WriteLine($"Client received: {msg}.");
                sb.Clear();

                if (!serverPid.HasValue) continue;

                var split = msg.Split(':');
                if (split.Length > 1)
                {
                    var receivedPid = int.Parse(split[0]);
                    if (receivedPid == serverPid.Value) continue;
                    Console.WriteLine($"Wrong server {receivedPid}. Attempting to reconnect to {serverPid}.");
                    doReconnect = true;
                    break;
                }                
            }

            if (cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (doReconnect)
            {
                await Run(cancellationToken);
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

            if (_clientStream != null)
            {
                _clientStream.Dispose();
                _clientStream = null;
            }
        }
    }
}
