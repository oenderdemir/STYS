namespace STYS.Agent.Contracts.Dtos;

public sealed class AgentEnrollmentRequest
{
    public string EnrollmentCode { get; set; } = string.Empty;
    public string AgentKey { get; set; } = string.Empty;
    public string? CihazKimligi { get; set; }
    public string? AgentVersion { get; set; }
    public string? PublicKey { get; set; }
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

public sealed class AgentEnrollmentCodeRequest
{
    public int KurumId { get; set; }
    public IReadOnlyCollection<int> TesisIds { get; set; } = [];
    public IReadOnlyCollection<string> AllowedScopes { get; set; } = [];
    public int? MaxKullanimSayisi { get; set; }
    public int? ExpirationHours { get; set; }
}

public sealed class AgentEnrollmentCodeDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int KurumId { get; set; }
    public string? KurumAd { get; set; }
    public IReadOnlyCollection<int> TesisIds { get; set; } = [];
    public IReadOnlyCollection<string> AllowedScopes { get; set; } = [];
    public int KullanimSayisi { get; set; }
    public int MaxKullanimSayisi { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int Durum { get; set; }
    public int? AgentId { get; set; }
    public DateTime CreatedAt { get; set; }
}
