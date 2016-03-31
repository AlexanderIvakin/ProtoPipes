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

        private readonly Guid _serverToken;

        private CancellationTokenSource _stopTokenSource;

        public ProtoClient(Guid serverToken)
        {
            _serverToken = serverToken;
        }

        public Task Run()
        {
            return Run(CancellationToken.None);
        }
        
        public Task Run(CancellationToken cancellationToken)
        {
            _clientStream?.Dispose();

            _stopTokenSource?.Dispose();

            _clientStream = new NamedPipeClientStream(".", _serverToken.ToString(), PipeDirection.InOut, 
                PipeOptions.Asynchronous | PipeOptions.WriteThrough);

            _stopTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            return Task.Factory.StartNew(async () => await ListenLoop(_stopTokenSource.Token), _stopTokenSource.Token);                
        }

        private Task RunInternal(CancellationToken cancellationToken)
        {
            _clientStream?.Dispose();

            _clientStream = new NamedPipeClientStream(".", _serverToken.ToString(), PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.WriteThrough);

            return Task.Factory.StartNew(async () => await ListenLoop(cancellationToken), cancellationToken);
        }

        public void Stop()
        {
            _stopTokenSource?.Cancel();
        }

        private async Task ListenLoop(CancellationToken cancellationToken)
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
            }

            if (cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
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

            _clientStream?.Dispose();
            _clientStream = null;

            _stopTokenSource?.Dispose();
            _stopTokenSource = null;
        }
    }
}
