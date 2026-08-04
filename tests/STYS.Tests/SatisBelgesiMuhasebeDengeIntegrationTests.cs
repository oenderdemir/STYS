using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Services;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Part A — dc861c7 (vergi toplamı/yuvarlama) ve önceki turların ürettiği SatirToplami/GenelToplam
/// formülünün, GERÇEK SQL Server üzerinde, GERÇEK public servis akışlarıyla (SatisBelgesiService
/// .CreateAsync/MuhasebeOnayinaGonderAsync/MuhasebeOnaylaAsync ve SatisBelgesiMuhasebeFisService
/// .MuhasebeFisiOlusturAsync) uçtan uca doğru sonuç ürettiğini ve muhasebe fişinin dengeli
/// kaldığını doğrulayan regresyon testleri.
///
/// Her test kendi izole Kurum/Il/Tesis/hesap planı/cari kart kümesini oluşturur (InitializeAsync)
/// ve testten sonra temizler (DisposeAsync) — RezervasyonOdemeMuhasebeIntegrationTests ile aynı
/// konvansiyon.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class SatisBelgesiMuhasebeDengeIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "DENGE-782";

    private string _uniqueSuffix = TestMarker;
    private int _kurumId;
    private int _ilId;
    private int _tesisId;
    private int _gelirHesapId;
    private int _kdvSatisHesapId;
    private int _kdvAlisHesapId;
    private int _giderHesapId;
    private int _satisIadeHesapId;
    private int _musteriCariKartId;
    private int _musteriHesapId;
    private int _tedarikciCariKartId;
    private int _tedarikciHesapId;
    private int _tevkifatKarsiligiHesapId;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString))
        {
            return;
        }

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        var (kurum, il, tesis) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(dbContext, _uniqueSuffix);
        _kurumId = kurum.Id;
        _ilId = il.Id;
        _tesisId = tesis.Id;

        var gelirHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.GelirSatis, "GELIR", tesis.Id);
        var kdvSatisHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.KDVHesaplanan, "KDVS", tesis.Id);
        var kdvAlisHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.KDVIndirilecek, "KDVA", tesis.Id);
        var giderHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.GiderHizmetMaliyet, "GIDER", tesis.Id);
        var satisIadeHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.SatisIade, "IADE", tesis.Id);
        var stokHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.StokTicariMal, "STOK", tesis.Id);
        var musteriHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "MUS", tesis.Id);
        var tedarikciHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "TED", tesis.Id);
        var tevkifatKarsiligiHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "TEVK", tesis.Id);
        dbContext.MuhasebeHesapPlanlari.AddRange(
            gelirHesap, kdvSatisHesap, kdvAlisHesap, giderHesap, satisIadeHesap, stokHesap, musteriHesap, tedarikciHesap, tevkifatKarsiligiHesap);
        await dbContext.SaveChangesAsync();
        _gelirHesapId = gelirHesap.Id;
        _kdvSatisHesapId = kdvSatisHesap.Id;
        _kdvAlisHesapId = kdvAlisHesap.Id;
        _giderHesapId = giderHesap.Id;
        _satisIadeHesapId = satisIadeHesap.Id;
        _musteriHesapId = musteriHesap.Id;
        _tedarikciHesapId = tedarikciHesap.Id;
        _tevkifatKarsiligiHesapId = tevkifatKarsiligiHesap.Id;

        var musteri = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "MUS", CariKartTipleri.Musteri, tesis.Id, musteriHesap.Id);
        // Bu testin amacı e-belge kanalı değil, muhasebe fişi dengesidir - ama FaturaKesAsync
        // artık kanalı sayaç kilidinden ÖNCE çözüyor (bkz. Faz 2B.4.2); mükellefiyet bayrağı
        // ayarlanmazsa kesim "her iki mükellefiyet bayrağı da kapalı" hatasıyla reddedilir.
        musteri.EArsivKapsamindaMi = true;
        var tedarikci = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "TED", CariKartTipleri.Tedarikci, tesis.Id, tedarikciHesap.Id);
        tedarikci.VergiNoTckn = "1111111111";
        dbContext.CariKartlar.AddRange(musteri, tedarikci);
        await dbContext.SaveChangesAsync();
        _musteriCariKartId = musteri.Id;
        _tedarikciCariKartId = tedarikci.Id;
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString) || _kurumId <= 0)
        {
            return;
        }

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        // Bu sınıftaki iade testi FaturaKesAsync için kurum bazlı bir sayaç seed ediyor - paylaşılan
        // CleanupAsync bu tabloyu bilmediğinden, Kurum silinmeden ÖNCE burada elle temizlenir.
        await dbContext.KurumFaturaNumaraSayaclari.Where(x => x.KurumId == _kurumId).ExecuteDeleteAsync();
        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, _uniqueSuffix, _tesisId, _kurumId, _ilId);
    }

    // ─────────────────────────────────────────────────────────────
    // Standart / tevkifatlı / iade — 6 belge tipi, hepsi denge testi
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task StandartSatisFaturasi_FisOlusurVeDengeliKalir()
    {
        var request = YeniBelgeRequest(SatisBelgesiTipi.SatisFaturasi, _musteriCariKartId, [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Oda ucreti", Miktar = 1, BirimFiyat = 1000m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
            }
        ]);

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, request);

        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);
        var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);

        await AssertFisDengeliVeTutarliAsync(dbContext, onaylanmis.Id.Value, dto.MuhasebeFisId);

        var cariHareket = await ReadCariHareketAsync(dbContext, onaylanmis.Id.Value);
        Assert.Equal(1200m, cariHareket.BorcTutari); // satış: müşteri borçlanır
        Assert.Equal(0m, cariHareket.AlacakTutari);
    }

    [IntegrationFact]
    public async Task StandartAlisFaturasi_FisOlusurVeDengeliKalir()
    {
        var request = YeniAlisBelgeRequest(SatisBelgesiTipi.AlisFaturasi, _tedarikciCariKartId, [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Hizmet alimi", Miktar = 1, BirimFiyat = 1000m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
            }
        ]);

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, request);

        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);
        var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);

        await AssertFisDengeliVeTutarliAsync(dbContext, onaylanmis.Id.Value, dto.MuhasebeFisId);

        var cariHareket = await ReadCariHareketAsync(dbContext, onaylanmis.Id.Value);
        Assert.Equal(0m, cariHareket.BorcTutari);
        Assert.Equal(1200m, cariHareket.AlacakTutari); // alış: tedarikçiye borçlanılır (alacak)
    }

    [IntegrationFact]
    public async Task SatisTevkifatliFatura_FisOlusurVeDengeliKalir()
    {
        var request = YeniBelgeRequest(SatisBelgesiTipi.SatisFaturasi, _musteriCariKartId, [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Tevkifatli hizmet", Miktar = 1, BirimFiyat = 1000m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Tevkifatli, KdvOrani = 20m,
                TevkifatPay = 7, TevkifatPayda = 10
            }
        ]);

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, request);

        Assert.Equal(1060m, onaylanmis.GenelToplam); // 1000+200-140

        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext, tevkifatKarsiligiHesapPlaniId: _tevkifatKarsiligiHesapId);
        var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);

        await AssertFisDengeliVeTutarliAsync(dbContext, onaylanmis.Id.Value, dto.MuhasebeFisId);
    }

    [IntegrationFact]
    public async Task AlisTevkifatliFatura_FisOlusurVeDengeliKalir()
    {
        var request = YeniAlisBelgeRequest(SatisBelgesiTipi.AlisFaturasi, _tedarikciCariKartId, [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Tevkifatli hizmet alimi", Miktar = 1, BirimFiyat = 1000m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Tevkifatli, KdvOrani = 20m,
                TevkifatPay = 5, TevkifatPayda = 10
            }
        ]);

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, request);

        Assert.Equal(1100m, onaylanmis.GenelToplam); // 1000+200-100

        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext, tevkifatKarsiligiHesapPlaniId: _tevkifatKarsiligiHesapId);
        var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);

        await AssertFisDengeliVeTutarliAsync(dbContext, onaylanmis.Id.Value, dto.MuhasebeFisId);
    }

    [IntegrationFact]
    public async Task SatisIadeFaturasi_GercekIadeStratejisiyleFisOlusurVeDengeliKalir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);

        // SatisIadeFaturasi artık geçerli bir IadeEdilenBelgeId (FaturaKesildi durumunda, aynı
        // müşteriye ait bir SatisFaturasi) gerektirir.
        var asilRequest = YeniBelgeRequest(SatisBelgesiTipi.SatisFaturasi, _musteriCariKartId, [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Asil konaklama", Miktar = 1, BirimFiyat = 800m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 10m
            }
        ]);
        var asilOnaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, asilRequest);
        await fisService.MuhasebeFisiOlusturAsync(asilOnaylanmis.Id!.Value, CancellationToken.None);

        dbContext.KurumFaturaNumaraSayaclari.Add(new STYS.Muhasebe.SatisBelgeleri.Entities.KurumFaturaNumaraSayaci
        {
            KurumId = _kurumId, MaliYil = 2026, SeriKodu = "DGE", SonNumara = 0, AktifMi = true
        });
        await dbContext.SaveChangesAsync();
        var asilFaturaKesildi = await satisService.FaturaKesAsync(asilOnaylanmis.Id!.Value, new FaturaKesRequest { SeriKodu = "DGE" });

        var request = YeniBelgeRequest(SatisBelgesiTipi.SatisIadeFaturasi, _musteriCariKartId, [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Iade edilen konaklama", Miktar = 1, BirimFiyat = 800m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 10m,
                KaynakSatirId = asilFaturaKesildi.Satirlar[0].Id!.Value.ToString()
            }
        ]);
        request.KarsiTarafFaturaNo = $"MUS-{Guid.NewGuid():N}"[..20];
        request.IadeEdilenBelgeId = asilFaturaKesildi.Id;

        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, request);

        var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);

        await AssertFisDengeliVeTutarliAsync(dbContext, onaylanmis.Id.Value, dto.MuhasebeFisId);

        var fis = await dbContext.MuhasebeFisler.AsNoTracking().Include(x => x.Satirlar)
            .FirstAsync(x => x.Id == dto.MuhasebeFisId);
        Assert.Contains(fis.Satirlar, s => s.MuhasebeHesapPlaniId == _satisIadeHesapId);

        var cariHareket = await ReadCariHareketAsync(dbContext, onaylanmis.Id.Value);
        Assert.Equal(0m, cariHareket.BorcTutari);
        Assert.Equal(880m, cariHareket.AlacakTutari); // satış iade: müşteri alacaklanır
    }

    [IntegrationFact]
    public async Task AlisIadeFaturasi_GercekIadeStratejisiyleFisOlusurVeDengeliKalir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);

        // AlisIadeFaturasi artık geçerli bir IadeEdilenBelgeId (muhasebe onaylı, aynı tedarikçiye
        // ait bir AlisFaturasi) gerektirir.
        var asilRequest = YeniAlisBelgeRequest(SatisBelgesiTipi.AlisFaturasi, _tedarikciCariKartId, [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Asil hizmet alimi", Miktar = 1, BirimFiyat = 500m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
            }
        ]);
        var asilOnaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, asilRequest);
        await fisService.MuhasebeFisiOlusturAsync(asilOnaylanmis.Id!.Value, CancellationToken.None);

        var request = YeniBelgeRequest(SatisBelgesiTipi.AlisIadeFaturasi, _tedarikciCariKartId, [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Iade edilen hizmet", Miktar = 1, BirimFiyat = 500m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m,
                KaynakSatirId = asilOnaylanmis.Satirlar[0].Id!.Value.ToString()
            }
        ]);
        request.IadeEdilenBelgeId = asilOnaylanmis.Id;

        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, request);

        var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);

        await AssertFisDengeliVeTutarliAsync(dbContext, onaylanmis.Id.Value, dto.MuhasebeFisId);

        var cariHareket = await ReadCariHareketAsync(dbContext, onaylanmis.Id.Value);
        Assert.Equal(600m, cariHareket.BorcTutari); // alış iade: tedarikçi borçlanır (bize)
        Assert.Equal(0m, cariHareket.AlacakTutari);
    }

    // ─────────────────────────────────────────────────────────────
    // Yuvarlama — 0,005 sınır değeri + doğrudan tutar girilen senaryo
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task BirdenFazlaSatirda_KdvMidpointYuvarlamasi_BelgeVeFisToplaminaTutarliYansir()
    {
        // Her satir: Matrah=10.00, KdvOrani=8.25% -> ham KDV = 0.825 (tam 0,005 sinirinda).
        // AwayFromZero ile satir bazinda 0.83'e yuvarlanmali; 3 ozdes satirin DOGRU toplami
        // 3*10.83=32.49'dur (naif "once topla, sonra tek seferde yuvarla" yontemiyle 32.48
        // cikardi - bu FARKI acikca dogrular).
        var satirlar = Enumerable.Range(1, 3).Select(i => new CreateSatisBelgesiSatiriRequest
        {
            SiraNo = i, Aciklama = $"Yuvarlama satiri {i}", Miktar = 1, BirimFiyat = 10.00m,
            KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 8.25m
        }).ToList();

        var request = YeniBelgeRequest(SatisBelgesiTipi.SatisFaturasi, _musteriCariKartId, satirlar);

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, request);

        // Veritabanindan NO-TRACKING tekrar oku - entity uzerindeki (henuz yazilmamis olabilecek)
        // degerlere degil, GERCEKTEN kalici olan decimal(18,2) degerlere bakiyoruz.
        var satirlarDb = await dbContext.SatisBelgesiSatirlari
            .AsNoTracking()
            .Where(x => x.SatisBelgesiId == onaylanmis.Id)
            .ToListAsync();

        Assert.All(satirlarDb, s => Assert.Equal(0.83m, s.KdvTutari));
        Assert.All(satirlarDb, s => Assert.Equal(10.83m, s.SatirToplami));

        var belgeDb = await dbContext.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.Id == onaylanmis.Id);
        Assert.Equal(32.49m, belgeDb.GenelToplam);
        Assert.NotEqual(32.48m, belgeDb.GenelToplam);

        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);
        var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);

        var fis = await dbContext.MuhasebeFisler.AsNoTracking().FirstAsync(x => x.Id == dto.MuhasebeFisId);
        Assert.Equal(32.49m, fis.ToplamBorc);
        Assert.Equal(32.49m, fis.ToplamAlacak);
        Assert.Equal(fis.ToplamBorc, fis.ToplamAlacak);
    }

    [IntegrationFact]
    public async Task DogrudanTutarGirilenIndirim_MidpointYuvarlamasi_VeritabaninaYuvarlanmisYazilir()
    {
        // IndirimOrani=0 -> ResolveRateBasedAmount fallback dalina duser, dogrudan girilen
        // IndirimTutari (33.335, tam 0,005 sinirinda) de AwayFromZero ile yuvarlanmalidir.
        // Matrah = 100 - 33.34 = 66.66 ; Kdv = 66.66*20% = 13.332 -> 13.33 ; Toplam = 79.99.
        var request = YeniBelgeRequest(SatisBelgesiTipi.SatisFaturasi, _musteriCariKartId, [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Dogrudan indirim tutari", Miktar = 1, BirimFiyat = 100m,
                IndirimTutari = 33.335m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
            }
        ]);

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, request);

        var satirDb = await dbContext.SatisBelgesiSatirlari.AsNoTracking().FirstAsync(x => x.SatisBelgesiId == onaylanmis.Id);
        Assert.Equal(33.34m, satirDb.IndirimTutari);
        Assert.Equal(66.66m, satirDb.Matrah);
        Assert.Equal(13.33m, satirDb.KdvTutari);
        Assert.Equal(79.99m, satirDb.SatirToplami);

        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);
        var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);

        var fis = await dbContext.MuhasebeFisler.AsNoTracking().FirstAsync(x => x.Id == dto.MuhasebeFisId);
        Assert.Equal(79.99m, fis.ToplamBorc);
        Assert.Equal(fis.ToplamBorc, fis.ToplamAlacak);
    }

    // ─────────────────────────────────────────────────────────────
    // Part E — gerçek MuhasebeDonem kaydı ve gerçek MuhasebeDonemService
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task GercekMuhasebeDonemKaydiyla_FisTesisMaliYilDonemVeTarihiTransactionIcindekiBelgeyleTutarli()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();

        // GERÇEK bir MuhasebeDonem satırı — FakeMuhasebeDonemService DEĞİL.
        var donem = new STYS.Muhasebe.MuhasebeDonemleri.Entities.MuhasebeDonem
        {
            TesisId = _tesisId,
            MaliYil = 2026,
            DonemNo = 3,
            BaslangicTarihi = new DateTime(2026, 3, 1),
            BitisTarihi = new DateTime(2026, 3, 31),
            KapaliMi = false
        };
        dbContext.MuhasebeDonemler.Add(donem);
        await dbContext.SaveChangesAsync();

        var belgeTarihi = new DateTime(2026, 3, 15);
        var request = YeniBelgeRequest(SatisBelgesiTipi.SatisFaturasi, _musteriCariKartId, [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Donem testi", Miktar = 1, BirimFiyat = 1000m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
            }
        ], belgeTarihi);

        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, request);

        var realDonemService = SatisBelgesiMuhasebeTestSupport.CreateRealMuhasebeDonemService(dbContext);
        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext, realDonemService);
        var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);

        var fis = await dbContext.MuhasebeFisler.AsNoTracking().FirstAsync(x => x.Id == dto.MuhasebeFisId);
        Assert.Equal(_tesisId, fis.TesisId);
        Assert.Equal(2026, fis.MaliYil);
        Assert.Equal(3, fis.Donem);
        Assert.Equal(belgeTarihi, fis.FisTarihi);

        var belgeDb = await dbContext.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.Id == onaylanmis.Id);
        Assert.Equal(belgeDb.TesisId, fis.TesisId);
        Assert.Equal(belgeDb.BelgeTarihi, fis.FisTarihi);
    }

    // ─────────────────────────────────────────────────────────────
    // Yardımcılar
    // ─────────────────────────────────────────────────────────────

    private CreateSatisBelgesiRequest YeniBelgeRequest(
        SatisBelgesiTipi belgeTipi, int cariKartId, List<CreateSatisBelgesiSatiriRequest> satirlar, DateTime? belgeTarihi = null) => new()
    {
        BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
        BelgeTipi = belgeTipi,
        TesisId = _tesisId,
        CariKartId = cariKartId,
        BelgeTarihi = belgeTarihi ?? new DateTime(2026, 1, 15),
        Satirlar = satirlar
    };

    /// <summary>AlisFaturasi (gelen belge) için KarsiTarafFaturaNo'yu otomatik doldurarak YeniBelgeRequest'i sarmalar - onay aşamasında artık zorunludur.</summary>
    private CreateSatisBelgesiRequest YeniAlisBelgeRequest(
        SatisBelgesiTipi belgeTipi, int cariKartId, List<CreateSatisBelgesiSatiriRequest> satirlar, DateTime? belgeTarihi = null)
    {
        var request = YeniBelgeRequest(belgeTipi, cariKartId, satirlar, belgeTarihi);
        if (belgeTipi == SatisBelgesiTipi.AlisFaturasi)
            request.KarsiTarafFaturaNo = $"TED-{Guid.NewGuid():N}"[..20];
        return request;
    }

    private static async Task<CariHareket> ReadCariHareketAsync(StysAppDbContext dbContext, int belgeId)
        => await dbContext.CariHareketler.AsNoTracking().FirstAsync(x => x.KaynakId == belgeId);

    /// <summary>
    /// A. bölümünde tekrar eden ortak doğrulama: fiş gerçekten oluşmuş, en az bir satır var,
    /// ToplamBorc==ToplamAlacak, fiş ana kaydındaki toplamlar satır toplamlarıyla aynı, ve
    /// belgenin MuhasebeFisId'si oluşan fişi gösteriyor — HEPSİ no-tracking, yeniden okunan
    /// kayıtlar üzerinden.
    /// </summary>
    private static async Task AssertFisDengeliVeTutarliAsync(StysAppDbContext dbContext, int belgeId, int? muhasebeFisId)
    {
        Assert.NotNull(muhasebeFisId);

        var fis = await dbContext.MuhasebeFisler.AsNoTracking()
            .Include(x => x.Satirlar)
            .FirstOrDefaultAsync(x => x.Id == muhasebeFisId);
        Assert.NotNull(fis);
        Assert.NotEmpty(fis!.Satirlar);

        var satirToplamBorc = fis.Satirlar.Sum(s => s.Borc);
        var satirToplamAlacak = fis.Satirlar.Sum(s => s.Alacak);
        Assert.Equal(fis.ToplamBorc, fis.ToplamAlacak);
        Assert.Equal(satirToplamBorc, fis.ToplamBorc);
        Assert.Equal(satirToplamAlacak, fis.ToplamAlacak);

        var belgeDb = await dbContext.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.Id == belgeId);
        Assert.Equal(fis.Id, belgeDb.MuhasebeFisId);
    }
}
