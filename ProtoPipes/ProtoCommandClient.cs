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

        private readonly Guid _serverToken;

        private readonly object _pipeLock = new object();

        public ProtoCommandClient(Guid serverToken)
        {
            _serverToken = serverToken;
        }

        public void Connect(CancellationToken cancellationToken)
        {
            const int connectTimeout = 500;

            _clientStream = new NamedPipeClientStream(".", _serverToken.ToString(),
                PipeDirection.InOut, PipeOptions.Asynchronous);

            _clientStream.Connect(connectTimeout);
            _clientStream.ReadMode = PipeTransmissionMode.Message;
        }

        public void StopAll()
        {
            var msg = @"stopall";
            var buff = Encoding.UTF8.GetBytes(msg);
            lock (_pipeLock)
            {
                if (_clientStream.IsConnected)
                {
                    _clientStream.Write(buff, 0, buff.Length);
                }
            }
        }

        public void GetTime()
        {
            var msg = @"gettime";
            var buff = Encoding.UTF8.GetBytes(msg);
            lock (_pipeLock)
            {
                if (_clientStream.IsConnected)
                {
                    _clientStream.Write(buff, 0, buff.Length);
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

            _clientStream?.Dispose();
            _clientStream = null;
        }
    }
}
