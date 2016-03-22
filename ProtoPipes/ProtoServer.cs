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

        public Task Run(CancellationToken cancellationToken)
        {
            _serverStream?.Dispose();

            _serverStream = new NamedPipeServerStream("protopipe", PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Message, PipeOptions.Asynchronous | PipeOptions.WriteThrough);

            return Task.Factory
                .FromAsync(_serverStream.BeginWaitForConnection,
                           _serverStream.EndWaitForConnection,
                           TaskCreationOptions.LongRunning)
                .ContinueWith(async t => await SpawnChild(cancellationToken), cancellationToken)
                .ContinueWith(async t => await ServerLoop(cancellationToken), cancellationToken);
        }

        private async Task SpawnChild(CancellationToken cancellationToken)
        {
            var child = new ProtoServer();
            await child.Run(cancellationToken);
        }

        private async Task ServerLoop(CancellationToken cancellationToken)
        {
            try
            {
                const int pauseMilliseconds = 2000;
                const int maxRandom = 100;
                var r = new Random();
                var pid = Process.GetCurrentProcess().Id;
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(pauseMilliseconds), cancellationToken);

                    var msg = $"{pid}:{r.Next(maxRandom)}";
                    Console.WriteLine($"Server: {msg}.");
                    var bytes = Encoding.UTF8.GetBytes(msg);
                    try
                    {
                        await _serverStream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
                    }
                    catch (IOException)
                    {
                        Console.WriteLine("Client disconnected, reconnecting...");
                        return;
                    }
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            finally
            {
                _serverStream.Disconnect();

                _serverStream.Dispose();
                _serverStream = null;
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

            if (_serverStream != null)
            {
                _serverStream.Dispose();
                _serverStream = null;
            }
        }
    }
}
