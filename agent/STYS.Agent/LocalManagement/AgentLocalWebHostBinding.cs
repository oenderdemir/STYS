using System.Net;

namespace STYS.Agent.LocalManagement;

public static class AgentLocalWebHostBinding
{
    public static IPEndPoint CreateLoopbackEndpoint(int port) => new(IPAddress.Loopback, port);
}
