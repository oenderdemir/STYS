using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Kamp.Dto;

namespace STYS.Kamp.Services;

public class KampPuanlamaService : IKampPuanlamaService
{
    private readonly StysAppDbContext _dbContext;

    public KampPuanlamaService(StysAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<KampBasvuruOnizlemeDto> PuanlaAsync(
        KampBasvuruRequestDto request,
        KampBasvuruOnizlemeDto onizleme,
        int kampProgramiId,
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
            .FirstOrDefaultAsync(x => x.AktifMi && x.KampProgramiId == kampProgramiId && x.KampYili == kampYili, cancellationToken);

        if (kuralSeti is null)
        {
            onizleme.Hatalar.Add($"{kampYili} yili icin aktif kamp kural seti bulunamadi.");
            return onizleme;
        }

        var tipKurali = await _dbContext.KampProgramiBasvuruSahibiTipKurallari
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AktifMi && x.KampProgramiId == kampProgramiId && x.KampBasvuruSahibiTipiId == basvuruSahibiTipi.Id, cancellationToken);

        if (tipKurali is null)
        {
            onizleme.Hatalar.Add("Secilen kamp programi icin basvuru sahibi tipi puan kurali bulunamadi.");
            return onizleme;
        }

        var puan = tipKurali.TabanPuan;

        if (tipKurali.HizmetYiliPuaniAktifMi)
        {
            puan += Math.Max(request.HizmetYili, 0);
        }

        if (tipKurali.EmekliBonusPuani > 0)
        {
            puan += tipKurali.EmekliBonusPuani;
        }

        puan += request.Katilimcilar.Count * Math.Max(0, kuralSeti.KatilimciBasinaPuan);

        var dikkateAlinanGecmisYillar = GetDikkateAlinanGecmisYillar(kampYili, kuralSeti.OncekiYilSayisi, gecmisKatilimYillari);
        puan -= dikkateAlinanGecmisYillar.Count * Math.Max(0, kuralSeti.KatilimCezaPuani);

        onizleme.Puan = puan;
        onizleme.OncelikSirasi = tipKurali.OncelikSirasi;
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
