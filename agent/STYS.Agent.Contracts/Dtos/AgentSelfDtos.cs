namespace STYS.Agent.Contracts.Dtos;

public sealed class AgentSelfTesisDto
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
}

public sealed class AgentSelfDto
{
    public int AgentId { get; set; }
    public string AgentAd { get; set; } = string.Empty;
    public string? AgentKey { get; set; }
    public int KurumId { get; set; }
    public string? KurumAd { get; set; }
    public IReadOnlyCollection<AgentSelfTesisDto> Tesisler { get; set; } = [];
    public IReadOnlyCollection<string> Scopes { get; set; } = [];
    public IReadOnlyCollection<string> Capabilities { get; set; } = [];
    public int Durum { get; set; }
    public string? AgentVersion { get; set; }
    public DateTime? LastHeartbeatAt { get; set; }
    public bool OnlineMi { get; set; }
}
