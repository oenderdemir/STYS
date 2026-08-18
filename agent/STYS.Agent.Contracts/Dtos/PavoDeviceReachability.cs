namespace STYS.Agent.Contracts.Dtos;

/// <summary>
/// Transport-level failure codes reported by the PAVO client when no device response was received.
///
/// The distinction that matters for payments is whether the request could have reached the device.
/// A TCP connection that was never established means the device processed nothing, so the payment
/// can be resolved as failed. Anything else — including a request that was sent but never answered —
/// leaves open the possibility that the card was charged and must stay Unknown for reconciliation.
/// </summary>
public static class PavoDeviceReachability
{
    /// <summary>TCP connect timed out; the device was never reached.</summary>
    public const string ConnectTimeout = "CONNECT_TIMEOUT";

    /// <summary>Host actively refused the connection; nothing was delivered.</summary>
    public const string ConnectionRefused = "CONNECTION_REFUSED";

    /// <summary>No route to the host; nothing was delivered.</summary>
    public const string NetworkUnreachable = "NETWORK_UNREACHABLE";

    /// <summary>
    /// Request was sent but no response arrived within the timeout. Deliberately NOT treated as
    /// "not delivered": the device may have completed the payment.
    /// </summary>
    public const string ResponseTimeout = "TIMEOUT";

    /// <summary>
    /// True only for failures where the connection was never established, so the device cannot have
    /// acted on the request.
    ///
    /// Kept deliberately narrow. TLS and generic network errors are excluded because they can also
    /// surface after bytes were sent, and for a payment the safe default is ambiguity, not a
    /// confident "failed".
    /// </summary>
    public static bool IsDeviceNeverReached(string? errorCode) =>
        string.Equals(errorCode, ConnectTimeout, StringComparison.OrdinalIgnoreCase)
        || string.Equals(errorCode, ConnectionRefused, StringComparison.OrdinalIgnoreCase)
        || string.Equals(errorCode, NetworkUnreachable, StringComparison.OrdinalIgnoreCase);
}
