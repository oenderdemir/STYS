using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace TOD.Platform.Security.Auth.Services;

public interface ICurrentTenantAccessor
{
    int? GetCurrentKurumId();

    IReadOnlyList<int> GetAccessibleKurumIds();

    bool IsSuperAdmin();

    bool IsKurumAdmin();
}

public class HttpContextCurrentTenantAccessor : ICurrentTenantAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentTenantAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? GetCurrentKurumId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var raw = user?.FindFirstValue("kurumId");

        return int.TryParse(raw, out var kurumId)
            ? kurumId
            : null;
    }

    public IReadOnlyList<int> GetAccessibleKurumIds()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var raw = user?.FindFirstValue("kurumIds");

        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var id) ? id : (int?)null)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();
    }

    public bool IsSuperAdmin()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var raw = user?.FindFirstValue("isSuperAdmin");

        return bool.TryParse(raw, out var value) && value;
    }

    public bool IsKurumAdmin()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var raw = user?.FindFirstValue("isKurumAdmin");

        return bool.TryParse(raw, out var value) && value;
    }
}
