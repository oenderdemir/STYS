using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Kamp.Dto;

namespace STYS.Kamp.Services;

public class KampPuanlamaService : IKampPuanlamaService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IKampParametreService _params;

    public KampPuanlamaService(StysAppDbContext dbContext, IKampParametreService kampParametreService)
    {
        _dbContext = dbContext;
        _params = kampParametreService;
    }

    public async Task<KampBasvuruOnizlemeDto> PuanlaAsync(
        KampBasvuruRequestDto request,
        KampBasvuruOnizlemeDto onizleme,
        int kampYili,
        IReadOnlyCollection<int> gecmisKatilimYillari,
        CancellationToken cancellationToken = default)
    {
        var basvuruSahibiTipi = await _dbContext.KampBasvuruSahibiTipleri
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AktifMi && x.Kod == request.BasvuruSahibiTipi, cancellationToken);

        if (basvuruSahibiTipi is null)
        {
            onizleme.Hatalar.Add("Basvuru sahibi tipi gecersiz.");
            return onizleme;
        }

        var kuralSeti = await _dbContext.KampKuralSetleri
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AktifMi && x.KampYili == kampYili, cancellationToken);

        if (kuralSeti is null)
        {
            onizleme.Hatalar.Add($"{kampYili} yili icin aktif kamp kural seti bulunamadi.");
            return onizleme;
        }

        var puan = basvuruSahibiTipi.TabanPuan;

        if (basvuruSahibiTipi.HizmetYiliPuaniAktifMi)
        {
            puan += Math.Max(request.HizmetYili, 0);
        }

        if (basvuruSahibiTipi.EmekliBonusPuani > 0)
        {
            puan += basvuruSahibiTipi.EmekliBonusPuani;
        }

        puan += request.Katilimcilar.Count * _params.GetInt(KampParametreKodlari.KatilimciBasinaPuan, 10);

        var dikkateAlinanGecmisYillar = GetDikkateAlinanGecmisYillar(kampYili, kuralSeti.OncekiYilSayisi, gecmisKatilimYillari);
        puan -= dikkateAlinanGecmisYillar.Count * Math.Max(0, kuralSeti.KatilimCezaPuani);

        onizleme.Puan = puan;
        onizleme.OncelikSirasi = basvuruSahibiTipi.OncelikSirasi;
        onizleme.GecmisKatilimYillari = gecmisKatilimYillari
            .Where(x => x > 0 && x < kampYili)
            .Distinct()
            .OrderByDescending(x => x)
            .ToList();

        return onizleme;
    }

    private static List<int> GetDikkateAlinanGecmisYillar(int kampYili, int oncekiYilSayisi, IReadOnlyCollection<int> gecmisKatilimYillari)
    {
        var altSinir = kampYili - Math.Max(0, oncekiYilSayisi);
        return gecmisKatilimYillari
            .Where(x => x >= altSinir && x < kampYili)
            .Distinct()
            .ToList();
    }
}
