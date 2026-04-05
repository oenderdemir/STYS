using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Kamp.Dto;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Kamp.Services;

public class KampTahsisService : IKampTahsisService
{
    private readonly StysAppDbContext _dbContext;

    public KampTahsisService(StysAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<KampTahsisBaglamDto> GetBaglamAsync(CancellationToken cancellationToken = default)
    {
        var donemler = await _dbContext.KampDonemleri
            .OrderByDescending(x => x.Yil)
            .ThenBy(x => x.KonaklamaBaslangicTarihi)
            .Select(x => new KampTahsisDonemSecenekDto
            {
                Id = x.Id,
                Ad = x.Ad
            })
            .ToListAsync(cancellationToken);

        var tesisler = await _dbContext.Tesisler
            .OrderBy(x => x.Ad)
            .Select(x => new KampTahsisTesisSecenekDto
            {
                Id = x.Id,
                Ad = x.Ad
            })
            .ToListAsync(cancellationToken);

        return new KampTahsisBaglamDto
        {
            Donemler = donemler,
            Tesisler = tesisler,
            Durumlar = KampBasvuruDurumlari.Hepsi.ToList()
        };
    }

    public async Task<List<KampTahsisListeDto>> GetListeAsync(KampTahsisFilterDto filter, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.KampBasvurulari
            .AsNoTracking()
            .Include(x => x.KampDonemi)
            .Include(x => x.Tesis)
            .AsQueryable();

        if (filter.KampDonemiId.HasValue && filter.KampDonemiId.Value > 0)
        {
            query = query.Where(x => x.KampDonemiId == filter.KampDonemiId.Value);
        }

        if (filter.TesisId.HasValue && filter.TesisId.Value > 0)
        {
            query = query.Where(x => x.TesisId == filter.TesisId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Durum))
        {
            query = query.Where(x => x.Durum == filter.Durum);
        }

        var basvurular = await query
            .OrderBy(x => x.KampDonemiId)
            .ThenBy(x => x.TesisId)
            .ThenBy(x => x.OncelikSirasi)
            .ThenByDescending(x => x.Puan)
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var atamalar = await _dbContext.KampDonemiTesisleri
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var tahsisSayilari = await _dbContext.KampBasvurulari
            .AsNoTracking()
            .Where(x => x.Durum == KampBasvuruDurumlari.TahsisEdildi)
            .GroupBy(x => new { x.KampDonemiId, x.TesisId })
            .Select(x => new
            {
                x.Key.KampDonemiId,
                x.Key.TesisId,
                Count = x.Count()
            })
            .ToListAsync(cancellationToken);

        var kontenjanMap = atamalar.ToDictionary(x => (x.KampDonemiId, x.TesisId), x => x.ToplamKontenjan);
        var tahsisMap = tahsisSayilari.ToDictionary(x => (x.KampDonemiId, x.TesisId), x => x.Count);
        var siralamaCounter = new Dictionary<(int KampDonemiId, int TesisId), int>();
        var result = new List<KampTahsisListeDto>(basvurular.Count);

        foreach (var item in basvurular)
        {
            var key = (item.KampDonemiId, item.TesisId);
            var toplamKontenjan = kontenjanMap.GetValueOrDefault(key);
            var tahsisEdilenSayisi = tahsisMap.GetValueOrDefault(key);
            var siralama = siralamaCounter.TryGetValue(key, out var currentSiralama) ? currentSiralama + 1 : 1;
            siralamaCounter[key] = siralama;

            result.Add(new KampTahsisListeDto
            {
                Id = item.Id,
                Siralama = siralama,
                KampDonemiId = item.KampDonemiId,
                KampDonemiAd = item.KampDonemi?.Ad ?? string.Empty,
                TesisId = item.TesisId,
                TesisAd = item.Tesis?.Ad ?? string.Empty,
                BasvuruSahibiAdiSoyadi = item.BasvuruSahibiAdiSoyadi,
                BasvuruSahibiTipi = item.BasvuruSahibiTipi,
                KonaklamaBirimiTipi = item.KonaklamaBirimiTipi,
                Durum = item.Durum,
                KatilimciSayisi = item.KatilimciSayisi,
                OncelikSirasi = item.OncelikSirasi,
                Puan = item.Puan,
                DonemToplamTutar = item.DonemToplamTutar,
                AvansToplamTutar = item.AvansToplamTutar,
                ToplamKontenjan = toplamKontenjan,
                TahsisEdilenSayisi = tahsisEdilenSayisi,
                KalanKontenjan = Math.Max(0, toplamKontenjan - tahsisEdilenSayisi),
                CreatedAt = item.CreatedAt ?? item.UpdatedAt ?? DateTime.MinValue,
                Uyarilar = TryDeserializeWarnings(item.UyariMesajlariJson)
            });
        }

        return result;
    }

    public async Task KararVerAsync(int kampBasvuruId, KampTahsisKararRequestDto request, CancellationToken cancellationToken = default)
    {
        if (!KampBasvuruDurumlari.TahsisKararlari.Contains(request.Durum))
        {
            throw new BaseException("Tahsis karari gecersiz.", 400);
        }

        var entity = await _dbContext.KampBasvurulari.FirstOrDefaultAsync(x => x.Id == kampBasvuruId, cancellationToken)
            ?? throw new BaseException("Kamp basvurusu bulunamadi.", 404);

        if (entity.Durum == KampBasvuruDurumlari.IptalEdildi || entity.Durum == KampBasvuruDurumlari.Reddedildi)
        {
            throw new BaseException("Iptal edilen veya reddedilen basvuru icin tahsis karari degistirilemez.", 400);
        }

        if (request.Durum == KampBasvuruDurumlari.TahsisEdildi)
        {
            var atama = await _dbContext.KampDonemiTesisleri
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.KampDonemiId == entity.KampDonemiId && x.TesisId == entity.TesisId, cancellationToken)
                ?? throw new BaseException("Basvuru icin kamp donemi tesis atamasi bulunamadi.", 400);

            var mevcutTahsisSayisi = await _dbContext.KampBasvurulari.CountAsync(
                x => x.KampDonemiId == entity.KampDonemiId
                    && x.TesisId == entity.TesisId
                    && x.Durum == KampBasvuruDurumlari.TahsisEdildi
                    && x.Id != entity.Id,
                cancellationToken);

            if (mevcutTahsisSayisi >= atama.ToplamKontenjan)
            {
                throw new BaseException("Secilen tesis ve donem icin tahsis kontenjani dolu.", 400);
            }
        }

        entity.Durum = request.Durum;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<KampTahsisOtomatikKararSonucDto> OtomatikKararUygulaAsync(
        KampTahsisOtomatikKararRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var atama = await _dbContext.KampDonemiTesisleri
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.KampDonemiId == request.KampDonemiId && x.TesisId == request.TesisId,
                cancellationToken)
            ?? throw new BaseException("Secilen kamp donemi ve tesis icin atama bulunamadi.", 404);

        var toplamKontenjan = Math.Max(0, atama.ToplamKontenjan);
        var adaylar = await _dbContext.KampBasvurulari
            .Where(x => x.KampDonemiId == request.KampDonemiId
                && x.TesisId == request.TesisId
                && x.Durum != KampBasvuruDurumlari.IptalEdildi
                && x.Durum != KampBasvuruDurumlari.Reddedildi)
            .OrderBy(x => x.OncelikSirasi)
            .ThenByDescending(x => x.Puan)
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var tahsisEdilenSayisi = 0;
        var tahsisEdilemeyenSayisi = 0;
        var guncellenenKayitSayisi = 0;

        for (var i = 0; i < adaylar.Count; i++)
        {
            var hedefDurum = i < toplamKontenjan
                ? KampBasvuruDurumlari.TahsisEdildi
                : KampBasvuruDurumlari.TahsisEdilemedi;

            if (hedefDurum == KampBasvuruDurumlari.TahsisEdildi)
            {
                tahsisEdilenSayisi++;
            }
            else
            {
                tahsisEdilemeyenSayisi++;
            }

            if (adaylar[i].Durum == hedefDurum)
            {
                continue;
            }

            adaylar[i].Durum = hedefDurum;
            guncellenenKayitSayisi++;
        }

        if (guncellenenKayitSayisi > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new KampTahsisOtomatikKararSonucDto
        {
            KampDonemiId = request.KampDonemiId,
            TesisId = request.TesisId,
            ToplamKontenjan = toplamKontenjan,
            DegerlendirilenBasvuruSayisi = adaylar.Count,
            TahsisEdilenSayisi = tahsisEdilenSayisi,
            TahsisEdilemeyenSayisi = tahsisEdilemeyenSayisi,
            GuncellenenKayitSayisi = guncellenenKayitSayisi
        };
    }

    public async Task<KampNoShowIptalSonucDto> NoShowIptalUygulaAsync(int kampDonemiId, CancellationToken cancellationToken = default)
    {
        var donem = await _dbContext.KampDonemleri
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == kampDonemiId, cancellationToken)
            ?? throw new BaseException("Kamp donemi bulunamadi.", 404);

        // Talimat: "donemin baslamasindan itibaren ikinci gunun sonuna kadar kampa katilmayanlarin tahsisi iptal edilecektir"
        // Bu islem ancak donem basladiktan 2 gun sonra calistiriabilir.
        var noShowSinirTarihi = donem.KonaklamaBaslangicTarihi.Date.AddDays(2);
        if (DateTime.UtcNow.Date < noShowSinirTarihi)
        {
            throw new BaseException($"No-show iptali, kamp doneminin baslamasindan en az 2 gun sonra uygulanabilir. Erken tarih: {noShowSinirTarihi:dd.MM.yyyy}", 400);
        }

        // TahsisEdildi durumundaki ama henuz rezervasyonu olusturulmamis basvurulari bul
        // Rezervasyonu olan = kampa katilmis sayilir
        var tahsisliBasvurular = await _dbContext.KampBasvurulari
            .Where(x => x.KampDonemiId == kampDonemiId
                && x.Durum == KampBasvuruDurumlari.TahsisEdildi)
            .ToListAsync(cancellationToken);

        var rezervasyonluBasvuruIdler = await _dbContext.KampRezervasyonlari
            .AsNoTracking()
            .Where(x => x.KampDonemiId == kampDonemiId
                && x.Durum == KampRezervasyonDurumlari.Aktif)
            .Select(x => x.KampBasvuruId)
            .ToListAsync(cancellationToken);

        var rezervasyonluSet = new HashSet<int>(rezervasyonluBasvuruIdler);
        var iptalEdilecekler = tahsisliBasvurular
            .Where(x => !rezervasyonluSet.Contains(x.Id))
            .ToList();

        foreach (var basvuru in iptalEdilecekler)
        {
            basvuru.Durum = KampBasvuruDurumlari.IptalEdildi;
        }

        if (iptalEdilecekler.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new KampNoShowIptalSonucDto
        {
            KampDonemiId = kampDonemiId,
            KampDonemiAd = donem.Ad,
            DegerlendirilenBasvuruSayisi = tahsisliBasvurular.Count,
            IptalEdilenSayisi = iptalEdilecekler.Count
        };
    }

    private static List<string> TryDeserializeWarnings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
