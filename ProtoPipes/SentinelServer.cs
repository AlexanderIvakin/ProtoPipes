using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProtoPipes
{
    public class SentinelServer: IDisposable
    {
        private readonly int _pid;

        private readonly Guid _serverToken;

        private NamedPipeServerStream _serverStream;

        public SentinelServer(int pid, Guid serverToken)
        {
            _pid = pid;
            _serverToken = serverToken;
        }

        public Task Run(CancellationToken cancellationToken)
        {
            _serverStream?.Dispose();

            _serverStream = new NamedPipeServerStream("protosentinel", PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances, 
                PipeTransmissionMode.Message, PipeOptions.Asynchronous | PipeOptions.WriteThrough);


            return Task.Factory
                .FromAsync(_serverStream.BeginWaitForConnection,
                    _serverStream.EndWaitForConnection,
                    TaskCreationOptions.LongRunning)
                .ContinueWith(async t => await SpawnChild(cancellationToken), cancellationToken)
                .ContinueWith(async t => await ServerLoop(cancellationToken), cancellationToken);
        }

        private async Task ServerLoop(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var msg = $"{_pid}:{_serverToken}";
#if DEBUG
            Console.WriteLine($"SentinelServer: {msg}.");
#endif
            var bytes = Encoding.UTF8.GetBytes(msg);
            try
            {
                await _serverStream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Sentinel: Operation has been cancelled...");
            }
            catch (IOException)
            {
                Console.WriteLine("Sentinel: Client disconnected...");
            }
            finally
            {
                _serverStream.Disconnect();

                _serverStream.Dispose();
                _serverStream = null;
            }
        }

        private async Task SpawnChild(CancellationToken cancellationToken)
        {
            var child = new SentinelServer(_pid, _serverToken);
            await child.Run(cancellationToken);
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