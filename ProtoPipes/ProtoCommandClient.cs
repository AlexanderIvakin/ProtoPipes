using System;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProtoPipes
{
    public class ProtoCommandClient : IDisposable
    {
        private NamedPipeClientStream _clientStream;

        private readonly int _serverPid;

        private AutoResetEvent _are;

        public ProtoCommandClient(int serverPid)
        {
            _serverPid = serverPid;
            _are = new AutoResetEvent(false);
        }

        public async Task Connect(CancellationToken cancellationToken)
        {
            const int connectTimeout = 500;
            const int maxTries = 5;
            var trie = 0;
            var connected = false;

            while (trie < maxTries)
            {
                trie++;
                _clientStream?.Dispose();

                _clientStream = new NamedPipeClientStream(".", "protocommands",
                    PipeDirection.InOut, PipeOptions.Asynchronous);

                _clientStream.Connect(connectTimeout);
                _clientStream.ReadMode = PipeTransmissionMode.Message;

                connected = await Handshake(cancellationToken);
                if (connected)
                {
                    _are.Set();
                    break;
                }
            }

            if (!connected)
            {
                throw new ApplicationException($"Failed to connect to the server running at {_serverPid}");
            }
        }

        private async Task<bool> Handshake(CancellationToken cancellationToken)
        {
            const int bufferLength = 1024;
            var buffer = new byte[bufferLength];
            var sb = new StringBuilder();

            do
            {
                var bytesRead = await _clientStream.ReadAsync(buffer, 0, bufferLength, cancellationToken);
                sb.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
            } while (!_clientStream.IsMessageComplete);
            var msg = sb.ToString();

            int receivedPid;
            if (int.TryParse(msg, out receivedPid))
            {
                return receivedPid == _serverPid;
            }
            return false;
        }

        public void StopAll()
        {
            _are.WaitOne();
            var msg = @"stopall";
            var buff = Encoding.UTF8.GetBytes(msg);
            if (_clientStream.IsConnected)
            {
                 _clientStream.Write(buff, 0, buff.Length);
            }
            _are.Set();
        }

        public void GetTime()
        {
            _are.WaitOne();
            var msg = @"gettime";
            var buff = Encoding.UTF8.GetBytes(msg);
            if (_clientStream.IsConnected)
            {
                _clientStream.Write(buff, 0, buff.Length);
            }
            _are.Set();
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
