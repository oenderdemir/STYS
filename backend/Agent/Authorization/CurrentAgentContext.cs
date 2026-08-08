using System.Security.Claims;

namespace STYS.Agent.Authorization;

public sealed class CurrentAgentContext : ICurrentAgentContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentAgentContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int AgentId => GetClaimInt("agentId") ?? 0;
    public string AgentInstanceId => GetClaimString("agentInstanceId") ?? string.Empty;
    public int KurumId => GetClaimInt("kurumId") ?? 0;
    public int CredentialVersion => GetClaimInt("credentialVersion") ?? 0;

    public IReadOnlyCollection<int> TesisIds
    {
        get
        {
            var raw = GetClaimString("agentTesisIds");
            if (string.IsNullOrWhiteSpace(raw)) return [];
            return raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => int.TryParse(x, out var id) ? id : (int?)null)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .ToList();
        }
    }

    public IReadOnlyCollection<string> Scopes
    {
        get
        {
            var raw = GetClaimString("agentScopes");
            if (string.IsNullOrWhiteSpace(raw)) return [];
            return raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .ToList();
        }
    }

    public bool IsAuthenticated => AgentId > 0;

    private string? GetClaimString(string claimType)
    {
        return _httpContextAccessor.HttpContext?.User.FindFirstValue(claimType);
    }

    private int? GetClaimInt(string claimType)
    {
        var value = GetClaimString(claimType);
        return int.TryParse(value, out var result) ? result : null;
    }
}
