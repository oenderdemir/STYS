namespace STYS.Agent.Contracts.Dtos;

public sealed class AgentDto
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string AgentKey { get; set; } = string.Empty;
    public int KurumId { get; set; }
    public string? KurumAd { get; set; }
    public int Durum { get; set; }
    public string? AgentVersion { get; set; }
    public DateTime? SonGorulmeTarihi { get; set; }
    public string? CihazKimligi { get; set; }
    public IReadOnlyCollection<int> TesisIds { get; set; } = [];
    public IReadOnlyCollection<string> Scopes { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}

public sealed class AgentListDto
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string AgentKey { get; set; } = string.Empty;
    public int KurumId { get; set; }
    public string? KurumAd { get; set; }
    public int Durum { get; set; }
    public string? AgentVersion { get; set; }
    public DateTime? SonGorulmeTarihi { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class AgentKaydetRequest
{
    public string Ad { get; set; } = string.Empty;
    public int KurumId { get; set; }
    public IReadOnlyCollection<int> TesisIds { get; set; } = [];
    public IReadOnlyCollection<string> Scopes { get; set; } = [];
}
