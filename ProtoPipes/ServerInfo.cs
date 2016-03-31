using System;

namespace ProtoPipes
{
    public class ServerInfo
    {
        public int PID { get; }
        public Guid NotificationServerToken { get; }
        public Guid CommandServerToken { get; }

        public ServerInfo(int pid, Guid notificationServerToken, Guid commandServerToken)
        {
            PID = pid;
            NotificationServerToken = notificationServerToken;
            CommandServerToken = commandServerToken;
        }
    }
}
