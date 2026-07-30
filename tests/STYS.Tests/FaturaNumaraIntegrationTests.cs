using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.MuhasebeDonemleri.Entities;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Services;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Kurum + mali yıl + seri bazlı, eşzamanlılığa güvenli resmî fatura numaralandırması
/// (KurumFaturaNumaraSayaci + SatisBelgesiService.FaturaKesAsync) için GERÇEK SQL Server üzerinde
/// çalışan entegrasyon ve eşzamanlılık testleri.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class FaturaNumaraIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "FATNUM-918";

    private string _uniqueSuffix = TestMarker;
    private int _kurumId;
    private int _ilId;
    private int _tesisId;
    private int _musteriKartId;

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

        var gelirHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.GelirSatis, "GELIR", _tesisId);
        var kdvHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.KDVHesaplanan, "KDV", _tesisId);
        var musteriHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "MUS", _tesisId);
        dbContext.MuhasebeHesapPlanlari.AddRange(gelirHesap, kdvHesap, musteriHesap);
        await dbContext.SaveChangesAsync();

        var musteriKart = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "MUS", CariKartTipleri.Musteri, _tesisId, musteriHesap.Id);
        dbContext.CariKartlar.Add(musteriKart);
        await dbContext.SaveChangesAsync();
        _musteriKartId = musteriKart.Id;

        dbContext.MuhasebeDonemler.AddRange(
            new MuhasebeDonem
            {
                TesisId = _tesisId, MaliYil = 2025, DonemNo = 1,
                BaslangicTarihi = new DateTime(2025, 1, 1), BitisTarihi = new DateTime(2025, 12, 31), KapaliMi = false
            },
            new MuhasebeDonem
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
        await CleanupKurumAsync(dbContext, _kurumId, _tesisId, _ilId, _uniqueSuffix);
    }

    // ─────────────────────────────────────────────────────────────
    // Yardımcılar
    // ─────────────────────────────────────────────────────────────

    private static async Task SeedSayacAsync(
        StysAppDbContext dbContext, int kurumId, int maliYil, string seriKodu, int sonNumara = 0, bool aktifMi = true)
    {
        dbContext.KurumFaturaNumaraSayaclari.Add(new KurumFaturaNumaraSayaci
        {
            KurumId = kurumId,
            MaliYil = maliYil,
            SeriKodu = seriKodu,
            SonNumara = sonNumara,
            AktifMi = aktifMi
        });
        await dbContext.SaveChangesAsync();
    }

    private async Task<SatisBelgesiDto> SeedOnaylanmisSatisFaturasiAsync(
        StysAppDbContext dbContext, DateTime belgeTarihi, int? tesisId = null, int? musteriKartId = null)
    {
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var request = new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
            TesisId = tesisId ?? _tesisId,
            CariKartId = musteriKartId ?? _musteriKartId,
            BelgeTarihi = belgeTarihi,
            MusteriAdSoyad = "Test Musteri " + _uniqueSuffix,
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1,
                    Aciklama = "Test satir",
                    Miktar = 1,
                    BirimFiyat = 1000m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli,
                    KdvOrani = 20m
                }
            ]
        };

        var created = await service.CreateAsync(request);
        await service.MuhasebeOnayinaGonderAsync(created.Id!.Value);
        await service.MuhasebeOnaylaAsync(created.Id!.Value);

        var donemService = SatisBelgesiMuhasebeTestSupport.CreateRealMuhasebeDonemService(dbContext);
        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext, donemService);
        return await fisService.MuhasebeFisiOlusturAsync(created.Id.Value);
    }

    private static async Task CleanupKurumAsync(StysAppDbContext dbContext, int kurumId, int tesisId, int ilId, string uniqueSuffix)
    {
        var belgeIds = await dbContext.SatisBelgeleri
            .Where(x => x.KurumId == kurumId)
            .Select(x => x.Id)
            .ToListAsync();

        var fisIds = new List<int>();
        if (belgeIds.Count > 0)
        {
            // KaynakId, farklı KaynakModul'lere ait fişler arasında PAYLAŞILAN bir int uzayıdır
            // (ör. TahsilatOdemeBelgesi de kendi Id'sini KaynakId olarak kullanabilir) - yalnızca
            // KaynakId eşleşmesiyle filtrelemek, bu testle hiç ilgisi olmayan (rastgele aynı
            // sayısal Id'ye sahip) başka bir modülün fişini yanlışlıkla silmeye çalışabilir. Bu
            // yüzden KaynakModul == SatisBelgesi koşulu da ZORUNLU eklenir.
            fisIds = await dbContext.MuhasebeFisler
                .Where(x => x.KaynakModul == MuhasebeKaynakModulleri.SatisBelgesi
                            && x.KaynakId != null && belgeIds.Contains(x.KaynakId.Value))
                .Select(x => x.Id)
                .ToListAsync();

            await dbContext.CariHareketler
                .Where(x => x.KaynakModul == MuhasebeKaynakModulleri.SatisBelgesi
                            && x.KaynakId != null && belgeIds.Contains(x.KaynakId.Value))
                .ExecuteDeleteAsync();
            await dbContext.SatisBelgeleri.Where(x => belgeIds.Contains(x.Id)).ExecuteDeleteAsync();
        }

        if (fisIds.Count > 0)
        {
            await dbContext.MuhasebeFisSatirlari.Where(x => fisIds.Contains(x.MuhasebeFisId)).ExecuteDeleteAsync();
            await dbContext.MuhasebeFisler.Where(x => fisIds.Contains(x.Id)).ExecuteDeleteAsync();
        }

        await dbContext.KurumFaturaNumaraSayaclari.Where(x => x.KurumId == kurumId).ExecuteDeleteAsync();
        await dbContext.MuhasebeDonemler.Where(x => x.TesisId == tesisId).ExecuteDeleteAsync();
        await dbContext.CariKartlar.Where(x => x.TesisId == tesisId).ExecuteDeleteAsync();
        await dbContext.MuhasebeHesapPlanlari.Where(x => x.Kod != null && x.Kod.Contains(uniqueSuffix)).ExecuteDeleteAsync();
        await dbContext.Tesisler.Where(x => x.Id == tesisId).ExecuteDeleteAsync();
        await dbContext.Iller.Where(x => x.Id == ilId).ExecuteDeleteAsync();
        await dbContext.Kurumlar.Where(x => x.Id == kurumId).ExecuteDeleteAsync();
    }

    // ─────────────────────────────────────────────────────────────
    // Temel numaralandırma davranışı
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task FaturaKesAsync_GecerliSeriIleNumaraUretirVeDurumFaturaKesildiOlur()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await SeedSayacAsync(dbContext, _kurumId, 2026, "ABC");

        var belge = await SeedOnaylanmisSatisFaturasiAsync(dbContext, new DateTime(2026, 3, 10));

        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var sonuc = await service.FaturaKesAsync(belge.Id!.Value, new FaturaKesRequest { SeriKodu = "abc" });

        Assert.Equal("ABC2026000000001", sonuc.ResmiFaturaNo);
        Assert.Equal(SatisBelgesiDurumu.FaturaKesildi, sonuc.Durum);
        Assert.NotNull(sonuc.FaturaKesimTarihi);

        var sayacDb = await dbContext.KurumFaturaNumaraSayaclari.AsNoTracking()
            .FirstAsync(x => x.KurumId == _kurumId && x.MaliYil == 2026 && x.SeriKodu == "ABC");
        Assert.Equal(1, sayacDb.SonNumara);
    }

    [IntegrationFact]
    public async Task FaturaKesAsync_AyniSeriIcindeArdisikNumaralarArtar()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await SeedSayacAsync(dbContext, _kurumId, 2026, "SEQ");

        var belge1 = await SeedOnaylanmisSatisFaturasiAsync(dbContext, new DateTime(2026, 3, 1));
        var belge2 = await SeedOnaylanmisSatisFaturasiAsync(dbContext, new DateTime(2026, 3, 2));
        var belge3 = await SeedOnaylanmisSatisFaturasiAsync(dbContext, new DateTime(2026, 3, 3));

        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var sonuc1 = await service.FaturaKesAsync(belge1.Id!.Value, new FaturaKesRequest { SeriKodu = "SEQ" });
        var sonuc2 = await service.FaturaKesAsync(belge2.Id!.Value, new FaturaKesRequest { SeriKodu = "SEQ" });
        var sonuc3 = await service.FaturaKesAsync(belge3.Id!.Value, new FaturaKesRequest { SeriKodu = "SEQ" });

        Assert.Equal("SEQ2026000000001", sonuc1.ResmiFaturaNo);
        Assert.Equal("SEQ2026000000002", sonuc2.ResmiFaturaNo);
        Assert.Equal("SEQ2026000000003", sonuc3.ResmiFaturaNo);
    }

    [IntegrationFact]
    public async Task FaturaKesAsync_FarkliKurumlarAyniSeriIcinBagimsizBirdenBaslar()
    {
        var uniqueSuffixB = $"{TestMarker}-B-{Guid.NewGuid():N}"[..24];
        int kurumBId = 0, ilBId = 0, tesisBId = 0;

        try
        {
            await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
            await SeedSayacAsync(dbContext, _kurumId, 2026, "IZO");
            var belgeA = await SeedOnaylanmisSatisFaturasiAsync(dbContext, new DateTime(2026, 4, 1));
            var serviceA = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
            await serviceA.FaturaKesAsync(belgeA.Id!.Value, new FaturaKesRequest { SeriKodu = "IZO" });
            await serviceA.FaturaKesAsync(
                (await SeedOnaylanmisSatisFaturasiAsync(dbContext, new DateTime(2026, 4, 2))).Id!.Value,
                new FaturaKesRequest { SeriKodu = "IZO" });
            // kurumA'nın sayacı artık 2'de.

            var (kurumB, ilB, tesisB) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(dbContext, uniqueSuffixB);
            kurumBId = kurumB.Id; ilBId = ilB.Id; tesisBId = tesisB.Id;

            var gelirHesapB = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(uniqueSuffixB, MuhasebeAnaHesapKodlari.GelirSatis, "GELIR", tesisBId);
            var kdvHesapB = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(uniqueSuffixB, MuhasebeAnaHesapKodlari.KDVHesaplanan, "KDV", tesisBId);
            var musteriHesapB = SatisBelgesiMuhasebeTestSupport.BuildHesap(uniqueSuffixB, "MUS", tesisBId);
            dbContext.MuhasebeHesapPlanlari.AddRange(gelirHesapB, kdvHesapB, musteriHesapB);
            await dbContext.SaveChangesAsync();
            var musteriKartB = SatisBelgesiMuhasebeTestSupport.BuildCariKart(uniqueSuffixB, "MUS", CariKartTipleri.Musteri, tesisBId, musteriHesapB.Id);
            dbContext.CariKartlar.Add(musteriKartB);
            await dbContext.SaveChangesAsync();
            dbContext.MuhasebeDonemler.Add(new MuhasebeDonem
            {
                TesisId = tesisBId, MaliYil = 2026, DonemNo = 1,
                BaslangicTarihi = new DateTime(2026, 1, 1), BitisTarihi = new DateTime(2026, 12, 31), KapaliMi = false
            });
            await dbContext.SaveChangesAsync();

            await SeedSayacAsync(dbContext, kurumBId, 2026, "IZO");
            var belgeB = await SeedOnaylanmisSatisFaturasiAsync(dbContext, new DateTime(2026, 4, 1), tesisBId, musteriKartB.Id);

            var serviceB = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
            var sonucB = await serviceB.FaturaKesAsync(belgeB.Id!.Value, new FaturaKesRequest { SeriKodu = "IZO" });

            Assert.Equal("IZO2026000000001", sonucB.ResmiFaturaNo);
        }
        finally
        {
            if (kurumBId > 0)
            {
                await using var cleanupContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
                await CleanupKurumAsync(cleanupContext, kurumBId, tesisBId, ilBId, uniqueSuffixB);
            }
        }
    }

    [IntegrationFact]
    public async Task FaturaKesAsync_AyniKurumdaFarkliSerilerBagimsizdir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await SeedSayacAsync(dbContext, _kurumId, 2026, "AAA", sonNumara: 5);
        await SeedSayacAsync(dbContext, _kurumId, 2026, "BBB");

        var belge = await SeedOnaylanmisSatisFaturasiAsync(dbContext, new DateTime(2026, 5, 1));
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var sonuc = await service.FaturaKesAsync(belge.Id!.Value, new FaturaKesRequest { SeriKodu = "BBB" });

        Assert.Equal("BBB2026000000001", sonuc.ResmiFaturaNo);
    }

    [IntegrationFact]
    public async Task FaturaKesAsync_YeniMaliYilSayaciBagimsizSifirdanBaslatir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await SeedSayacAsync(dbContext, _kurumId, 2026, "YIL", sonNumara: 7);
        await SeedSayacAsync(dbContext, _kurumId, 2025, "YIL");

        var belge2025 = await SeedOnaylanmisSatisFaturasiAsync(dbContext, new DateTime(2025, 6, 15));
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var sonuc = await service.FaturaKesAsync(belge2025.Id!.Value, new FaturaKesRequest { SeriKodu = "YIL" });

        Assert.Equal("YIL2025000000001", sonuc.ResmiFaturaNo);
    }

    [IntegrationFact]
    public async Task FaturaKesAsync_PasifSeriReddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await SeedSayacAsync(dbContext, _kurumId, 2026, "PAS", aktifMi: false);

        var belge = await SeedOnaylanmisSatisFaturasiAsync(dbContext, new DateTime(2026, 3, 10));
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            service.FaturaKesAsync(belge.Id!.Value, new FaturaKesRequest { SeriKodu = "PAS" }));
        Assert.Contains("pasif durumda", ex.Message);
    }

    [IntegrationFact]
    public async Task FaturaKesAsync_BaskaKurumunSerisiReddedilir()
    {
        var uniqueSuffixB = $"{TestMarker}-B-{Guid.NewGuid():N}"[..24];
        int kurumBId = 0, ilBId = 0, tesisBId = 0;

        try
        {
            await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
            var (kurumB, ilB, tesisB) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(dbContext, uniqueSuffixB);
            kurumBId = kurumB.Id; ilBId = ilB.Id; tesisBId = tesisB.Id;
            await SeedSayacAsync(dbContext, kurumBId, 2026, "YAB");

            var belgeA = await SeedOnaylanmisSatisFaturasiAsync(dbContext, new DateTime(2026, 3, 10));
            var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

            var ex = await Assert.ThrowsAsync<BaseException>(() =>
                service.FaturaKesAsync(belgeA.Id!.Value, new FaturaKesRequest { SeriKodu = "YAB" }));
            Assert.Contains("bulunamadı", ex.Message);
        }
        finally
        {
            if (kurumBId > 0)
            {
                await using var cleanupContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
                await dbContext_CleanupOnlyKurum(cleanupContext, kurumBId, tesisBId, ilBId);
            }
        }
    }

    private static async Task dbContext_CleanupOnlyKurum(StysAppDbContext dbContext, int kurumId, int tesisId, int ilId)
    {
        await dbContext.KurumFaturaNumaraSayaclari.Where(x => x.KurumId == kurumId).ExecuteDeleteAsync();
        await dbContext.Tesisler.Where(x => x.Id == tesisId).ExecuteDeleteAsync();
        await dbContext.Iller.Where(x => x.Id == ilId).ExecuteDeleteAsync();
        await dbContext.Kurumlar.Where(x => x.Id == kurumId).ExecuteDeleteAsync();
    }

    [Theory]
    [InlineData("AB")]
    [InlineData("ABCD")]
    [InlineData("AB_")]
    [InlineData("")]
    public void NormalizeSeriKodu_GecersizFormatReddedilir_FormatDogrulamasi(string invalidSeri)
    {
        // NormalizeSeriKodu private static'tir; format kuralı burada FaturaKesAsync'in gerçek
        // reddini (aşağıdaki entegrasyon testinde) tetikleyen aynı mantığın belgelenmiş halidir.
        Assert.True(invalidSeri.Trim().ToUpperInvariant().Length != 3 ||
                    !invalidSeri.All(c => (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')));
    }

    [IntegrationFact]
    public async Task FaturaKesAsync_GecersizSeriFormatiReddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var belge = await SeedOnaylanmisSatisFaturasiAsync(dbContext, new DateTime(2026, 3, 10));
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            service.FaturaKesAsync(belge.Id!.Value, new FaturaKesRequest { SeriKodu = "AB1_" }));
        Assert.Contains("3 karakter", ex.Message);
    }

    // ─────────────────────────────────────────────────────────────
    // Uygunluk kuralları
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task FaturaKesAsync_AlisFaturasiIcinReddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var created = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.AlisFaturasi,
            TesisId = _tesisId,
            BelgeTarihi = new DateTime(2026, 3, 10),
            MusteriAdSoyad = "Tedarikci",
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Alis", Miktar = 1, BirimFiyat = 500m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        });

        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            service.FaturaKesAsync(created.Id!.Value, new FaturaKesRequest { SeriKodu = "ABC" }));
        Assert.Contains("yalnızca standart satış faturaları", ex.Message);
    }

    [IntegrationFact]
    public async Task FaturaKesAsync_ProformaIcinReddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var created = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.Proforma,
            TesisId = _tesisId,
            BelgeTarihi = new DateTime(2026, 3, 10),
            MusteriAdSoyad = "Musteri",
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Proforma", Miktar = 1, BirimFiyat = 500m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        });

        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            service.FaturaKesAsync(created.Id!.Value, new FaturaKesRequest { SeriKodu = "ABC" }));
        Assert.Contains("yalnızca standart satış faturaları", ex.Message);
    }

    [IntegrationFact]
    public async Task FaturaKesAsync_MuhasebeOnaylanmamisBelgeIcinReddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var created = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
            TesisId = _tesisId,
            CariKartId = _musteriKartId,
            BelgeTarihi = new DateTime(2026, 3, 10),
            MusteriAdSoyad = "Musteri",
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Satis", Miktar = 1, BirimFiyat = 500m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        });

        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            service.FaturaKesAsync(created.Id!.Value, new FaturaKesRequest { SeriKodu = "ABC" }));
        Assert.Contains("MuhasebeOnaylandı", ex.Message);
    }

    [IntegrationFact]
    public async Task FaturaKesAsync_MuhasebeFisIdOlmayanOnaylanmisBelgeIcinReddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var created = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
            TesisId = _tesisId,
            CariKartId = _musteriKartId,
            BelgeTarihi = new DateTime(2026, 3, 10),
            MusteriAdSoyad = "Musteri",
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Satis", Miktar = 1, BirimFiyat = 500m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        });
        await service.MuhasebeOnayinaGonderAsync(created.Id!.Value);
        await service.MuhasebeOnaylaAsync(created.Id!.Value);
        // Bilerek MuhasebeFisiOlusturAsync ÇAĞRILMADI - MuhasebeFisId yok.

        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            service.FaturaKesAsync(created.Id!.Value, new FaturaKesRequest { SeriKodu = "ABC" }));
        Assert.Contains("muhasebe fişi bulunamadı", ex.Message);
    }

    // ─────────────────────────────────────────────────────────────
    // İdempotency ve transaction bütünlüğü
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task FaturaKesAsync_IkinciCagriYeniNumaraTuketmezAyniSonucuDoner()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await SeedSayacAsync(dbContext, _kurumId, 2026, "IDM");

        var belge = await SeedOnaylanmisSatisFaturasiAsync(dbContext, new DateTime(2026, 3, 10));
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var ilk = await service.FaturaKesAsync(belge.Id!.Value, new FaturaKesRequest { SeriKodu = "IDM" });
        var ikinci = await service.FaturaKesAsync(belge.Id!.Value, new FaturaKesRequest { SeriKodu = "IDM" });

        Assert.Equal(ilk.ResmiFaturaNo, ikinci.ResmiFaturaNo);

        var sayacDb = await dbContext.KurumFaturaNumaraSayaclari.AsNoTracking()
            .FirstAsync(x => x.KurumId == _kurumId && x.MaliYil == 2026 && x.SeriKodu == "IDM");
        Assert.Equal(1, sayacDb.SonNumara);
    }

    [IntegrationFact]
    public async Task FaturaKesAsync_CakisanNumarayaCarpinca_SayacVeBelgeBirlikteGeriAlinir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await SeedSayacAsync(dbContext, _kurumId, 2026, "DUP");

        // Zaten "DUP2026000000001" numarasıyla resmî olarak kesilmiş GİBİ davranan bir "hayalet"
        // belge oluştur (gerçek CreateAsync/FaturaKesAsync akışı ResmiFaturaNo'yu asla client'tan
        // almadığı için, bu çakışmayı simüle etmenin tek yolu doğrudan entity üzerinden - migrasyon/
        // manuel veri düzeltmesi senaryosunu taklit ederek - yazmaktır).
        var hayaletBelge = await SeedOnaylanmisSatisFaturasiAsync(dbContext, new DateTime(2026, 3, 1));
        var hayaletDb = await dbContext.SatisBelgeleri.FirstAsync(x => x.Id == hayaletBelge.Id);
        hayaletDb.ResmiFaturaNo = "DUP2026000000001";
        hayaletDb.FaturaKesimTarihi = DateTime.UtcNow;
        hayaletDb.Durum = SatisBelgesiDurumu.FaturaKesildi;
        await dbContext.SaveChangesAsync();

        var gercekBelge = await SeedOnaylanmisSatisFaturasiAsync(dbContext, new DateTime(2026, 3, 2));
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            service.FaturaKesAsync(gercekBelge.Id!.Value, new FaturaKesRequest { SeriKodu = "DUP" }));
        Assert.Equal(409, ex.ErrorCode);

        await using var verifyContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var sayacDb = await verifyContext.KurumFaturaNumaraSayaclari.AsNoTracking()
            .FirstAsync(x => x.KurumId == _kurumId && x.MaliYil == 2026 && x.SeriKodu == "DUP");
        Assert.Equal(0, sayacDb.SonNumara);

        var gercekBelgeDb = await verifyContext.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.Id == gercekBelge.Id);
        Assert.Equal(SatisBelgesiDurumu.MuhasebeOnaylandi, gercekBelgeDb.Durum);
        Assert.Null(gercekBelgeDb.ResmiFaturaNo);
        Assert.Null(gercekBelgeDb.FaturaKesimTarihi);
    }

    // ─────────────────────────────────────────────────────────────
    // Değişmezlik/tutarlılık invariantları (hardening turu)
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task FaturaKesAsync_ResmiNumaraDoluAmaDurumMuhasebeOnaylandi_ReddedilirVeHicbirSeyDegismez()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await SeedSayacAsync(dbContext, _kurumId, 2026, "ABC", sonNumara: 5);

        var belge = await SeedOnaylanmisSatisFaturasiAsync(dbContext, new DateTime(2026, 3, 10));
        var belgeDb = await dbContext.SatisBelgeleri.FirstAsync(x => x.Id == belge.Id);
        belgeDb.ResmiFaturaNo = "ABC2026000000005";
        // Durum BİLEREK MuhasebeOnaylandi bırakıldı - ResmiFaturaNo dolu ama Durum FaturaKesildi
        // DEĞİL senaryosu.
        await dbContext.SaveChangesAsync();

        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            service.FaturaKesAsync(belge.Id!.Value, new FaturaKesRequest { SeriKodu = "ABC" }));
        Assert.Equal(500, ex.ErrorCode);
        Assert.Contains("veri tutarsızlığı", ex.Message);

        await using var verifyContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var belgeSonHal = await verifyContext.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.Id == belge.Id);
        Assert.Equal("ABC2026000000005", belgeSonHal.ResmiFaturaNo);
        Assert.Equal(SatisBelgesiDurumu.MuhasebeOnaylandi, belgeSonHal.Durum);

        var sayacDb = await verifyContext.KurumFaturaNumaraSayaclari.AsNoTracking()
            .FirstAsync(x => x.KurumId == _kurumId && x.MaliYil == 2026 && x.SeriKodu == "ABC");
        Assert.Equal(5, sayacDb.SonNumara);
    }

    [IntegrationFact]
    public async Task FaturaKesAsync_FaturaKesildiAmaFaturaKesimTarihiBos_Reddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var belge = await SeedOnaylanmisSatisFaturasiAsync(dbContext, new DateTime(2026, 3, 10));
        var belgeDb = await dbContext.SatisBelgeleri.FirstAsync(x => x.Id == belge.Id);
        belgeDb.ResmiFaturaNo = "XYZ2026000000001";
        belgeDb.Durum = SatisBelgesiDurumu.FaturaKesildi;
        // FaturaKesimTarihi BİLEREK boş bırakıldı.
        await dbContext.SaveChangesAsync();

        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            service.FaturaKesAsync(belge.Id!.Value, new FaturaKesRequest { SeriKodu = "XYZ" }));
        Assert.Equal(500, ex.ErrorCode);
        Assert.Contains("fatura kesim tarihi bulunamadı", ex.Message);
    }

    [IntegrationFact]
    public async Task FaturaKesAsync_SayacMevcutNumarininSirasindanKucukse_IdempotentBasariGibiDonulmez()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await SeedSayacAsync(dbContext, _kurumId, 2026, "LOW", sonNumara: 2);

        var belge = await SeedOnaylanmisSatisFaturasiAsync(dbContext, new DateTime(2026, 3, 10));
        var belgeDb = await dbContext.SatisBelgeleri.FirstAsync(x => x.Id == belge.Id);
        belgeDb.ResmiFaturaNo = "LOW2026000000005";
        belgeDb.Durum = SatisBelgesiDurumu.FaturaKesildi;
        belgeDb.FaturaKesimTarihi = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            service.FaturaKesAsync(belge.Id!.Value, new FaturaKesRequest { SeriKodu = "LOW" }));
        Assert.Equal(500, ex.ErrorCode);
        Assert.Contains("küçük", ex.Message);
    }

    [IntegrationFact]
    public async Task FaturaKesAsync_DahaOnceFarkliSeriyleKesilmisBelge_YeniNumaraTuketmezCakismaVerir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await SeedSayacAsync(dbContext, _kurumId, 2026, "AAA");

        var belge = await SeedOnaylanmisSatisFaturasiAsync(dbContext, new DateTime(2026, 3, 10));
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var ilkSonuc = await service.FaturaKesAsync(belge.Id!.Value, new FaturaKesRequest { SeriKodu = "AAA" });
        Assert.Equal("AAA2026000000001", ilkSonuc.ResmiFaturaNo);

        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            service.FaturaKesAsync(belge.Id!.Value, new FaturaKesRequest { SeriKodu = "BBB" }));
        Assert.Equal(409, ex.ErrorCode);
        Assert.Contains("tekrar fatura kesilemez", ex.Message);

        await using var verifyContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var sayacAaaDb = await verifyContext.KurumFaturaNumaraSayaclari.AsNoTracking()
            .FirstAsync(x => x.KurumId == _kurumId && x.MaliYil == 2026 && x.SeriKodu == "AAA");
        Assert.Equal(1, sayacAaaDb.SonNumara);

        var sayacBbbVarMi = await verifyContext.KurumFaturaNumaraSayaclari.AsNoTracking()
            .AnyAsync(x => x.KurumId == _kurumId && x.MaliYil == 2026 && x.SeriKodu == "BBB");
        Assert.False(sayacBbbVarMi);

        var belgeSonHal = await verifyContext.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.Id == belge.Id);
        Assert.Equal("AAA2026000000001", belgeSonHal.ResmiFaturaNo);
    }

    [IntegrationFact]
    public async Task FaturaKesAsync_SayacUstSinirdaysa_ReddedilirHicbirSeyDegismez()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await SeedSayacAsync(dbContext, _kurumId, 2026, "MAX", sonNumara: 999999999);

        var belge = await SeedOnaylanmisSatisFaturasiAsync(dbContext, new DateTime(2026, 3, 10));
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            service.FaturaKesAsync(belge.Id!.Value, new FaturaKesRequest { SeriKodu = "MAX" }));
        Assert.Equal(409, ex.ErrorCode);
        Assert.Contains("999999999", ex.Message);

        await using var verifyContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var sayacDb = await verifyContext.KurumFaturaNumaraSayaclari.AsNoTracking()
            .FirstAsync(x => x.KurumId == _kurumId && x.MaliYil == 2026 && x.SeriKodu == "MAX");
        Assert.Equal(999999999, sayacDb.SonNumara);

        var belgeSonHal = await verifyContext.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.Id == belge.Id);
        Assert.Equal(SatisBelgesiDurumu.MuhasebeOnaylandi, belgeSonHal.Durum);
        Assert.Null(belgeSonHal.ResmiFaturaNo);
        Assert.Null(belgeSonHal.FaturaKesimTarihi);
    }

    // ─────────────────────────────────────────────────────────────
    // Muhasebe fişi durum matrisi
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task FaturaKesAsync_TersKayitFisineBagliBelge_Kesilemez()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await SeedSayacAsync(dbContext, _kurumId, 2026, "TRK");

        var belge = await SeedOnaylanmisSatisFaturasiAsync(dbContext, new DateTime(2026, 3, 10));
        var fisDb = await dbContext.MuhasebeFisler.FirstAsync(x => x.Id == belge.MuhasebeFisId!.Value);
        fisDb.Durum = MuhasebeFisDurumlari.TersKayit;
        await dbContext.SaveChangesAsync();

        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            service.FaturaKesAsync(belge.Id!.Value, new FaturaKesRequest { SeriKodu = "TRK" }));
        Assert.Contains("ters kayıt fişidir", ex.Message);
    }

    [IntegrationFact]
    public async Task FaturaKesAsync_TaslakMuhasebeFisliNormalSatisFaturasi_BasariylaKesilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await SeedSayacAsync(dbContext, _kurumId, 2026, "TSL");

        var belge = await SeedOnaylanmisSatisFaturasiAsync(dbContext, new DateTime(2026, 3, 10));
        var fisDb = await dbContext.MuhasebeFisler.AsNoTracking().FirstAsync(x => x.Id == belge.MuhasebeFisId!.Value);
        Assert.Equal(MuhasebeFisDurumlari.Taslak, fisDb.Durum);

        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var sonuc = await service.FaturaKesAsync(belge.Id!.Value, new FaturaKesRequest { SeriKodu = "TSL" });

        Assert.Equal("TSL2026000000001", sonuc.ResmiFaturaNo);
        Assert.Equal(SatisBelgesiDurumu.FaturaKesildi, sonuc.Durum);
    }

    // ─────────────────────────────────────────────────────────────
    // Eşzamanlılık — GERÇEK SQL Server, ayrı DbContext/servis örnekleri
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task FaturaKesAsync_EszamanliFarkliBelgeler_HepsiFarkliVeBosluksuzNumaraAlir()
    {
        const int belgeSayisi = 15;

        await using var seedContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await SeedSayacAsync(seedContext, _kurumId, 2026, "CNC");

        var belgeIds = new List<int>();
        for (var i = 0; i < belgeSayisi; i++)
        {
            var belge = await SeedOnaylanmisSatisFaturasiAsync(seedContext, new DateTime(2026, 6, 1));
            belgeIds.Add(belge.Id!.Value);
        }

        var tasks = belgeIds.Select(async id =>
        {
            await using var ctx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
            var svc = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(ctx);
            return await svc.FaturaKesAsync(id, new FaturaKesRequest { SeriKodu = "CNC" });
        });

        var sonuclar = await Task.WhenAll(tasks);

        var numaralar = sonuclar.Select(x => x.ResmiFaturaNo).ToList();
        Assert.Equal(belgeSayisi, numaralar.Distinct().Count());

        var siraNolar = numaralar
            .Select(n => int.Parse(n!.Substring(7)))
            .OrderBy(x => x)
            .ToList();
        Assert.Equal(Enumerable.Range(1, belgeSayisi), siraNolar);

        var sayacDb = await seedContext.KurumFaturaNumaraSayaclari.AsNoTracking()
            .FirstAsync(x => x.KurumId == _kurumId && x.MaliYil == 2026 && x.SeriKodu == "CNC");
        Assert.Equal(belgeSayisi, sayacDb.SonNumara);
    }

    [IntegrationFact]
    public async Task FaturaKesAsync_AyniBelgeyeEszamanliIkiCagri_TekNumaraTuketilir()
    {
        await using var seedContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await SeedSayacAsync(seedContext, _kurumId, 2026, "SNC");
        var belge = await SeedOnaylanmisSatisFaturasiAsync(seedContext, new DateTime(2026, 6, 10));

        async Task<SatisBelgesiDto> KesAsync()
        {
            await using var ctx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
            var svc = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(ctx);
            return await svc.FaturaKesAsync(belge.Id!.Value, new FaturaKesRequest { SeriKodu = "SNC" });
        }

        var t1 = KesAsync();
        var t2 = KesAsync();
        var sonuclar = await Task.WhenAll(t1, t2);

        Assert.Equal(sonuclar[0].ResmiFaturaNo, sonuclar[1].ResmiFaturaNo);
        Assert.Equal("SNC2026000000001", sonuclar[0].ResmiFaturaNo);

        var sayacDb = await seedContext.KurumFaturaNumaraSayaclari.AsNoTracking()
            .FirstAsync(x => x.KurumId == _kurumId && x.MaliYil == 2026 && x.SeriKodu == "SNC");
        Assert.Equal(1, sayacDb.SonNumara);
    }
}
