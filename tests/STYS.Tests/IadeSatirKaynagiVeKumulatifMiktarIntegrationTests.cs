using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.MuhasebeDonemleri.Entities;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.SatisBelgeleri;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Services;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// İade satırlarının kaynak satıra (KaynakSatirId) bağlantısını ve kümülatif iade miktarının
/// asıl satır miktarını aşmadığını doğrulayan GERÇEK SQL Server entegrasyon testleri
/// (SatisBelgesiService.ValidateIadeSatirlariAsync).
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class IadeSatirKaynagiVeKumulatifMiktarIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "IADEKMK-771";

    private string _uniqueSuffix = TestMarker;
    private int _kurumId;
    private int _ilId;
    private int _tesisId;
    private int _musteriKartId;
    private int _tedarikciKartId;

    // İkinci (tamamen ayrı) kurum — tenant izolasyonu senaryosu için.
    private string _uniqueSuffix2 = TestMarker + "-2";
    private int _kurumId2;
    private int _ilId2;
    private int _tesisId2;
    private int _musteriKartId2;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString))
        {
            return;
        }

        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        _uniqueSuffix2 = $"{TestMarker}-2-{Guid.NewGuid():N}"[..24];

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();

        (_kurumId, _ilId, _tesisId, _musteriKartId, _tedarikciKartId) =
            await SeedKurumSetiAsync(dbContext, _uniqueSuffix);

        (_kurumId2, _ilId2, _tesisId2, _musteriKartId2, _) =
            await SeedKurumSetiAsync(dbContext, _uniqueSuffix2);
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString) || _kurumId <= 0)
        {
            return;
        }

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await CleanupKurumAsync(dbContext, _kurumId, _tesisId, _ilId, _uniqueSuffix);
        await CleanupKurumAsync(dbContext, _kurumId2, _tesisId2, _ilId2, _uniqueSuffix2);
    }

    // ─────────────────────────────────────────────────────────────
    // Yardımcılar
    // ─────────────────────────────────────────────────────────────

    private static async Task<(int KurumId, int IlId, int TesisId, int MusteriKartId, int TedarikciKartId)> SeedKurumSetiAsync(
        StysAppDbContext dbContext, string uniqueSuffix)
    {
        var (kurum, il, tesis) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(dbContext, uniqueSuffix);
        var tesisId = tesis.Id;

        var gelirHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(uniqueSuffix, MuhasebeAnaHesapKodlari.GelirSatis, "GELIR", tesisId);
        var kdvSatisHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(uniqueSuffix, MuhasebeAnaHesapKodlari.KDVHesaplanan, "KDVS", tesisId);
        var kdvAlisHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(uniqueSuffix, MuhasebeAnaHesapKodlari.KDVIndirilecek, "KDVA", tesisId);
        var giderHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(uniqueSuffix, MuhasebeAnaHesapKodlari.GiderHizmetMaliyet, "GIDER", tesisId);
        var satisIadeHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(uniqueSuffix, MuhasebeAnaHesapKodlari.SatisIade, "IADE", tesisId);
        var stokHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(uniqueSuffix, MuhasebeAnaHesapKodlari.StokTicariMal, "STOK", tesisId);
        var musteriHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(uniqueSuffix, "MUS", tesisId);
        var tedarikciHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(uniqueSuffix, "TED", tesisId);
        dbContext.MuhasebeHesapPlanlari.AddRange(
            gelirHesap, kdvSatisHesap, kdvAlisHesap, giderHesap, satisIadeHesap, stokHesap, musteriHesap, tedarikciHesap);
        await dbContext.SaveChangesAsync();

        var musteriKart = SatisBelgesiMuhasebeTestSupport.BuildCariKart(uniqueSuffix, "MUS", CariKartTipleri.Musteri, tesisId, musteriHesap.Id);
        var tedarikciKart = SatisBelgesiMuhasebeTestSupport.BuildCariKart(uniqueSuffix, "TED", CariKartTipleri.Tedarikci, tesisId, tedarikciHesap.Id);
        tedarikciKart.VergiNoTckn = "1111111111";
        dbContext.CariKartlar.AddRange(musteriKart, tedarikciKart);
        await dbContext.SaveChangesAsync();

        dbContext.MuhasebeDonemler.Add(new MuhasebeDonem
        {
            TesisId = tesisId, MaliYil = 2026, DonemNo = 1,
            BaslangicTarihi = new DateTime(2026, 1, 1), BitisTarihi = new DateTime(2026, 12, 31), KapaliMi = false
        });
        await dbContext.SaveChangesAsync();

        return (kurum.Id, il.Id, tesisId, musteriKart.Id, tedarikciKart.Id);
    }

    private static async Task CleanupKurumAsync(StysAppDbContext dbContext, int kurumId, int tesisId, int ilId, string uniqueSuffix)
    {
        var belgeIds = await dbContext.SatisBelgeleri.IgnoreQueryFilters()
            .Where(x => x.KurumId == kurumId).Select(x => x.Id).ToListAsync();
        var fisIds = new List<int>();
        if (belgeIds.Count > 0)
        {
            fisIds = await dbContext.MuhasebeFisler.IgnoreQueryFilters()
                .Where(x => x.KaynakModul == MuhasebeKaynakModulleri.SatisBelgesi && x.KaynakId != null && belgeIds.Contains(x.KaynakId.Value))
                .Select(x => x.Id).ToListAsync();
            await dbContext.CariHareketler
                .Where(x => x.KaynakModul == MuhasebeKaynakModulleri.SatisBelgesi && x.KaynakId != null && belgeIds.Contains(x.KaynakId.Value))
                .ExecuteDeleteAsync();
            await dbContext.SatisBelgeleri.IgnoreQueryFilters().Where(x => belgeIds.Contains(x.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IadeEdilenBelgeId, (int?)null));
            await dbContext.SatisBelgeleri.IgnoreQueryFilters().Where(x => belgeIds.Contains(x.Id)).ExecuteDeleteAsync();
        }
        if (fisIds.Count > 0)
        {
            await dbContext.MuhasebeFisSatirlari.IgnoreQueryFilters().Where(x => fisIds.Contains(x.MuhasebeFisId)).ExecuteDeleteAsync();
            await dbContext.MuhasebeFisler.IgnoreQueryFilters().Where(x => fisIds.Contains(x.Id)).ExecuteDeleteAsync();
        }

        await dbContext.KurumFaturaNumaraSayaclari.Where(x => x.KurumId == kurumId).ExecuteDeleteAsync();
        await dbContext.MuhasebeDonemler.Where(x => x.TesisId == tesisId).ExecuteDeleteAsync();
        await dbContext.CariKartlar.Where(x => x.TesisId == tesisId).ExecuteDeleteAsync();
        await dbContext.MuhasebeHesapPlanlari.Where(x => x.Kod != null && x.Kod.Contains(uniqueSuffix)).ExecuteDeleteAsync();
        await dbContext.Tesisler.Where(x => x.Id == tesisId).ExecuteDeleteAsync();
        await dbContext.Iller.Where(x => x.Id == ilId).ExecuteDeleteAsync();
        await dbContext.Kurumlar.Where(x => x.Id == kurumId).ExecuteDeleteAsync();
    }

    /// <summary>
    /// SatisFaturasi'nı Create -> MuhasebeOnayinaGonder -> MuhasebeOnayla -> MuhasebeFisiOlustur ->
    /// FaturaKes akışıyla "FaturaKesildi" durumuna getirir ve kaynak satırın Id/Miktar bilgisini
    /// (SatisIadeFaturasi.KaynakSatirId için kullanılmak üzere) döner.
    /// </summary>
    private static async Task<(SatisBelgesiDto Belge, int SatirId, decimal Miktar)> SeedOnaylanmisSatisFaturasiVeKesAsync(
        StysAppDbContext dbContext, int kurumId, int tesisId, int musteriKartId, string uniqueSuffix,
        DateTime belgeTarihi, string seriKodu, decimal miktar = 10m, decimal birimFiyat = 100m)
    {
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var donemService = SatisBelgesiMuhasebeTestSupport.CreateRealMuhasebeDonemService(dbContext);
        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext, donemService);

        var created = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
            TesisId = tesisId,
            CariKartId = musteriKartId,
            BelgeTarihi = belgeTarihi,
            MusteriAdSoyad = "Test Musteri " + uniqueSuffix,
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Test satir", Miktar = miktar, BirimFiyat = birimFiyat,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        });
        var satirId = created.Satirlar[0].Id!.Value;

        await service.MuhasebeOnayinaGonderAsync(created.Id!.Value);
        await service.MuhasebeOnaylaAsync(created.Id.Value);
        await fisService.MuhasebeFisiOlusturAsync(created.Id.Value);

        var sayacVarMi = await dbContext.KurumFaturaNumaraSayaclari.AsNoTracking()
            .AnyAsync(x => !x.IsDeleted && x.KurumId == kurumId && x.MaliYil == belgeTarihi.Year && x.SeriKodu == seriKodu);
        if (!sayacVarMi)
        {
            dbContext.KurumFaturaNumaraSayaclari.Add(new KurumFaturaNumaraSayaci
            {
                KurumId = kurumId, MaliYil = belgeTarihi.Year, SeriKodu = seriKodu, SonNumara = 0, AktifMi = true
            });
            await dbContext.SaveChangesAsync();
        }

        var kesildi = await service.FaturaKesAsync(created.Id.Value, new FaturaKesRequest { SeriKodu = seriKodu });
        return (kesildi, satirId, miktar);
    }

    /// <summary>AlisFaturasi'nı Create -> MuhasebeOnayinaGonder -> MuhasebeOnayla -> MuhasebeFisiOlustur akışıyla "MuhasebeOnaylandi" durumuna getirir.</summary>
    private static async Task<(SatisBelgesiDto Belge, int SatirId, decimal Miktar)> SeedOnaylanmisAlisFaturasiAsync(
        StysAppDbContext dbContext, int tesisId, int tedarikciKartId, string uniqueSuffix,
        DateTime belgeTarihi, decimal miktar = 10m, decimal birimFiyat = 100m)
    {
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var donemService = SatisBelgesiMuhasebeTestSupport.CreateRealMuhasebeDonemService(dbContext);
        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext, donemService);

        var created = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.AlisFaturasi,
            TesisId = tesisId,
            CariKartId = tedarikciKartId,
            BelgeTarihi = belgeTarihi,
            MusteriAdSoyad = "Test Tedarikci " + uniqueSuffix,
            KarsiTarafFaturaNo = $"TED-{Guid.NewGuid():N}"[..20],
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Test satir", Miktar = miktar, BirimFiyat = birimFiyat,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        });
        var satirId = created.Satirlar[0].Id!.Value;

        await service.MuhasebeOnayinaGonderAsync(created.Id!.Value);
        await service.MuhasebeOnaylaAsync(created.Id.Value);
        var onaylanmis = await fisService.MuhasebeFisiOlusturAsync(created.Id.Value);

        return (onaylanmis, satirId, miktar);
    }

    private static CreateSatisBelgesiRequest BuildSatisIadeRequest(
        int tesisId, int musteriKartId, int iadeEdilenBelgeId, string uniqueSuffix,
        DateTime belgeTarihi, int kaynakSatirId, decimal miktar, decimal birimFiyat = 100m, decimal kdvOrani = 20m)
    {
        return new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.SatisIadeFaturasi,
            TesisId = tesisId,
            CariKartId = musteriKartId,
            BelgeTarihi = belgeTarihi,
            MusteriAdSoyad = "Test Musteri " + uniqueSuffix,
            KarsiTarafFaturaNo = $"MUS-{Guid.NewGuid():N}"[..20],
            IadeEdilenBelgeId = iadeEdilenBelgeId,
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Iade", Miktar = miktar, BirimFiyat = birimFiyat,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = kdvOrani,
                    KaynakSatirId = kaynakSatirId.ToString()
                }
            ]
        };
    }

    private static async Task<SatisBelgesi> ReadNoTrackingAsync(StysAppDbContext dbContext, int id)
        => await dbContext.SatisBelgeleri.AsNoTracking().Include(x => x.Satirlar).FirstAsync(x => x.Id == id);

    // ─────────────────────────────────────────────────────────────
    // 1. Geçerli tek kısmi iade
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task SatisIadeFaturasi_GecerliTekKismiIade_KabulEdilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var (asil, satirId, miktar) = await SeedOnaylanmisSatisFaturasiVeKesAsync(
            dbContext, _kurumId, _tesisId, _musteriKartId, _uniqueSuffix, new DateTime(2026, 3, 1), "TK1", miktar: 10m);

        var iade = await service.CreateAsync(BuildSatisIadeRequest(
            _tesisId, _musteriKartId, asil.Id!.Value, _uniqueSuffix, new DateTime(2026, 3, 5), satirId, miktar: 4m));

        await service.MuhasebeOnayinaGonderAsync(iade.Id!.Value);
        await service.MuhasebeOnaylaAsync(iade.Id.Value);

        var sonHal = await ReadNoTrackingAsync(dbContext, iade.Id.Value);
        Assert.Equal(SatisBelgesiDurumu.MuhasebeOnaylandi, sonHal.Durum);
        Assert.Equal(satirId.ToString(), sonHal.Satirlar.Single(x => !x.IsDeleted).KaynakSatirId);
    }

    // ─────────────────────────────────────────────────────────────
    // 2. İki kısmi iadenin toplamının asıl miktara eşit olması
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task SatisIadeFaturasi_IkiKismiIadeToplamiAsilMiktaraEsit_IkisiDeKabulEdilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var (asil, satirId, miktar) = await SeedOnaylanmisSatisFaturasiVeKesAsync(
            dbContext, _kurumId, _tesisId, _musteriKartId, _uniqueSuffix, new DateTime(2026, 3, 1), "TK2", miktar: 10m);

        var iade1 = await service.CreateAsync(BuildSatisIadeRequest(
            _tesisId, _musteriKartId, asil.Id!.Value, _uniqueSuffix, new DateTime(2026, 3, 5), satirId, miktar: 6m));
        await service.MuhasebeOnayinaGonderAsync(iade1.Id!.Value);
        await service.MuhasebeOnaylaAsync(iade1.Id.Value);

        var iade2 = await service.CreateAsync(BuildSatisIadeRequest(
            _tesisId, _musteriKartId, asil.Id.Value, _uniqueSuffix, new DateTime(2026, 3, 6), satirId, miktar: 4m));
        await service.MuhasebeOnayinaGonderAsync(iade2.Id!.Value);
        await service.MuhasebeOnaylaAsync(iade2.Id.Value);

        var iade1SonHal = await ReadNoTrackingAsync(dbContext, iade1.Id.Value);
        var iade2SonHal = await ReadNoTrackingAsync(dbContext, iade2.Id.Value);
        Assert.Equal(SatisBelgesiDurumu.MuhasebeOnaylandi, iade1SonHal.Durum);
        Assert.Equal(SatisBelgesiDurumu.MuhasebeOnaylandi, iade2SonHal.Durum);
    }

    // ─────────────────────────────────────────────────────────────
    // 3. Kümülatif miktarın asıl miktarı aşmasının reddedilmesi
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task SatisIadeFaturasi_KumulatifMiktarAsilMiktariAsarsa_OnayaGondermedeReddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var (asil, satirId, miktar) = await SeedOnaylanmisSatisFaturasiVeKesAsync(
            dbContext, _kurumId, _tesisId, _musteriKartId, _uniqueSuffix, new DateTime(2026, 3, 1), "TK3", miktar: 10m);

        // 1. iade onaylanır (6 <= 10).
        var iade1 = await service.CreateAsync(BuildSatisIadeRequest(
            _tesisId, _musteriKartId, asil.Id!.Value, _uniqueSuffix, new DateTime(2026, 3, 5), satirId, miktar: 6m));
        await service.MuhasebeOnayinaGonderAsync(iade1.Id!.Value);
        await service.MuhasebeOnaylaAsync(iade1.Id.Value);

        // 2. iade TEK BAŞINA geçerli (6 <= 10) — Create sırasında reddedilmez; yalnızca KÜMÜLATİF
        // (6 + 6 = 12 > 10) olduğunda, onaya gönderme aşamasında reddedilir.
        var iade2 = await service.CreateAsync(BuildSatisIadeRequest(
            _tesisId, _musteriKartId, asil.Id.Value, _uniqueSuffix, new DateTime(2026, 3, 6), satirId, miktar: 6m));

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.MuhasebeOnayinaGonderAsync(iade2.Id!.Value));
        Assert.Contains("toplam iade miktarı", ex.Message);

        var iade2SonHal = await ReadNoTrackingAsync(dbContext, iade2.Id!.Value);
        Assert.Equal(SatisBelgesiDurumu.Taslak, iade2SonHal.Durum);
    }

    // ─────────────────────────────────────────────────────────────
    // 4. Tek iade satırının asıl miktarı aşmasının reddedilmesi
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task SatisIadeFaturasi_TekSatirAsilMiktariAsarsa_CreateSirasindaReddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var (asil, satirId, miktar) = await SeedOnaylanmisSatisFaturasiVeKesAsync(
            dbContext, _kurumId, _tesisId, _musteriKartId, _uniqueSuffix, new DateTime(2026, 3, 1), "TK4", miktar: 10m);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.CreateAsync(BuildSatisIadeRequest(
            _tesisId, _musteriKartId, asil.Id!.Value, _uniqueSuffix, new DateTime(2026, 3, 5), satirId, miktar: 11m)));

        Assert.Contains("toplam iade miktarı", ex.Message);
    }

    // ─────────────────────────────────────────────────────────────
    // 5. KaynakSatirId bulunmaması
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task SatisIadeFaturasi_KaynakSatirIdBulunmamasi_CreateSirasindaReddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var (asil, _, _) = await SeedOnaylanmisSatisFaturasiVeKesAsync(
            dbContext, _kurumId, _tesisId, _musteriKartId, _uniqueSuffix, new DateTime(2026, 3, 1), "TK5", miktar: 10m);

        var request = new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.SatisIadeFaturasi,
            TesisId = _tesisId,
            CariKartId = _musteriKartId,
            BelgeTarihi = new DateTime(2026, 3, 5),
            MusteriAdSoyad = "Musteri",
            KarsiTarafFaturaNo = $"MUS-{Guid.NewGuid():N}"[..20],
            IadeEdilenBelgeId = asil.Id,
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Iade", Miktar = 4m, BirimFiyat = 100m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m,
                    KaynakSatirId = null
                }
            ]
        };

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.CreateAsync(request));
        Assert.Contains("kaynak satır referansı", ex.Message);
    }

    // ─────────────────────────────────────────────────────────────
    // 6. Kaynak satırın başka faturaya ait olması
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task SatisIadeFaturasi_KaynakSatirBaskaFaturayaAit_CreateSirasindaReddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var (asil, _, _) = await SeedOnaylanmisSatisFaturasiVeKesAsync(
            dbContext, _kurumId, _tesisId, _musteriKartId, _uniqueSuffix, new DateTime(2026, 3, 1), "TK6", miktar: 10m);

        // Tamamen AYRI, ilgisiz ikinci bir asıl fatura — kaynak satırı buradan alınacak.
        var (baskaAsil, baskaSatirId, _) = await SeedOnaylanmisSatisFaturasiVeKesAsync(
            dbContext, _kurumId, _tesisId, _musteriKartId, _uniqueSuffix, new DateTime(2026, 3, 1), "K6B", miktar: 10m);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.CreateAsync(BuildSatisIadeRequest(
            _tesisId, _musteriKartId, asil.Id!.Value, _uniqueSuffix, new DateTime(2026, 3, 5), baskaSatirId, miktar: 4m)));

        Assert.Contains("iade edilen belgeye ait değil", ex.Message);
        _ = baskaAsil;
    }

    // ─────────────────────────────────────────────────────────────
    // 7. Kaynak satırın başka (geçerli) IadeEdilenBelgeId altından seçilmesi
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task SatisIadeFaturasi_KaynakSatirBaskaGecerliAsilFaturadan_CreateSirasindaReddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        // İki AYRI, ikisi de GEÇERLİ (FaturaKesildi) asıl fatura.
        var (asilA, _, _) = await SeedOnaylanmisSatisFaturasiVeKesAsync(
            dbContext, _kurumId, _tesisId, _musteriKartId, _uniqueSuffix, new DateTime(2026, 3, 1), "K7A", miktar: 10m);
        var (asilB, satirIdB, _) = await SeedOnaylanmisSatisFaturasiVeKesAsync(
            dbContext, _kurumId, _tesisId, _musteriKartId, _uniqueSuffix, new DateTime(2026, 3, 1), "K7B", miktar: 10m);

        // IadeEdilenBelgeId = asilA, ama KaynakSatirId = asilB'nin satırı.
        var ex = await Assert.ThrowsAsync<BaseException>(() => service.CreateAsync(BuildSatisIadeRequest(
            _tesisId, _musteriKartId, asilA.Id!.Value, _uniqueSuffix, new DateTime(2026, 3, 5), satirIdB, miktar: 4m)));

        Assert.Contains("iade edilen belgeye ait değil", ex.Message);
    }

    // ─────────────────────────────────────────────────────────────
    // 8. Soft-delete edilmiş kaynak satır
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task SatisIadeFaturasi_SoftDeleteEdilmisKaynakSatir_CreateSirasindaReddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var (asil, satirId, _) = await SeedOnaylanmisSatisFaturasiVeKesAsync(
            dbContext, _kurumId, _tesisId, _musteriKartId, _uniqueSuffix, new DateTime(2026, 3, 1), "TK8", miktar: 10m);

        var kaynakSatirDb = await dbContext.SatisBelgesiSatirlari.FirstAsync(x => x.Id == satirId);
        kaynakSatirDb.IsDeleted = true;
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.CreateAsync(BuildSatisIadeRequest(
            _tesisId, _musteriKartId, asil.Id!.Value, _uniqueSuffix, new DateTime(2026, 3, 5), satirId, miktar: 4m)));

        Assert.Contains("Kaynak satır bulunamadı", ex.Message);
    }

    // ─────────────────────────────────────────────────────────────
    // 9. İptal/soft-delete edilmiş önceki iadenin toplam davranışı
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task SatisIadeFaturasi_IptalEdilmisOncekiIade_KumulatifToplamaDahilDegil()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var (asil, satirId, miktar) = await SeedOnaylanmisSatisFaturasiVeKesAsync(
            dbContext, _kurumId, _tesisId, _musteriKartId, _uniqueSuffix, new DateTime(2026, 3, 1), "TK9", miktar: 10m);

        // 1. iade onaylanır (8), sonra İPTAL edilir.
        var iade1 = await service.CreateAsync(BuildSatisIadeRequest(
            _tesisId, _musteriKartId, asil.Id!.Value, _uniqueSuffix, new DateTime(2026, 3, 5), satirId, miktar: 8m));
        await service.MuhasebeOnayinaGonderAsync(iade1.Id!.Value);
        await service.MuhasebeOnaylaAsync(iade1.Id.Value);
        await service.IptalEtAsync(iade1.Id.Value);

        // 2. iade, iptal edilmiş 1. iadeyle birlikte toplansaydı (8+8=16>10) reddedilirdi; iptal
        // edilmiş iade toplama DAHİL EDİLMEDİĞİNDEN (yalnızca 8<=10) BAŞARILI olmalıdır.
        var iade2 = await service.CreateAsync(BuildSatisIadeRequest(
            _tesisId, _musteriKartId, asil.Id.Value, _uniqueSuffix, new DateTime(2026, 3, 6), satirId, miktar: 8m));
        await service.MuhasebeOnayinaGonderAsync(iade2.Id!.Value);
        await service.MuhasebeOnaylaAsync(iade2.Id.Value);

        var iade1SonHal = await ReadNoTrackingAsync(dbContext, iade1.Id.Value);
        var iade2SonHal = await ReadNoTrackingAsync(dbContext, iade2.Id.Value);
        Assert.Equal(SatisBelgesiDurumu.IptalEdildi, iade1SonHal.Durum);
        Assert.Equal(SatisBelgesiDurumu.MuhasebeOnaylandi, iade2SonHal.Durum);
        _ = miktar;
    }

    // ─────────────────────────────────────────────────────────────
    // 10. Yalnızca açıklama yapılan partial update'te satır bağlantılarının korunması
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task UpdateAsync_SatisIadeFaturasi_SadeceAciklamaGuncelleme_SatirBaglantilariKorunur()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var (asil, satirId, _) = await SeedOnaylanmisSatisFaturasiVeKesAsync(
            dbContext, _kurumId, _tesisId, _musteriKartId, _uniqueSuffix, new DateTime(2026, 3, 1), "K10", miktar: 10m);

        var iade = await service.CreateAsync(BuildSatisIadeRequest(
            _tesisId, _musteriKartId, asil.Id!.Value, _uniqueSuffix, new DateTime(2026, 3, 5), satirId, miktar: 4m));

        var oncekiDb = await ReadNoTrackingAsync(dbContext, iade.Id!.Value);

        await service.UpdateAsync(iade.Id.Value, new UpdateSatisBelgesiRequest { Aciklama = "Yeni aciklama" });

        await using var verifyContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var sonrakiDb = await ReadNoTrackingAsync(verifyContext, iade.Id.Value);

        Assert.Equal(
            oncekiDb.Satirlar.Single(x => !x.IsDeleted).KaynakSatirId,
            sonrakiDb.Satirlar.Single(x => !x.IsDeleted).KaynakSatirId);
        Assert.Equal(satirId.ToString(), sonrakiDb.Satirlar.Single(x => !x.IsDeleted).KaynakSatirId);
        Assert.Equal(asil.Id, sonrakiDb.IadeEdilenBelgeId);
        Assert.Equal("Yeni aciklama", sonrakiDb.Aciklama);

        // Onay akışı hâlâ başarıyla çalışabilmeli — bağlantı BOZULMAMIŞ.
        await service.MuhasebeOnayinaGonderAsync(iade.Id.Value);
        await service.MuhasebeOnaylaAsync(iade.Id.Value);
    }

    // ─────────────────────────────────────────────────────────────
    // 10b. Satirlar alanı AÇIKÇA (null DEĞİL) ama BOŞ gönderilirse 400 (bkz. görev 4a)
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task UpdateAsync_SatirlarAlaniAcikcaBosGonderilirse_400Doner()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var (asil, satirId, _) = await SeedOnaylanmisSatisFaturasiVeKesAsync(
            dbContext, _kurumId, _tesisId, _musteriKartId, _uniqueSuffix, new DateTime(2026, 3, 1), "K10B", miktar: 10m);

        var iade = await service.CreateAsync(BuildSatisIadeRequest(
            _tesisId, _musteriKartId, asil.Id!.Value, _uniqueSuffix, new DateTime(2026, 3, 5), satirId, miktar: 4m));

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.UpdateAsync(
            iade.Id!.Value, new UpdateSatisBelgesiRequest { Aciklama = "Deneme", Satirlar = [] }));

        Assert.Equal(400, ex.ErrorCode);

        // Satırlar/bağlantı DOKUNULMADAN kalmış olmalı - reddedilen istek hiçbir yan etki bırakmaz.
        var sonHal = await ReadNoTrackingAsync(dbContext, iade.Id!.Value);
        var aktifSatir = Assert.Single(sonHal.Satirlar, x => !x.IsDeleted);
        Assert.Equal(satirId.ToString(), aktifSatir.KaynakSatirId);
    }

    // ─────────────────────────────────────────────────────────────
    // 10c. IadeEdilenBelgeId değişir, Satirlar gönderilmezse: mevcut (dokunulmamış) satırların
    // YENİ kaynağa ait olduğu HER ZAMAN yeniden doğrulanır (bkz. görev 4b) - eski kaynağın
    // satırlarıyla yeni bir kaynak referansının BİRLİKTE kaydedilmesi mümkün OLMAMALIDIR.
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task UpdateAsync_IadeEdilenBelgeIdDegisirSatirlarGonderilmezse_EskiKaynakSatirYeniBelgeyeAitDegilseReddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        // İki AYRI, ikisi de GEÇERLİ (FaturaKesildi) asıl fatura.
        var (asilA, satirIdA, _) = await SeedOnaylanmisSatisFaturasiVeKesAsync(
            dbContext, _kurumId, _tesisId, _musteriKartId, _uniqueSuffix, new DateTime(2026, 3, 1), "K10CA", miktar: 10m);
        var (asilB, _, _) = await SeedOnaylanmisSatisFaturasiVeKesAsync(
            dbContext, _kurumId, _tesisId, _musteriKartId, _uniqueSuffix, new DateTime(2026, 3, 1), "K10CB", miktar: 10m);

        // İade, asilA'nın satirIdA'sına bağlı olarak oluşturulur.
        var iade = await service.CreateAsync(BuildSatisIadeRequest(
            _tesisId, _musteriKartId, asilA.Id!.Value, _uniqueSuffix, new DateTime(2026, 3, 5), satirIdA, miktar: 4m));

        // Yalnızca IadeEdilenBelgeId asilB'ye DEĞİŞTİRİLİR - Satirlar HİÇ gönderilmez (null); eski
        // satır (satirIdA, asilA'ya ait) SESSİZCE asilB ile birlikte kalabilseydi bu bir veri
        // tutarsızlığı olurdu - reddedilmelidir.
        var ex = await Assert.ThrowsAsync<BaseException>(() => service.UpdateAsync(
            iade.Id!.Value, new UpdateSatisBelgesiRequest { IadeEdilenBelgeId = asilB.Id!.Value }));

        Assert.Contains("iade edilen belgeye ait değil", ex.Message);
    }

    // ─────────────────────────────────────────────────────────────
    // 10d. Referans kaldırma + Satirlar=[] BİRLİKTE gönderilirse ATOMİK olarak kabul edilir:
    // referans kaldırılır VE mevcut satırlar soft-delete edilir (bkz. görev 2).
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task UpdateAsync_ReferansKaldirVeSatirlarBosGonderilirse_ReferansKaldirilirVeSatirlarSoftDeleteEdilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var (asil, satirId, _) = await SeedOnaylanmisSatisFaturasiVeKesAsync(
            dbContext, _kurumId, _tesisId, _musteriKartId, _uniqueSuffix, new DateTime(2026, 3, 1), "K10D", miktar: 10m);

        var iade = await service.CreateAsync(BuildSatisIadeRequest(
            _tesisId, _musteriKartId, asil.Id!.Value, _uniqueSuffix, new DateTime(2026, 3, 5), satirId, miktar: 4m));

        var guncellenen = await service.UpdateAsync(iade.Id!.Value, new UpdateSatisBelgesiRequest
        {
            IadeEdilenBelgeReferansiKaldir = true,
            Satirlar = []
        });

        Assert.Null(guncellenen.IadeEdilenBelgeId);

        var sonHal = await ReadNoTrackingAsync(dbContext, iade.Id.Value);
        Assert.Null(sonHal.IadeEdilenBelgeId);
        Assert.DoesNotContain(sonHal.Satirlar, x => !x.IsDeleted);
        // Eski satır SOFT-DELETE edilmiş olarak (KaynakSatirId'siyle birlikte) hâlâ DB'de durur -
        // fiziksel olarak silinmez, ama artık AKTİF (geçerli) bir referans TAŞIMAZ.
        var eskiSatir = Assert.Single(sonHal.Satirlar);
        Assert.True(eskiSatir.IsDeleted);
        Assert.Equal(satirId.ToString(), eskiSatir.KaynakSatirId);
    }

    [IntegrationFact]
    public async Task UpdateAsync_ReferansYokkenReferansKaldirBayragiIleSatirlarBosGonderilirse_400Doner()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        // Normal (iade OLMAYAN, dolayısıyla zaten IadeEdilenBelgeId'si null olan) bir belge.
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

        // IadeEdilenBelgeReferansiKaldir=true bayrağı ANLAMSIZDIR (kaldırılacak bir referans YOK) -
        // bu istismar/kaçış yolu olarak KULLANILAMAZ; Satirlar=[] genel kuralına göre 400 döner,
        // belgenin GERÇEK satırları SESSİZCE silinmez.
        var ex = await Assert.ThrowsAsync<BaseException>(() => service.UpdateAsync(created.Id!.Value, new UpdateSatisBelgesiRequest
        {
            IadeEdilenBelgeReferansiKaldir = true,
            Satirlar = []
        }));

        Assert.Equal(400, ex.ErrorCode);

        var sonHal = await ReadNoTrackingAsync(dbContext, created.Id!.Value);
        var aktifSatir = Assert.Single(sonHal.Satirlar, x => !x.IsDeleted);
        Assert.Equal("Test", aktifSatir.Aciklama);
    }

    // ─────────────────────────────────────────────────────────────
    // 11. Eşzamanlılık: iki eşzamanlı onaydan yalnızca sınırı aşmayan başarılı olmalı
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task SatisIadeFaturasi_EsZamanliIkiOnayaGonderme_YalnizcaSiniriAsmayanBasarili()
    {
        await using var setupContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var setupService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(setupContext);

        var (asil, satirId, miktar) = await SeedOnaylanmisSatisFaturasiVeKesAsync(
            setupContext, _kurumId, _tesisId, _musteriKartId, _uniqueSuffix, new DateTime(2026, 3, 1), "K11", miktar: 10m);

        // İkisi de TEK BAŞINA geçerli (6<=10), ama toplamları (12) asıl miktarı aşıyor.
        var iade1 = await setupService.CreateAsync(BuildSatisIadeRequest(
            _tesisId, _musteriKartId, asil.Id!.Value, _uniqueSuffix, new DateTime(2026, 3, 5), satirId, miktar: 6m));
        var iade2 = await setupService.CreateAsync(BuildSatisIadeRequest(
            _tesisId, _musteriKartId, asil.Id.Value, _uniqueSuffix, new DateTime(2026, 3, 6), satirId, miktar: 6m));

        async Task<(bool basarili, BaseException? hata)> OnayaGonderAsync(int belgeId)
        {
            try
            {
                await using var ctx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
                var svc = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(ctx);
                await svc.MuhasebeOnayinaGonderAsync(belgeId);
                return (true, null);
            }
            catch (BaseException ex)
            {
                return (false, ex);
            }
        }

        var sonuclar = await Task.WhenAll(OnayaGonderAsync(iade1.Id!.Value), OnayaGonderAsync(iade2.Id!.Value));

        Assert.Single(sonuclar, x => x.basarili);
        var basarisiz = Assert.Single(sonuclar, x => !x.basarili);
        Assert.Contains("toplam iade miktarı", basarisiz.hata!.Message);

        // Nihai durum tutarlı: yalnızca biri MuhasebeOnayinda, diğeri Taslak'ta kalmış olmalı.
        await using var verifyContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var iade1SonHal = await ReadNoTrackingAsync(verifyContext, iade1.Id.Value);
        var iade2SonHal = await ReadNoTrackingAsync(verifyContext, iade2.Id.Value);
        var durumlar = new[] { iade1SonHal.Durum, iade2SonHal.Durum };
        Assert.Contains(SatisBelgesiDurumu.MuhasebeOnayinda, durumlar);
        Assert.Contains(SatisBelgesiDurumu.Taslak, durumlar);
        _ = miktar;
    }

    // ─────────────────────────────────────────────────────────────
    // 12. Normal satış/alış faturalarının etkilenmemesi
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task SatisFaturasi_KaynakSatirIdOlmadanNormalAkis_Etkilenmez()
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
                    // KaynakSatirId verilmedi — normal SatisFaturasi'nda zorunlu DEĞİLDİR.
                }
            ]
        });

        await service.MuhasebeOnayinaGonderAsync(created.Id!.Value);
        await service.MuhasebeOnaylaAsync(created.Id.Value);

        var sonHal = await ReadNoTrackingAsync(dbContext, created.Id.Value);
        Assert.Equal(SatisBelgesiDurumu.MuhasebeOnaylandi, sonHal.Durum);
    }

    [IntegrationFact]
    public async Task AlisFaturasi_KaynakSatirIdOlmadanNormalAkis_Etkilenmez()
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

        await service.MuhasebeOnayinaGonderAsync(created.Id!.Value);
        await service.MuhasebeOnaylaAsync(created.Id.Value);

        var sonHal = await ReadNoTrackingAsync(dbContext, created.Id.Value);
        Assert.Equal(SatisBelgesiDurumu.MuhasebeOnaylandi, sonHal.Durum);
    }

    // ─────────────────────────────────────────────────────────────
    // 13. Kurum/tenant izolasyonu
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task SatisIadeFaturasi_BaskaKurumunKaynakSatiri_CreateSirasindaReddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var (asil, _, _) = await SeedOnaylanmisSatisFaturasiVeKesAsync(
            dbContext, _kurumId, _tesisId, _musteriKartId, _uniqueSuffix, new DateTime(2026, 3, 1), "K13", miktar: 10m);

        // Tamamen AYRI bir kurumun kendi geçerli (FaturaKesildi) asıl faturası ve satırı.
        var (_, baskaKurumSatirId, _) = await SeedOnaylanmisSatisFaturasiVeKesAsync(
            dbContext, _kurumId2, _tesisId2, _musteriKartId2, _uniqueSuffix2, new DateTime(2026, 3, 1), "T2K", miktar: 10m);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.CreateAsync(BuildSatisIadeRequest(
            _tesisId, _musteriKartId, asil.Id!.Value, _uniqueSuffix, new DateTime(2026, 3, 5), baskaKurumSatirId, miktar: 4m)));

        Assert.Contains("iade edilen belgeye ait değil", ex.Message);
    }

    // ─────────────────────────────────────────────────────────────
    // AlisIadeFaturasi — simetri kontrolü (aynı merkezi doğrulama, karşı yön)
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task AlisIadeFaturasi_GecerliTekKismiIade_KabulEdilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var (asil, satirId, miktar) = await SeedOnaylanmisAlisFaturasiAsync(
            dbContext, _tesisId, _tedarikciKartId, _uniqueSuffix, new DateTime(2026, 3, 1), miktar: 10m);

        var iade = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.AlisIadeFaturasi,
            TesisId = _tesisId,
            CariKartId = _tedarikciKartId,
            BelgeTarihi = new DateTime(2026, 3, 5),
            MusteriAdSoyad = "Tedarikci",
            IadeEdilenBelgeId = asil.Id,
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Iade", Miktar = 4m, BirimFiyat = 100m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m,
                    KaynakSatirId = satirId.ToString()
                }
            ]
        });

        await service.MuhasebeOnayinaGonderAsync(iade.Id!.Value);
        await service.MuhasebeOnaylaAsync(iade.Id.Value);

        var sonHal = await ReadNoTrackingAsync(dbContext, iade.Id.Value);
        Assert.Equal(SatisBelgesiDurumu.MuhasebeOnaylandi, sonHal.Durum);
        _ = miktar;
    }

    [IntegrationFact]
    public async Task AlisIadeFaturasi_KaynakSatirIdBulunmamasi_CreateSirasindaReddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var (asil, _, _) = await SeedOnaylanmisAlisFaturasiAsync(
            dbContext, _tesisId, _tedarikciKartId, _uniqueSuffix, new DateTime(2026, 3, 1), miktar: 10m);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.AlisIadeFaturasi,
            TesisId = _tesisId,
            CariKartId = _tedarikciKartId,
            BelgeTarihi = new DateTime(2026, 3, 5),
            MusteriAdSoyad = "Tedarikci",
            IadeEdilenBelgeId = asil.Id,
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Iade", Miktar = 4m, BirimFiyat = 100m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        }));

        Assert.Contains("kaynak satır referansı", ex.Message);
    }

    // ─────────────────────────────────────────────────────────────
    // Finansal alan tutarlılığı — kaynak satırla birim fiyat/oran uyumsuzluğu
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task SatisIadeFaturasi_BirimFiyatKaynakSatirdanFarkli_CreateSirasindaReddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var (asil, satirId, _) = await SeedOnaylanmisSatisFaturasiVeKesAsync(
            dbContext, _kurumId, _tesisId, _musteriKartId, _uniqueSuffix, new DateTime(2026, 3, 1), "KF1", miktar: 10m, birimFiyat: 100m);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.CreateAsync(BuildSatisIadeRequest(
            _tesisId, _musteriKartId, asil.Id!.Value, _uniqueSuffix, new DateTime(2026, 3, 5), satirId, miktar: 4m, birimFiyat: 150m)));

        Assert.Contains("birebir eşleşmelidir", ex.Message);
    }

    [IntegrationFact]
    public async Task SatisIadeFaturasi_KdvOraniKaynakSatirdanFarkli_CreateSirasindaReddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var (asil, satirId, _) = await SeedOnaylanmisSatisFaturasiVeKesAsync(
            dbContext, _kurumId, _tesisId, _musteriKartId, _uniqueSuffix, new DateTime(2026, 3, 1), "KF2", miktar: 10m);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.CreateAsync(BuildSatisIadeRequest(
            _tesisId, _musteriKartId, asil.Id!.Value, _uniqueSuffix, new DateTime(2026, 3, 5), satirId, miktar: 4m, kdvOrani: 10m)));

        Assert.Contains("birebir eşleşmelidir", ex.Message);
    }

    // ─────────────────────────────────────────────────────────────
    // Soft-delete edilmiş önceki iadenin toplam davranışı — İPTALDEN AYRI, bağımsız test
    // (bkz. SatisIadeFaturasi_IptalEdilmisOncekiIade_KumulatifToplamaDahilDegil - soft-delete
    // davranışı o testten VARSAYILMAZ, burada ayrıca doğrulanır).
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task SatisIadeFaturasi_SoftDeleteEdilmisOncekiIade_KumulatifToplamaDahilDegil()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var (asil, satirId, miktar) = await SeedOnaylanmisSatisFaturasiVeKesAsync(
            dbContext, _kurumId, _tesisId, _musteriKartId, _uniqueSuffix, new DateTime(2026, 3, 1), "SD1", miktar: 10m);

        // 1. iade geçerli (MuhasebeOnaylandi) bir duruma getirilir, ardından SOFT-DELETE edilir
        // (İPTAL EDİLMEZ - Durum kasten MuhasebeOnaylandi olarak KALIR, yalnızca IsDeleted=true
        // yapılır). Bu, iptal ile soft-delete'in AYRI/bağımsız iki koşul olduğunu kanıtlar.
        var iade1 = await service.CreateAsync(BuildSatisIadeRequest(
            _tesisId, _musteriKartId, asil.Id!.Value, _uniqueSuffix, new DateTime(2026, 3, 5), satirId, miktar: 8m));
        await service.MuhasebeOnayinaGonderAsync(iade1.Id!.Value);
        await service.MuhasebeOnaylaAsync(iade1.Id.Value);

        var iade1Db = await dbContext.SatisBelgeleri.FirstAsync(x => x.Id == iade1.Id);
        iade1Db.IsDeleted = true;
        await dbContext.SaveChangesAsync();

        // 2. iade, soft-delete edilmiş 1. iadeyle birlikte toplansaydı (8+8=16>10) reddedilirdi;
        // soft-delete edilmiş iade toplama DAHİL EDİLMEDİĞİNDEN (yalnızca 8<=10) BAŞARILI olmalıdır.
        var iade2 = await service.CreateAsync(BuildSatisIadeRequest(
            _tesisId, _musteriKartId, asil.Id.Value, _uniqueSuffix, new DateTime(2026, 3, 6), satirId, miktar: 8m));
        await service.MuhasebeOnayinaGonderAsync(iade2.Id!.Value);
        await service.MuhasebeOnaylaAsync(iade2.Id.Value);

        var iade2SonHal = await ReadNoTrackingAsync(dbContext, iade2.Id.Value);
        Assert.Equal(SatisBelgesiDurumu.MuhasebeOnaylandi, iade2SonHal.Durum);
        _ = miktar;
    }

    // ─────────────────────────────────────────────────────────────
    // Kanonik KaynakSatirId biçimi — "00123" vb. baştaki sıfırlı gösterimler
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task SatisIadeFaturasi_KaynakSatirIdBastaSifirlarla_KanonikBicimdeSaklanir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var (asil, satirId, _) = await SeedOnaylanmisSatisFaturasiVeKesAsync(
            dbContext, _kurumId, _tesisId, _musteriKartId, _uniqueSuffix, new DateTime(2026, 3, 1), "CN1", miktar: 10m);

        var request = BuildSatisIadeRequest(
            _tesisId, _musteriKartId, asil.Id!.Value, _uniqueSuffix, new DateTime(2026, 3, 5), satirId, miktar: 4m);
        // Kaynak satır kimliğini KASTEN baştaki sıfırlarla gönder ("00123" biçimi).
        request.Satirlar[0].KaynakSatirId = "00" + satirId;

        var iade = await service.CreateAsync(request);

        var sonHal = await ReadNoTrackingAsync(dbContext, iade.Id!.Value);
        Assert.Equal(satirId.ToString(), sonHal.Satirlar.Single(x => !x.IsDeleted).KaynakSatirId);
    }

    [IntegrationFact]
    public async Task SatisIadeFaturasi_FarkliSayisalGosterimler_KumulatifSiniriBirlikteAsamaz()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var (asil, satirId, miktar) = await SeedOnaylanmisSatisFaturasiVeKesAsync(
            dbContext, _kurumId, _tesisId, _musteriKartId, _uniqueSuffix, new DateTime(2026, 3, 1), "CN2", miktar: 10m);

        // 1. iade, KaynakSatirId "00123" (baştaki sıfırlı) biçiminde GÖNDERİLİR - kanonik biçime
        // ("123") ÇEVRİLEREK kaydedilir (bkz. yukarıdaki test) ve onaylanır (6 <= 10).
        var request1 = BuildSatisIadeRequest(
            _tesisId, _musteriKartId, asil.Id!.Value, _uniqueSuffix, new DateTime(2026, 3, 5), satirId, miktar: 6m);
        request1.Satirlar[0].KaynakSatirId = "00" + satirId;
        var iade1 = await service.CreateAsync(request1);
        await service.MuhasebeOnayinaGonderAsync(iade1.Id!.Value);
        await service.MuhasebeOnaylaAsync(iade1.Id.Value);

        // 2. iade, AYNI kaynak satırı DÜZ ("123") biçiminde gösterir - TEK BAŞINA geçerli (6<=10),
        // ama 1. iade ile KÜMÜLATİF toplam (6+6=12>10) asıl miktarı aşar. Düzeltmeden ÖNCE, 1.
        // iadenin "00123" olarak saklanması nedeniyle metinsel eşitlik sorgusu bu satırı
        // GÖREMEZDİ ve bu istek YANLIŞLIKLA başarılı olurdu - artık reddedilmelidir.
        var request2 = BuildSatisIadeRequest(
            _tesisId, _musteriKartId, asil.Id.Value, _uniqueSuffix, new DateTime(2026, 3, 6), satirId, miktar: 6m);
        var iade2 = await service.CreateAsync(request2);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.MuhasebeOnayinaGonderAsync(iade2.Id!.Value));
        Assert.Contains("toplam iade miktarı", ex.Message);

        var iade2SonHal = await ReadNoTrackingAsync(dbContext, iade2.Id!.Value);
        Assert.Equal(SatisBelgesiDurumu.Taslak, iade2SonHal.Durum);
        _ = miktar;
    }

    [IntegrationFact]
    public async Task SatisIadeFaturasi_DogrudanSeedEdilmisKanonikOlmayanOncekiIade_KumulatifToplamaDahilEdilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var (asil, satirId, miktar) = await SeedOnaylanmisSatisFaturasiVeKesAsync(
            dbContext, _kurumId, _tesisId, _musteriKartId, _uniqueSuffix, new DateTime(2026, 3, 1), "CN3", miktar: 10m);

        // Uygulama servisini (ve dolayısıyla ValidateIadeSatirlariAsync'in kanonikleştirmesini)
        // BAYPAS EDEREK, doğrudan DbContext üzerinden, kanonik OLMAYAN ("00" + satirId) bir
        // KaynakSatirId ile ZATEN MuhasebeOnaylandi durumunda olan bir "eski" iade satırı seed
        // edilir - bu, düzeltmeden ÖNCE veya harici bir veri aktarımıyla oluşmuş kanonik olmayan
        // bir kaydı temsil eder.
        var eskiIade = new SatisBelgesi
        {
            KurumId = _kurumId,
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.SatisIadeFaturasi,
            Durum = SatisBelgesiDurumu.MuhasebeOnaylandi,
            TicariDurum = TicariBelgeDurumu.Hazir,
            MuhasebeDurumu = TicariBelgeMuhasebeDurumu.Onaylandi,
            FaturalamaDurumu = TicariBelgeFaturalamaDurumu.Uygulanamaz,
            TesisId = _tesisId,
            CariKartId = _musteriKartId,
            BelgeTarihi = new DateTime(2026, 3, 5),
            MusteriAdSoyad = "Musteri " + _uniqueSuffix,
            KurumsalMi = false,
            IadeEdilenBelgeId = asil.Id,
            ToplamMatrah = 800m,
            ToplamKdv = 160m,
            GenelToplam = 960m,
            Satirlar =
            [
                new SatisBelgesiSatiri
                {
                    SiraNo = 1,
                    Aciklama = "Kanonik olmayan eski iade satiri",
                    Birim = "Adet",
                    Miktar = 8m,
                    BirimFiyat = 100m,
                    KdvUygulamaTipi = KdvUygulamaTipi.Kdvli,
                    KdvOrani = 20m,
                    KdvTutari = 160m,
                    Matrah = 800m,
                    SatirToplami = 960m,
                    KaynakSatirId = "00" + satirId
                }
            ]
        };
        dbContext.SatisBelgeleri.Add(eskiIade);
        await dbContext.SaveChangesAsync();

        var eskiIadeDbSatir = await dbContext.SatisBelgesiSatirlari.AsNoTracking()
            .FirstAsync(x => x.SatisBelgesiId == eskiIade.Id);
        Assert.Equal("00" + satirId, eskiIadeDbSatir.KaynakSatirId); // seed'in kanonik OLMADIĞI doğrulanır.

        // Yeni iade, AYNI kaynak satırı kanonik ("123") biçimde gösterir - TEK BAŞINA geçerli
        // (8<=10), ama eski (kanonik olmayan, doğrudan seed edilmiş) iadeyle KÜMÜLATİF toplam
        // (8+8=16>10) asıl miktarı aşar - sorgu SAYISAL eşitlik kullandığından eski kayıt
        // GÖRÜLÜR ve istek reddedilir.
        var yeniIade = await service.CreateAsync(BuildSatisIadeRequest(
            _tesisId, _musteriKartId, asil.Id!.Value, _uniqueSuffix, new DateTime(2026, 3, 6), satirId, miktar: 8m));

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.MuhasebeOnayinaGonderAsync(yeniIade.Id!.Value));
        Assert.Contains("toplam iade miktarı", ex.Message);

        var yeniIadeSonHal = await ReadNoTrackingAsync(dbContext, yeniIade.Id!.Value);
        Assert.Equal(SatisBelgesiDurumu.Taslak, yeniIadeSonHal.Durum);
        _ = miktar;
    }
}
