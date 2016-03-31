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

        private readonly Guid _notificationServerToken;

        private readonly Guid _commandServerToken;

        private NamedPipeServerStream _serverStream;

        private CancellationTokenSource _stopTokenSource;

        public SentinelServer(int pid, Guid notificationServerToken, Guid commandServerToken)
        {
            _pid = pid;
            _notificationServerToken = notificationServerToken;
            _commandServerToken = commandServerToken;
        }

        public Task Run()
        {
            return Run(CancellationToken.None);
        }

        public Task Run(CancellationToken cancellationToken)
        {
            _serverStream?.Dispose();

            _stopTokenSource?.Dispose();

            _serverStream = new NamedPipeServerStream("protosentinel", PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances, 
                PipeTransmissionMode.Message, PipeOptions.Asynchronous | PipeOptions.WriteThrough);

            _stopTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            return Task.Factory
                .FromAsync(_serverStream.BeginWaitForConnection,
                    _serverStream.EndWaitForConnection,
                    TaskCreationOptions.LongRunning)
                .ContinueWith(async t => await SpawnChild(_stopTokenSource.Token), _stopTokenSource.Token)
                .ContinueWith(async t => await ServerLoop(_stopTokenSource.Token), _stopTokenSource.Token);
        }

        public void Stop()
        {
            _stopTokenSource?.Cancel();
        }

        private Task RunChild(CancellationToken cancellationToken)
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

            var msg = $"{_pid}:{_notificationServerToken}:{_commandServerToken}";
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
            var child = new SentinelServer(_pid, _notificationServerToken, _commandServerToken);
            await child.RunChild(cancellationToken);
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

            if (_stopTokenSource != null)
            {
                _stopTokenSource.Dispose();
                _stopTokenSource = null;
            }
        }
    }
}