using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProtoPipes
{
    public class ProtoCommandServer : IDisposable
    {
        private NamedPipeServerStream _serverStream;

        private CancellationTokenSource _stopTokenSource;

        public event EventHandler StopAll = delegate { };

        protected virtual void OnStopAll(object sender, EventArgs args)
        {
            StopAll.Invoke(sender, args);
        }

        public event EventHandler GetTime = delegate { };

        protected virtual void OnGetTime(object sender, EventArgs args)
        {
            GetTime.Invoke(sender, args);
        }

        public Task Run()
        {
            return Run(CancellationToken.None);
        }

        public Task Run(CancellationToken cancellationToken)
        {
            _serverStream?.Dispose();
            _stopTokenSource?.Dispose();

            _serverStream = new NamedPipeServerStream("protocommands", PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Message,
                PipeOptions.Asynchronous);

            _stopTokenSource = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken);

            return Task.Factory.FromAsync(_serverStream.BeginWaitForConnection,
                _serverStream.EndWaitForConnection,
                TaskCreationOptions.LongRunning | TaskCreationOptions.PreferFairness)
                    .ContinueWith(async t => await SpawnChild(cancellationToken), cancellationToken)
                    .ContinueWith(async t => await ServerLoop(cancellationToken), cancellationToken);
        }

        public void Stop()
        {
            _stopTokenSource?.Cancel();
        }

        private Task RunInternal(CancellationToken cancellationToken)
        {
            _serverStream?.Dispose();

            _serverStream = new NamedPipeServerStream("protocommands", PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Message,
                PipeOptions.Asynchronous);

            return Task.Factory.FromAsync(_serverStream.BeginWaitForConnection,
                _serverStream.EndWaitForConnection,
                TaskCreationOptions.LongRunning | TaskCreationOptions.PreferFairness)
                    .ContinueWith(async t => await SpawnChild(cancellationToken), cancellationToken)
                    .ContinueWith(async t => await ServerLoop(cancellationToken), cancellationToken);
        }

        private async Task SpawnChild(CancellationToken cancellationToken)
        {
            var child = new ProtoCommandServer();
            await child.RunInternal(cancellationToken);
        }

        private async Task ServerLoop(CancellationToken cancellationToken)
        {
            try
            {
                try
                {
                    await SayMyName(cancellationToken);

                    await Task.Run(() => _serverStream.WaitForPipeDrain(), cancellationToken);

                    const int bufferLength = 1024;
                    var buffer = new byte[bufferLength];
                    var sb = new StringBuilder();

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        do
                        {
                            var bytesRead = await _serverStream.ReadAsync(buffer, 0, bufferLength, cancellationToken);
                            sb.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                        } while (!_serverStream.IsMessageComplete);

                        var msg = sb.ToString();
                        Console.WriteLine($"Command server received: {msg}.");
                        sb.Clear();

                        HandleMessage(msg);
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Stopping command server");
                }
                catch (IOException)
                {
                    Console.WriteLine("Client disconnected...");
                }
            }
            finally
            {
                _serverStream.Disconnect();
                _serverStream.Dispose();
                _serverStream = null;
            }
        }

        private void HandleMessage(string msg)
        {
            switch (msg.ToLowerInvariant())
            {
                case "stopall":
                    OnStopAll(this, new EventArgs());
                    break;
                case "gettime":
                    OnGetTime(this, new EventArgs());
                    break;
                default:
                    throw new ArgumentException($"Invalid command {msg}");
            }
        }

        private async Task SayMyName(CancellationToken cancellationToken)
        {
            var msg = $"{Process.GetCurrentProcess().Id}";
            var buff = Encoding.UTF8.GetBytes(msg);

            await _serverStream.WriteAsync(buff, 0, buff.Length);
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
