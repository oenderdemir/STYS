using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.MuhasebeDonemleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Tesisler.Entities;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Belge tipi (ve tesis) geçişlerinin güvenli olduğunu GERÇEK SQL Server'a karşı doğrulayan
/// entegrasyon testleri: (a) BelgeTipi/TesisId değiştiğinde CariKartId yeniden gönderilmese
/// bile mevcut carinin NİHAİ yön/tesisle yeniden doğrulanması (bkz. görev 4), (b) iade referansı
/// kaldırma + Satirlar=[] atomik istisnasının yalnızca NİHAİ belge tipi hâlâ iade tipiyse
/// uygulanması (bkz. görev 3).
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class BelgeTipiGecisleriIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "BLGTIP-442";

    private string _uniqueSuffix = TestMarker;
    private int _kurumId;
    private int _ilId;
    private int _tesisId;
    private int _tesisId2;
    private int _musteriKartId;
    private int _tedarikciKartId;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString))
        {
            return;
        }

        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (kurum, il, tesis) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(dbContext, _uniqueSuffix);
        _kurumId = kurum.Id;
        _ilId = il.Id;
        _tesisId = tesis.Id;

        // Aynı KURUMA ait, AYRI bir ikinci tesis - "cari başka bir tesise ait" senaryosu için
        // (kurum değişmediğinden "başka kuruma taşınamaz" kontrolüyle KARIŞMAZ).
        var tesis2 = new Tesis
        {
            KurumId = kurum.Id, IlId = il.Id, Ad = "Test Tesis 2 " + _uniqueSuffix,
            Telefon = "0000", Adres = "Test Adres 2", AktifMi = true
        };
        dbContext.Tesisler.Add(tesis2);
        await dbContext.SaveChangesAsync();
        _tesisId2 = tesis2.Id;

        var musteriHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "MUS", _tesisId);
        var tedarikciHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "TED", _tesisId);
        dbContext.MuhasebeHesapPlanlari.AddRange(musteriHesap, tedarikciHesap);
        await dbContext.SaveChangesAsync();

        // Müşteri cari, tesise SABİT bağlıdır (TesisId dolu) - tesis-uyumsuzluğu testinde kullanılır.
        var musteriKart = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "MUS", CariKartTipleri.Musteri, _tesisId, musteriHesap.Id);
        var tedarikciKart = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "TED", CariKartTipleri.Tedarikci, _tesisId, tedarikciHesap.Id);
        tedarikciKart.VergiNoTckn = "2222222222";
        dbContext.CariKartlar.AddRange(musteriKart, tedarikciKart);
        await dbContext.SaveChangesAsync();
        _musteriKartId = musteriKart.Id;
        _tedarikciKartId = tedarikciKart.Id;

        dbContext.MuhasebeDonemler.Add(new MuhasebeDonem
        {
            TesisId = _tesisId, MaliYil = 2026, DonemNo = 1,
            BaslangicTarihi = new DateTime(2026, 1, 1), BitisTarihi = new DateTime(2026, 12, 31), KapaliMi = false
        });
        await dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString) || _kurumId <= 0)
        {
            return;
        }

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var belgeIds = await dbContext.SatisBelgeleri.IgnoreQueryFilters().Where(x => x.KurumId == _kurumId).Select(x => x.Id).ToListAsync();
        if (belgeIds.Count > 0)
        {
            await dbContext.SatisBelgeleri.IgnoreQueryFilters().Where(x => belgeIds.Contains(x.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IadeEdilenBelgeId, (int?)null));
            await dbContext.SatisBelgeleri.IgnoreQueryFilters().Where(x => belgeIds.Contains(x.Id)).ExecuteDeleteAsync();
        }

        await dbContext.MuhasebeDonemler.Where(x => x.TesisId == _tesisId).ExecuteDeleteAsync();
        await dbContext.CariKartlar.Where(x => x.TesisId == _tesisId).ExecuteDeleteAsync();
        await dbContext.MuhasebeHesapPlanlari.Where(x => x.Kod != null && x.Kod.Contains(_uniqueSuffix)).ExecuteDeleteAsync();
        await dbContext.Tesisler.Where(x => x.Id == _tesisId || x.Id == _tesisId2).ExecuteDeleteAsync();
        await dbContext.Iller.Where(x => x.Id == _ilId).ExecuteDeleteAsync();
        await dbContext.Kurumlar.Where(x => x.Id == _kurumId).ExecuteDeleteAsync();
    }

    // ─────────────────────────────────────────────────────────────
    // Görev 4: BelgeTipi/TesisId değişince mevcut cari yeniden doğrulanır
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task UpdateAsync_BelgeYonuDegisirCariKartIdGonderilmezse_UyumsuzTedarikciCarisiReddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var created = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.AlisFaturasi,
            TesisId = _tesisId,
            CariKartId = _tedarikciKartId,
            BelgeTarihi = new DateTime(2026, 3, 1),
            MusteriAdSoyad = "Tedarikci",
            KarsiTarafFaturaNo = $"TED-{Guid.NewGuid():N}"[..20],
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Test", Miktar = 5m, BirimFiyat = 100m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        });

        // Yalnızca BelgeTipi satış yönüne değiştirilir - CariKartId HİÇ gönderilmez; mevcut
        // tedarikçi carisi artık satış yönüne UYGUN DEĞİLDİR ve reddedilmelidir (bkz. görev 4).
        var ex = await Assert.ThrowsAsync<BaseException>(() => service.UpdateAsync(created.Id!.Value, new UpdateSatisBelgesiRequest
        {
            BelgeTipi = SatisBelgesiTipi.SatisFaturasi
        }));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Contains("müşteri tipli", ex.Message);
    }

    [IntegrationFact]
    public async Task UpdateAsync_TesisDegisirCariKartIdGonderilmezse_BaskaTesisinCarisiReddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var created = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
            TesisId = _tesisId,
            CariKartId = _musteriKartId,
            BelgeTarihi = new DateTime(2026, 3, 1),
            MusteriAdSoyad = "Musteri",
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Test", Miktar = 5m, BirimFiyat = 100m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        });

        // Yalnızca TesisId (aynı kurum içindeki İKİNCİ tesise) değiştirilir - CariKartId HİÇ
        // gönderilmez; mevcut müşteri carisi TesisId'ye SABİT bağlı olduğundan (farklı tesis)
        // reddedilmelidir (bkz. görev 4).
        var ex = await Assert.ThrowsAsync<BaseException>(() => service.UpdateAsync(created.Id!.Value, new UpdateSatisBelgesiRequest
        {
            TesisId = _tesisId2
        }));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Contains("tesisiyle uyumlu değil", ex.Message);
    }

    [IntegrationFact]
    public async Task UpdateAsync_UyumluBelgeTipiGecisi_MevcutCariYenidenDogrulanirVeKorunur()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var created = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
            TesisId = _tesisId,
            CariKartId = _musteriKartId,
            BelgeTarihi = new DateTime(2026, 3, 1),
            MusteriAdSoyad = "Musteri",
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Test", Miktar = 5m, BirimFiyat = 100m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        });

        // FaturaTaslagi -> Proforma: İKİSİ de "satış" yönü sayılır (IsAlisBelgesi=false) -
        // mevcut müşteri carisi UYUMLU kalır, CariKartId gönderilmese de güncelleme başarılı olmalı.
        var guncellenen = await service.UpdateAsync(created.Id!.Value, new UpdateSatisBelgesiRequest
        {
            BelgeTipi = SatisBelgesiTipi.Proforma
        });

        Assert.Equal(_musteriKartId, guncellenen.CariKartId);
        Assert.Equal(SatisBelgesiTipi.Proforma, guncellenen.BelgeTipi);
    }

    // ─────────────────────────────────────────────────────────────
    // Görev 3: Referans kaldırma + Satirlar=[] istisnası yalnızca NİHAİ tip iade tipiyse uygulanır
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task UpdateAsync_ReferansKaldirVeSatirlarBosAmaNihaiTipNormalOluyorsa_400DonerBosBelgeOlusturulmaz()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var asilFatura = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
            TesisId = _tesisId,
            CariKartId = _musteriKartId,
            BelgeTarihi = new DateTime(2026, 3, 1),
            MusteriAdSoyad = "Musteri",
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Asil", Miktar = 10m, BirimFiyat = 100m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        });
        var asilSatirId = asilFatura.Satirlar[0].Id!.Value;

        var iade = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.SatisIadeFaturasi,
            TesisId = _tesisId,
            CariKartId = _musteriKartId,
            BelgeTarihi = new DateTime(2026, 3, 5),
            MusteriAdSoyad = "Musteri",
            KarsiTarafFaturaNo = $"MUS-{Guid.NewGuid():N}"[..20],
            IadeEdilenBelgeId = asilFatura.Id,
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Iade", Miktar = 4m, BirimFiyat = 100m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m,
                    KaynakSatirId = asilSatirId.ToString()
                }
            ]
        });

        // AYNI istekte hem BelgeTipi NORMAL bir tipe (SatisFaturasi) çevriliyor HEM DE referans
        // kaldırma + Satirlar=[] gönderiliyor - NİHAİ tip artık iade tipi OLMADIĞINDAN atomik
        // istisna UYGULANMAMALI, bu bir 400 ile reddedilmeli (boş satırlı bir "normal" belge
        // sessizce OLUŞTURULMAMALI, bkz. görev 3).
        var ex = await Assert.ThrowsAsync<BaseException>(() => service.UpdateAsync(iade.Id!.Value, new UpdateSatisBelgesiRequest
        {
            BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
            IadeEdilenBelgeReferansiKaldir = true,
            Satirlar = []
        }));

        Assert.Equal(400, ex.ErrorCode);

        var sonHal = await dbContext.SatisBelgeleri.AsNoTracking().Include(x => x.Satirlar).FirstAsync(x => x.Id == iade.Id);
        Assert.Equal(SatisBelgesiTipi.SatisIadeFaturasi, sonHal.BelgeTipi);
        Assert.Equal(asilFatura.Id, sonHal.IadeEdilenBelgeId);
        Assert.Contains(sonHal.Satirlar, x => !x.IsDeleted);
    }
}
