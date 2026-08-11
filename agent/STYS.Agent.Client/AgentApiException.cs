using System.Net;

namespace STYS.Agent.Client;

public sealed class AgentApiException : Exception
{
    public AgentApiException(HttpStatusCode statusCode, string message, string? traceId = null)
        : base(message)
    {
        StatusCode = statusCode;
        TraceId = traceId;
    }

    public HttpStatusCode StatusCode { get; }
    public string? TraceId { get; }
}
