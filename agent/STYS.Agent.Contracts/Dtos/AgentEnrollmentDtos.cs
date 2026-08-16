namespace STYS.Agent.Contracts.Dtos;

public sealed class AgentEnrollmentRequest
{
    public string EnrollmentCode { get; set; } = string.Empty;
    public string AgentKey { get; set; } = string.Empty;
    public string? AgentDisplayName { get; set; }
    public string? CihazKimligi { get; set; }
    public string? AgentVersion { get; set; }
    public string? PublicKey { get; set; }
    public IReadOnlyCollection<string> Capabilities { get; set; } = [];
}

public sealed class AgentEnrollmentResponse
{
    public int AgentId { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string AgentKey { get; set; } = string.Empty;
    public int Durum { get; set; }
    public string? Message { get; set; }
}

/// <summary>Credential-authenticated approval-status probe. A PendingApproval agent has no access
/// token, so it authenticates with the credential it received at registration and learns nothing
/// beyond its own lifecycle status.</summary>
public sealed class AgentEnrollmentStatusRequest
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}

public sealed class AgentEnrollmentStatusResponse
{
    public int AgentId { get; set; }
    public int Durum { get; set; }
    /// <summary>True only when the agent may proceed to acquire an access token.</summary>
    public bool Approved { get; set; }
    /// <summary>True while the operator has neither approved nor refused the agent.</summary>
    public bool PendingApproval { get; set; }
    public string? Message { get; set; }
}

public sealed class AgentEnrollmentCodeRequest
{
    public IReadOnlyCollection<int> TesisIds { get; set; } = [];
    public IReadOnlyCollection<string> AllowedScopes { get; set; } = [];
    public int? MaxKullanimSayisi { get; set; }
    public int? ExpirationHours { get; set; }
    public bool RequiresApproval { get; set; }
}

public sealed class AgentEnrollmentCodeDto
{
    public int Id { get; set; }

    /// <summary>Plaintext enrollment code. Populated ONLY in the response that creates the code;
    /// it is never persisted and every later read returns null.</summary>
    public string? Code { get; set; }

    /// <summary>Non-secret prefix so operators can identify a code in listings.</summary>
    public string CodePrefix { get; set; } = string.Empty;

    public int KurumId { get; set; }
    public string? KurumAd { get; set; }
    public IReadOnlyCollection<int> TesisIds { get; set; } = [];
    public IReadOnlyCollection<string> AllowedScopes { get; set; } = [];
    public int KullanimSayisi { get; set; }
    public int MaxKullanimSayisi { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool RequiresApproval { get; set; }
    public int Durum { get; set; }
    public int? AgentId { get; set; }
    public DateTime CreatedAt { get; set; }
}
