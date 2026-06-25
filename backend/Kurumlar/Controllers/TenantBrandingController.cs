using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Kurumlar.Dto;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.Kurumlar.Controllers;

public class PublicController : UIController
{
    private readonly StysAppDbContext _stysDbContext;

    public PublicController(StysAppDbContext stysDbContext)
    {
        _stysDbContext = stysDbContext;
    }

    [HttpGet("tenant-branding")]
    [AllowAnonymous]
    public async Task<ActionResult<TenantBrandingDto>> GetTenantBranding(
        [FromQuery] string? host,
        CancellationToken cancellationToken)
    {
        var normalizedHost = NormalizeHost(host) ?? NormalizeHost(Request.Host.Host);
        if (string.IsNullOrWhiteSpace(normalizedHost))
        {
            return Ok(DefaultBranding());
        }

        var kurum = await _stysDbContext.Kurumlar
            .AsNoTracking()
            .Where(x => x.AktifMi && !x.IsDeleted && x.LoginHost != null && x.LoginHost == normalizedHost)
            .FirstOrDefaultAsync(cancellationToken);

        if (kurum is null)
        {
            var subdomainKey = ExtractSubdomainKey(normalizedHost);
            if (!string.IsNullOrWhiteSpace(subdomainKey))
            {
                kurum = await _stysDbContext.Kurumlar
                    .AsNoTracking()
                    .Where(x => x.AktifMi && !x.IsDeleted && x.TenantKey != null && x.TenantKey == subdomainKey)
                    .FirstOrDefaultAsync(cancellationToken);
            }
        }

        if (kurum is null)
        {
            return Ok(DefaultBranding());
        }

        string? logoUrl = null;
        if (!string.IsNullOrWhiteSpace(kurum.LogoDosyaAdi))
        {
            var ticks = kurum.LogoYuklenmeTarihi?.Ticks ?? 0;
            logoUrl = $"/ui/kurum/{kurum.Id}/logo?v={ticks}";
        }

        return Ok(new TenantBrandingDto
        {
            KurumId = kurum.Id,
            TenantKey = kurum.TenantKey,
            KurumAdi = kurum.Ad,
            LogoUrl = logoUrl,
            ApplicationName = "STYS"
        });
    }

    private static TenantBrandingDto DefaultBranding() => new()
    {
        KurumId = null,
        TenantKey = null,
        KurumAdi = "STYS",
        LogoUrl = null,
        ApplicationName = "STYS"
    };

    private static string? NormalizeHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return null;

        var value = host.Trim().ToLowerInvariant();

        var slashIndex = value.IndexOf('/');
        if (slashIndex >= 0)
            value = value[..slashIndex];

        var colonIndex = value.IndexOf(':');
        if (colonIndex >= 0)
            value = value[..colonIndex];

        if (value.StartsWith("www."))
            value = value[4..];

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? ExtractSubdomainKey(string normalizedHost)
    {
        if (string.IsNullOrWhiteSpace(normalizedHost))
            return null;

        if (normalizedHost.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return null;

        var parts = normalizedHost.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
            return null;

        return parts[0];
    }
}
