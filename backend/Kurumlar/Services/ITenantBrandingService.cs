using STYS.Kurumlar.Dto;

namespace STYS.Kurumlar.Services;

public interface ITenantBrandingService
{
    Task<TenantBrandingDto> GetBrandingAsync(string? host, string? fallbackHost, CancellationToken cancellationToken);
}
