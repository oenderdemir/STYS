using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using STYS.Muhasebe.MuhasebeHesapBakiyeleri.Entities;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.MuhasebeHesapBakiyeleri.Services;

/// <summary>
/// Bu servis OnaylaAsync/IptalEtAsync akışlarında yalnızca bir kez çağrılmalıdır.
/// İleride idempotency için fiş üzerinde BakiyeIslendiMi alanı veya bakiye hareket
/// log tablosu eklenebilir.
/// </summary>
public class MuhasebeHesapBakiyeGuncellemeService
    : IMuhasebeHesapBakiyeGuncellemeService
{
    private readonly StysAppDbContext _dbContext;

    public MuhasebeHesapBakiyeGuncellemeService(StysAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task FisBakiyeleriniIsleAsync(
        MuhasebeFis fis,
        CancellationToken cancellationToken = default)
    {
        if (fis.Durum != MuhasebeFisDurumlari.Onayli
            && fis.Durum != MuhasebeFisDurumlari.TersKayit)
        {
            throw new BaseException(
                "Muhasebe bakiyesi yalnızca onaylı veya ters kayıt fişleri için güncellenebilir.",
                400);
        }

        // 1. Aktif satırları al
        var aktifSatirlar = fis.Satirlar.Where(x => !x.IsDeleted).ToList();

        if (aktifSatirlar.Count == 0)
            return;

        // 2. Satırlardaki hesap ID'lerinden hesapları DB'den çek
        var hesapIds = aktifSatirlar
            .Select(x => x.MuhasebeHesapPlaniId)
            .Distinct()
            .ToList();

        var hareketHesaplari = await _dbContext.MuhasebeHesapPlanlari
            .Where(x => hesapIds.Contains(x.Id) && !x.IsDeleted && x.AktifMi)
            .ToListAsync(cancellationToken);

        // 3. Her satır için hesap bulunamazsa hata ver
        foreach (var satir in aktifSatirlar)
        {
            var hesap = hareketHesaplari
                .FirstOrDefault(x => x.Id == satir.MuhasebeHesapPlaniId);

            if (hesap is null)
                throw new BaseException(
                    $"Fiş satırındaki muhasebe hesabı bulunamadı veya aktif değil.",
                    400);
        }

        // 4. Üst hesap kodlarını çıkar
        var ustKodlar = hareketHesaplari
            .SelectMany(x => GetUstHesapKodlari(x.TamKod))
            .Distinct()
            .ToList();

        Dictionary<string, MuhasebeHesapPlani>? ustHesapLookup = null;

        if (ustKodlar.Count > 0)
        {
            var ustHesapListesi = await _dbContext.MuhasebeHesapPlanlari
                .Where(x => ustKodlar.Contains(x.TamKod) && !x.IsDeleted && x.AktifMi)
                .ToListAsync(cancellationToken);

            // Her TamKod için en uygun kaydı seç:
            // 1. TesisId == fis.TesisId
            // 2. TesisId == null
            // 3. İlk aktif kayıt
            ustHesapLookup = ustKodlar.ToDictionary(
                kod => kod,
                kod =>
                {
                    var tesisli = ustHesapListesi
                        .FirstOrDefault(x => x.TamKod == kod && x.TesisId == fis.TesisId);

                    if (tesisli is not null)
                        return tesisli;

                    var genel = ustHesapListesi
                        .FirstOrDefault(x => x.TamKod == kod && x.TesisId == null);

                    if (genel is not null)
                        return genel;

                    return ustHesapListesi.First(x => x.TamKod == kod);
                });
        }

        // 5. Her satır için bakiye işle
        foreach (var satir in aktifSatirlar)
        {
            var hesap = hareketHesaplari.First(x => x.Id == satir.MuhasebeHesapPlaniId);

            // A. Gerçek hareket hesabı (KonsolideMi = false)
            await BakiyeSatiriArtirAsync(
                fis.TesisId,
                fis.MaliYil,
                fis.Donem,
                hesap,
                konsolideMi: false,
                borc: satir.Borc,
                alacak: satir.Alacak,
                cancellationToken);

            // B. Üst hesaplar (KonsolideMi = true)
            var hesapUstKodlari = GetUstHesapKodlari(hesap.TamKod);

            foreach (var ustKod in hesapUstKodlari)
            {
                if (ustHesapLookup is null || !ustHesapLookup.TryGetValue(ustKod, out var ustHesap))
                    continue;

                await BakiyeSatiriArtirAsync(
                    fis.TesisId,
                    fis.MaliYil,
                    fis.Donem,
                    ustHesap,
                    konsolideMi: true,
                    borc: satir.Borc,
                    alacak: satir.Alacak,
                    cancellationToken);
            }
        }
    }

    private async Task BakiyeSatiriArtirAsync(
        int tesisId,
        int maliYil,
        int donem,
        MuhasebeHesapPlani hesap,
        bool konsolideMi,
        decimal borc,
        decimal alacak,
        CancellationToken cancellationToken)
    {
        // Mevcut kayıt var mı?
        var mevcut = await _dbContext.MuhasebeHesapBakiyeleri
            .FirstOrDefaultAsync(x =>
                x.TesisId == tesisId
                && x.MaliYil == maliYil
                && x.Donem == donem
                && x.MuhasebeHesapPlaniId == hesap.Id
                && x.KonsolideMi == konsolideMi
                && !x.IsDeleted,
                cancellationToken);

        if (mevcut is not null)
        {
            // Mevcut kaydı güncelle
            mevcut.BorcToplam += borc;
            mevcut.AlacakToplam += alacak;

            var net = mevcut.BorcToplam - mevcut.AlacakToplam;
            mevcut.BorcBakiye = net > 0 ? net : 0;
            mevcut.AlacakBakiye = net < 0 ? Math.Abs(net) : 0;

            mevcut.HesapKodu = hesap.TamKod;
            mevcut.HesapAdi = hesap.Ad;
            mevcut.SonGuncellemeTarihi = DateTime.UtcNow;
        }
        else
        {
            // Yeni kayıt oluştur
            var net = borc - alacak;

            var yeni = new MuhasebeHesapBakiye
            {
                TesisId = tesisId,
                MaliYil = maliYil,
                Donem = donem,
                MuhasebeHesapPlaniId = hesap.Id,
                HesapKodu = hesap.TamKod,
                HesapAdi = hesap.Ad,
                KonsolideMi = konsolideMi,
                BorcToplam = borc,
                AlacakToplam = alacak,
                BorcBakiye = net > 0 ? net : 0,
                AlacakBakiye = net < 0 ? Math.Abs(net) : 0,
                SonGuncellemeTarihi = DateTime.UtcNow,
            };

            await _dbContext.MuhasebeHesapBakiyeleri.AddAsync(yeni, cancellationToken);
        }
    }

    /// <summary>
    /// Nokta ile ayrılmış tam hesap kodundan üst hesap kodlarını türetir.
    /// Örn: "150.01.001" → ["150", "150.01"]
    /// Örn: "150" → []
    /// </summary>
    private static List<string> GetUstHesapKodlari(string tamKod)
    {
        var result = new List<string>();

        if (string.IsNullOrWhiteSpace(tamKod))
            return result;

        var parts = tamKod.Split('.', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length <= 1)
            return result;

        for (var i = 1; i < parts.Length; i++)
        {
            result.Add(string.Join('.', parts.Take(i)));
        }

        return result;
    }
}
