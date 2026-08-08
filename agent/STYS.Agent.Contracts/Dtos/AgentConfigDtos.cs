namespace STYS.Agent.Contracts.Dtos;

public sealed class AgentConfigDto
{
    public long Version { get; set; }
    public Dictionary<string, string> Configs { get; set; } = [];
}
