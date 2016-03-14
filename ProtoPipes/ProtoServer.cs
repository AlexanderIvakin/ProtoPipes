using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProtoPipes
{
    public class ProtoServer : IDisposable
    {
        private NamedPipeServerStream _serverStream;

        private readonly CancellationToken _cancellationToken;

        private CancellationTokenSource _cts;

        private ProtoServer _child;

        public ProtoServer(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
        }

        public Task Start()
        {
            _serverStream?.Dispose();

            _cts?.Dispose();

            _serverStream = new NamedPipeServerStream("protopipe", PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Message, PipeOptions.Asynchronous | PipeOptions.WriteThrough);

            _cts = new CancellationTokenSource();

            using (var linkedTokenSource = CancellationTokenSource
                .CreateLinkedTokenSource(_cancellationToken, _cts.Token))
            {
                var linkedToken = linkedTokenSource.Token;

                return Task.Factory
                    .FromAsync(_serverStream.BeginWaitForConnection,
                               _serverStream.EndWaitForConnection,
                               TaskCreationOptions.LongRunning)
                    .ContinueWith(async t => await SpawnChild(), linkedToken)
                    .ContinueWith(async t => await ServerLoop(linkedToken), linkedToken);
            }
        }

        private async Task SpawnChild()
        {
            _child = new ProtoServer(_cancellationToken);
            await _child.Start();
        }

        private async Task ServerLoop(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            const int pauseMilliseconds = 2000;
            const int maxRandom = 100;
            var r = new Random();
            var pid = Process.GetCurrentProcess().Id;
            while (!cancellationToken.IsCancellationRequested)
            {
                Thread.Sleep(pauseMilliseconds);

                var msg = $"{pid}:{r.Next(maxRandom)}";
                Console.WriteLine($"Server: {msg}.");
                var bytes = Encoding.UTF8.GetBytes(msg);
                try
                {
                    await _serverStream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
                }
                catch(IOException)
                {
                    Console.WriteLine("Client disconnected, reconnecting...");

                    _serverStream.Disconnect();

                    _serverStream.Dispose();
                    _serverStream = null;
                                        
                    return;
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        public void Stop()
        {
            _cts?.Cancel();
            _child?.Stop();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            if (_serverStream != null)
            {
                _serverStream.Dispose();
                _serverStream = null;
            }

            if (_cts != null)
            {
                _cts.Dispose();
                _cts = null;
            }

            if (_child != null)
            {
                _child.Dispose();
                _child = null;
            }
        }
    }
}
