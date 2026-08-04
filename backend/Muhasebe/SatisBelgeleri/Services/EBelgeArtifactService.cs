using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.SatisBelgeleri.Enums;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

/// <summary>Salt-okunur, immutable artefakt DTO'su - byte içeriği kopyasız (ReadOnlyMemory) sunulur.</summary>
public sealed record EBelgeArtifactDto(
    long Id,
    int KurumId,
    int EBelgeKaydiId,
    EBelgeArtifactTipi ArtifactTipi,
    EBelgeArtifactAsamasi ArtifactAsamasi,
    string RuleSetId,
    string KaynakSnapshotSha256,
    string ArtifactSha256,
    ReadOnlyMemory<byte> Icerik,
    string MimeType,
    string DosyaAdi,
    DateTime OlusturulmaZamaniUtc);

/// <summary>
/// Uygulama-içi (henüz controller/download endpoint'i YOK - bkz. Faz 2B.6 görev md.13) salt
/// okunur artefakt erişim servisi. Tenant sınırını ZORUNLU uygular, soft-delete edilmiş
/// artefaktı DÖNDÜRMEZ, genel update/delete operasyonu SUNMAZ.
/// </summary>
public interface IEBelgeArtifactService
{
    Task<EBelgeArtifactDto?> GetUnsignedUblAsync(
        int kurumId,
        int eBelgeKaydiId,
        CancellationToken cancellationToken = default);
}

public sealed class EBelgeArtifactService : IEBelgeArtifactService
{
    private readonly StysAppDbContext _dbContext;

    public EBelgeArtifactService(StysAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<EBelgeArtifactDto?> GetUnsignedUblAsync(
        int kurumId,
        int eBelgeKaydiId,
        CancellationToken cancellationToken = default)
    {
        // Global sorgu filtresi (StysAppDbContext.BuildQueryFilter) zaten IsDeleted=false ve
        // tenant eşleşmesini uygular - burada KurumId ile AÇIKÇA da filtrelemek, filtrenin
        // devre dışı bırakıldığı (ör. süper admin bağlamı) senaryolarda bile tenant sınırının
        // KESİN olmasını sağlar.
        var artifact = await _dbContext.Set<Entities.EBelgeArtifact>()
            .AsNoTracking()
            .Where(a =>
                a.KurumId == kurumId &&
                a.EBelgeKaydiId == eBelgeKaydiId &&
                a.ArtifactTipi == EBelgeArtifactTipi.UblXml &&
                a.ArtifactAsamasi == EBelgeArtifactAsamasi.Unsigned)
            .FirstOrDefaultAsync(cancellationToken);

        if (artifact is null)
        {
            return null;
        }

        return new EBelgeArtifactDto(
            artifact.Id,
            artifact.KurumId,
            artifact.EBelgeKaydiId,
            artifact.ArtifactTipi,
            artifact.ArtifactAsamasi,
            artifact.RuleSetId,
            artifact.KaynakSnapshotSha256,
            artifact.ArtifactSha256,
            artifact.Icerik,
            artifact.MimeType,
            artifact.DosyaAdi,
            artifact.OlusturulmaZamaniUtc);
    }
}
