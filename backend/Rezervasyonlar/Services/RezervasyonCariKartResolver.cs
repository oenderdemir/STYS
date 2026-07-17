using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Rezervasyonlar.Entities;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Rezervasyonlar.Services;

/// <inheritdoc cref="IRezervasyonCariKartResolver" />
public class RezervasyonCariKartResolver : IRezervasyonCariKartResolver
{
    /// <summary>Kullanici tarafindan cari kart secimi gerektigini frontend'e ayirt ettirmek icin
    /// kullanilan HTTP durum kodu (plain 400'lerden ayrismasi icin 422 kullanilir).</summary>
    public const int CariKartSecimiGerekliStatusCode = 422;

    private readonly StysAppDbContext _dbContext;

    public RezervasyonCariKartResolver(StysAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> ResolveAsync(Rezervasyon rezervasyon, int? cariKartIdOverride, CancellationToken cancellationToken = default)
    {
        // Override her zaman en yuksek onceliklidir: rezervasyonun bir ana/varsayilan cari karti
        // olsa bile, cagiran bu belge/odeme icin farkli bir cari secmis olabilir (orn. rezervasyonun
        // bir kismini bir misafir, kalanini baska bir misafir odedi).
        if (cariKartIdOverride.HasValue)
        {
            var secilen = await _dbContext.CariKartlar.FirstOrDefaultAsync(
                x => !x.IsDeleted && x.Id == cariKartIdOverride.Value, cancellationToken);

            if (secilen is null || !secilen.AktifMi)
            {
                throw new BaseException("Secilen cari kart bulunamadi veya pasif durumda.", 400);
            }

            if (secilen.TesisId.HasValue && secilen.TesisId.Value != rezervasyon.TesisId)
            {
                throw new BaseException("Secilen cari kart bu rezervasyonun tesisiyle uyumlu degil.", 400);
            }

            return secilen.Id;
        }

        if (rezervasyon.CariKartId.HasValue)
        {
            return rezervasyon.CariKartId.Value;
        }

        var tcknVeyaTelefonEslesen = await FindEslesenCariKartAsync(rezervasyon, cancellationToken);
        if (tcknVeyaTelefonEslesen.HasValue)
        {
            return tcknVeyaTelefonEslesen.Value;
        }

        var tesis = await _dbContext.Tesisler
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == rezervasyon.TesisId, cancellationToken);

        if (tesis?.RezervasyonMisafirVarsayilanCariKartId is int varsayilanCariKartId)
        {
            var varsayilan = await _dbContext.CariKartlar
                .AsNoTracking()
                .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == varsayilanCariKartId && x.AktifMi, cancellationToken);

            if (varsayilan is not null)
            {
                return varsayilan.Id;
            }
        }

        throw new BaseException(
            "Rezervasyon icin cari kart otomatik belirlenemedi. Lutfen bir cari kart seciniz " +
            "(veya tesis ayarlarindan varsayilan 'Rezervasyon Misafirleri' cari kartini tanimlayiniz).",
            CariKartSecimiGerekliStatusCode);
    }

    private async Task<int?> FindEslesenCariKartAsync(Rezervasyon rezervasyon, CancellationToken cancellationToken)
    {
        var tcknVkn = rezervasyon.TcKimlikNo?.Trim();
        var telefon = rezervasyon.MisafirTelefon?.Trim();
        var adSoyad = rezervasyon.MisafirAdiSoyadi?.Trim();

        var aday = await _dbContext.CariKartlar
            .Where(x => !x.IsDeleted
                        && x.AktifMi
                        && x.TesisId == rezervasyon.TesisId
                        && (x.CariTipi == CariKartTipleri.Musteri || x.CariTipi == CariKartTipleri.KurumsalMusteri)
                        && (
                            (!string.IsNullOrEmpty(tcknVkn) && x.VergiNoTckn == tcknVkn)
                            || (!string.IsNullOrEmpty(telefon) && !string.IsNullOrEmpty(adSoyad)
                                && x.Telefon == telefon && x.UnvanAdSoyad == adSoyad)
                        ))
            // TCKN eslesmesi telefon+ad eslesmesinden daha guvenilir — once onu tercih et.
            .OrderByDescending(x => !string.IsNullOrEmpty(tcknVkn) && x.VergiNoTckn == tcknVkn)
            .FirstOrDefaultAsync(cancellationToken);

        return aday?.Id;
    }
}
