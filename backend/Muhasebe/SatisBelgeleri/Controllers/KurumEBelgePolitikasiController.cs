using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Identity.Infrastructure.EntityFramework;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri.Controllers;

/// <summary>
/// Faz 2B.10 görev md.17 - kurum e-belge politikası yönetim API'si. Mevcut `KurumController`
/// authorization yaklaşımı AYNEN kullanılır (`ICurrentTenantAccessor.IsSuperAdmin/IsKurumAdmin/
/// GetCurrentKurumId`) - yeni/tahmini bir rol/izin UYDURULMADI, mevcut
/// `StructurePermissions.MuhasebeSatisBelgeleriYonetimi` yetkisi YENİDEN kullanıldı. Entity
/// DOĞRUDAN döndürülmez - yalnız DTO; VKN/kurum kimlik detayı response'a EKLENMEZ.
/// </summary>
[Route("ui/kurumlar/{kurumId:int}/e-belge-politikasi")]
public class KurumEBelgePolitikasiController : UIController
{
    private readonly IEBelgeKurumPolitikaYonetimServisi _yonetimServisi;
    private readonly StysAppDbContext _stysDbContext;
    private readonly TodIdentityDbContext _identityDbContext;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly ICurrentTenantAccessor _currentTenantAccessor;

    public KurumEBelgePolitikasiController(
        IEBelgeKurumPolitikaYonetimServisi yonetimServisi,
        StysAppDbContext stysDbContext,
        TodIdentityDbContext identityDbContext,
        ICurrentUserAccessor currentUserAccessor,
        ICurrentTenantAccessor currentTenantAccessor)
    {
        _yonetimServisi = yonetimServisi;
        _stysDbContext = stysDbContext;
        _identityDbContext = identityDbContext;
        _currentUserAccessor = currentUserAccessor;
        _currentTenantAccessor = currentTenantAccessor;
    }

    [HttpGet]
    [Permission(StructurePermissions.MuhasebeSatisBelgeleriYonetimi.View, TodPlatformAuthorizationConstants.SuperAdminPermission)]
    public async Task<ActionResult<KurumEBelgePolitikasiDto?>> Get(int kurumId, CancellationToken cancellationToken)
    {
        await EnsureCanAccessKurumAsync(kurumId, cancellationToken);

        var politika = await _yonetimServisi.GetirAsync(kurumId, cancellationToken);
        return Ok(politika is null ? null : ToDto(politika));
    }

    [HttpPut]
    [Permission(StructurePermissions.MuhasebeSatisBelgeleriYonetimi.Manage, TodPlatformAuthorizationConstants.SuperAdminPermission)]
    public async Task<ActionResult<KurumEBelgePolitikasiDto>> Update(
        int kurumId,
        [FromBody] KurumEBelgePolitikasiGuncellemeDto request,
        CancellationToken cancellationToken)
    {
        await EnsureCanManageKurumAsync(kurumId, cancellationToken);

        var guncellenen = await _yonetimServisi.GuncelleAsync(kurumId, request, cancellationToken);
        return Ok(ToDto(guncellenen));
    }

    [HttpGet("revizyonlar")]
    [Permission(StructurePermissions.MuhasebeSatisBelgeleriYonetimi.View, TodPlatformAuthorizationConstants.SuperAdminPermission)]
    public async Task<ActionResult<List<KurumEBelgePolitikaRevizyonuDto>>> GetRevizyonlar(int kurumId, CancellationToken cancellationToken)
    {
        await EnsureCanAccessKurumAsync(kurumId, cancellationToken);

        var revizyonlar = await _yonetimServisi.RevizyonlariGetirAsync(kurumId, cancellationToken);
        return Ok(revizyonlar.Select(r => new KurumEBelgePolitikaRevizyonuDto
        {
            EskiSurum = r.EskiSurum,
            YeniSurum = r.YeniSurum,
            EskiYontem = r.EskiYontem,
            YeniYontem = r.YeniYontem,
            EskiAktifMi = r.EskiAktifMi,
            YeniAktifMi = r.YeniAktifMi,
            DegisiklikZamaniUtc = r.DegisiklikZamaniUtc,
            DegisiklikNedeni = r.DegisiklikNedeni,
            DegistirenKullanici = r.CreatedBy,
        }).ToList());
    }

    private static KurumEBelgePolitikasiDto ToDto(KurumEBelgePolitikasi entity) => new()
    {
        KurumId = entity.KurumId,
        EntegrasyonYontemi = entity.EntegrasyonYontemi,
        AktifMi = entity.AktifMi,
        AktivasyonYerelTarihi = entity.AktivasyonYerelTarihi,
        HariciSistemKodu = entity.HariciSistemKodu,
        PolitikaSurumu = entity.PolitikaSurumu,
        RowVersion = Convert.ToBase64String(entity.RowVersion),
    };

    // ──────────────────────────── Authorization (KurumController İLE AYNI desen) ────────────────────────────

    private async Task<List<int>> GetAccessibleKurumIdsAsync(CancellationToken cancellationToken)
    {
        if (_currentTenantAccessor.IsSuperAdmin())
        {
            return await _stysDbContext.Kurumlar.Where(x => !x.IsDeleted).Select(x => x.Id).ToListAsync(cancellationToken);
        }

        var currentUserId = _currentUserAccessor.GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return [];
        }

        return await _identityDbContext.UserKurums
            .Where(x => x.UserId == currentUserId.Value && x.AktifMi)
            .Select(x => x.KurumId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private async Task EnsureCanAccessKurumAsync(int kurumId, CancellationToken cancellationToken)
    {
        var accessibleIds = (await GetAccessibleKurumIdsAsync(cancellationToken)).ToHashSet();
        if (accessibleIds.Count == 0 || !accessibleIds.Contains(kurumId))
        {
            throw new BaseException("Bu kurum için yetkiniz bulunmuyor.", 403);
        }
    }

    private async Task EnsureCanManageKurumAsync(int kurumId, CancellationToken cancellationToken)
    {
        if (_currentTenantAccessor.IsSuperAdmin())
        {
            return;
        }

        var currentKurumId = _currentTenantAccessor.GetCurrentKurumId();
        if (!currentKurumId.HasValue || !_currentTenantAccessor.IsKurumAdmin() || currentKurumId.Value != kurumId)
        {
            throw new BaseException("Bu kurum için yetkiniz bulunmuyor.", 403);
        }

        var exists = await _stysDbContext.Kurumlar.AnyAsync(x => x.Id == kurumId && x.AktifMi && !x.IsDeleted, cancellationToken);
        if (!exists)
        {
            throw new BaseException("Kurum bulunamadı veya aktif değil.", 404);
        }
    }
}
