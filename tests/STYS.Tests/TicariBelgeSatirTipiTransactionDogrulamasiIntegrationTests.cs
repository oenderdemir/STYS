using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Depolar.Entities;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Services;
using STYS.Muhasebe.TasinirKartlari.Entities;
using STYS.Muhasebe.TasinirKodlari.Entities;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// SatirTipi semantiğinin (bkz. b8a38f6 sonrası görev) artık merkezi SatisBelgesiSatirTipiSemantigi
/// üzerinden hem YAZMA anında (create/update) hem de muhasebe fişi oluşturma TRANSACTION'ı içinde,
/// veritabanından YENİDEN OKUNAN gerçek satırlar üzerinde doğrulandığını GERÇEK SQL Server'a karşı
/// kanıtlayan hedefli entegrasyon testleri. İkinci doğrulama noktası özellikle, servis katmanındaki
/// yazma-anı kontrolünü ATLATMIŞ (burada doğrudan SQL/EF ile üretilen) tutarsız satırların sessizce
/// yanlış hesaba aktarılmadığını, ve böyle bir durumda hiçbir fiş/cari/stok kaydının oluşmadığını
/// (tam rollback) doğrular.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class TicariBelgeSatirTipiTransactionDogrulamasiIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "TBSATIRTX-972";

    private string _uniqueSuffix = TestMarker;
    private int _kurumId;
    private int _ilId;
    private int _tesisId;
    private int _giderHesapId;
    private int _stokHesapId;
    private int _kdvAlisHesapId;
    private int _tedarikciKartId;
    private int _tedarikciHesapId;
    private int _depoId;
    private int _tasinirKartId;
    private int _tasinirKodId;

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

        var giderHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.GiderHizmetMaliyet, "GIDER", _tesisId);
        var stokHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.StokTicariMal, "STOK", _tesisId);
        var kdvAlisHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.KDVIndirilecek, "KDVA", _tesisId);
        var tedarikciHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "TED", _tesisId);
        dbContext.MuhasebeHesapPlanlari.AddRange(giderHesap, stokHesap, kdvAlisHesap, tedarikciHesap);
        await dbContext.SaveChangesAsync();
        _giderHesapId = giderHesap.Id;
        _stokHesapId = stokHesap.Id;
        _kdvAlisHesapId = kdvAlisHesap.Id;
        _tedarikciHesapId = tedarikciHesap.Id;

        var tedarikci = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "TED", CariKartTipleri.Tedarikci, _tesisId, tedarikciHesap.Id);
        tedarikci.VergiNoTckn = "7777777777";
        dbContext.CariKartlar.Add(tedarikci);
        await dbContext.SaveChangesAsync();
        _tedarikciKartId = tedarikci.Id;

        var depo = new Depo { TesisId = _tesisId, Kod = $"DP-{_uniqueSuffix}", Ad = "Test Depo " + _uniqueSuffix, AktifMi = true };
        dbContext.Depolar.Add(depo);
        await dbContext.SaveChangesAsync();
        _depoId = depo.Id;

        var tasinirKod = new TasinirKod
        {
            TamKod = $"TK-{_uniqueSuffix}", Kod = Guid.NewGuid().ToString("N")[..16], Ad = "Test Taşınır Kod " + _uniqueSuffix,
            DuzeyNo = 1, AktifMi = true
        };
        dbContext.TasinirKodlar.Add(tasinirKod);
        await dbContext.SaveChangesAsync();
        _tasinirKodId = tasinirKod.Id;

        var tasinirKart = new TasinirKart
        {
            TesisId = _tesisId, TasinirKodId = tasinirKod.Id, MuhasebeHesapPlaniId = stokHesap.Id,
            StokKodu = $"SK-{_uniqueSuffix}", Ad = "Test Ürün " + _uniqueSuffix, Birim = "Adet", AktifMi = true
        };
        dbContext.TasinirKartlar.Add(tasinirKart);
        await dbContext.SaveChangesAsync();
        _tasinirKartId = tasinirKart.Id;
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString) || _kurumId <= 0)
        {
            return;
        }

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await dbContext.KurumFaturaNumaraSayaclari.Where(x => x.KurumId == _kurumId).ExecuteDeleteAsync();
        // İKİ AŞAMALI temizlik - belgeler/satırlar önce, taşınır kart/kod/depo sonra, tesis/il/
        // kurum en son (aynı desen - bkz. önceki tur test dosyaları).
        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, _uniqueSuffix);
        await dbContext.TasinirKartlar.Where(x => x.Id == _tasinirKartId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.MuhasebeHesapPlaniId, (int?)null));
        await dbContext.StokHareketleri.Where(x => x.TasinirKartId == _tasinirKartId).ExecuteDeleteAsync();
        await dbContext.TasinirKartlar.Where(x => x.Id == _tasinirKartId).ExecuteDeleteAsync();
        await dbContext.TasinirKodlar.Where(x => x.Id == _tasinirKodId).ExecuteDeleteAsync();
        await dbContext.Depolar.Where(x => x.Id == _depoId).ExecuteDeleteAsync();
        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, _uniqueSuffix, _tesisId, _kurumId, _ilId);
    }

    private CreateSatisBelgesiRequest YeniAlisRequest(List<CreateSatisBelgesiSatiriRequest> satirlar) => new()
    {
        BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
        BelgeTipi = SatisBelgesiTipi.AlisFaturasi,
        TesisId = _tesisId,
        CariKartId = _tedarikciKartId,
        BelgeTarihi = new DateTime(2026, 3, 1),
        KarsiTarafFaturaNo = $"TED-{Guid.NewGuid():N}"[..20],
        Satirlar = satirlar
    };

    // ─────────────────────────────────────────────────────────────
    // 1: Tanımsız SatirTipi create ve update'te reddedilir
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task TanimsizSatirTipi_CreateReddedilir()
    {
        var request = YeniAlisRequest(
        [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Tanimsiz satir tipi",
                SatirTipi = (SatisBelgesiSatirTipi)999,
                Miktar = 1, BirimFiyat = 500m, KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
            }
        ]);

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => satisService.CreateAsync(request, CancellationToken.None));
        Assert.Contains("Geçersiz satır tipi", ex.Message);
    }

    [IntegrationFact]
    public async Task TanimsizSatirTipi_UpdateReddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var taslak = await satisService.CreateAsync(YeniAlisRequest(
        [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Gecerli hizmet", SatirTipi = SatisBelgesiSatirTipi.EkHizmet,
                Miktar = 1, BirimFiyat = 500m, KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
            }
        ]));

        var updateRequest = new UpdateSatisBelgesiRequest
        {
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Tanimsiz satir tipi",
                    SatirTipi = (SatisBelgesiSatirTipi)999,
                    Miktar = 1, BirimFiyat = 500m, KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        };

        var ex = await Assert.ThrowsAsync<BaseException>(() => satisService.UpdateAsync(taslak.Id!.Value, updateRequest, CancellationToken.None));
        Assert.Contains("Geçersiz satır tipi", ex.Message);
    }

    // ─────────────────────────────────────────────────────────────
    // 2-4: Doğrudan DB'ye yazılmış (servis doğrulamasını atlatmış) tutarsız satırlar muhasebe
    // fişi oluşturma anında reddedilir; hiçbir fiş/cari/stok kaydı oluşmaz.
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task DbyeDogrudanEklenmisTasinirKartliHizmetSatiri_Muhasebelestirilemez()
    {
        var request = YeniAlisRequest(
        [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Basta gecerli hizmet", SatirTipi = SatisBelgesiSatirTipi.EkHizmet,
                Miktar = 1, BirimFiyat = 500m, KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
            }
        ]);

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, request);

        // Servis katmanını BAYPAS EDEREK, doğrudan SQL/EF ile satıra bir taşınır kart+depo
        // "yapıştırılır" - SatirTipi hâlâ EkHizmet (Urun DEĞİL). Bu, yazma-anı doğrulamasını
        // atlatmış, eski/tutarsız bir kayıt senaryosunu simüle eder.
        await dbContext.SatisBelgesiSatirlari
            .Where(x => x.SatisBelgesiId == onaylanmis.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.TasinirKartId, _tasinirKartId)
                .SetProperty(x => x.DepoId, _depoId));

        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);
        var ex = await Assert.ThrowsAsync<BaseException>(
            () => fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None));
        Assert.Contains("taşınır kart seçilemez", ex.Message);

        await SatisBelgesiMuhasebeTestSupport.AssertHicMuhasebeKaydiOlusmadiAsync(dbContext, onaylanmis.Id!.Value);
    }

    [IntegrationFact]
    public async Task DbyeDogrudanEksikBirakilmisUrunSatiri_Muhasebelestirilemez()
    {
        var request = YeniAlisRequest(
        [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Basta gecerli urun", SatirTipi = SatisBelgesiSatirTipi.Urun,
                TasinirKartId = _tasinirKartId, DepoId = _depoId,
                Miktar = 2, BirimFiyat = 100m, KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
            }
        ]);

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, request);

        // Servis katmanını BAYPAS EDEREK, doğrudan SQL/EF ile taşınır kart referansı SİLİNİR -
        // SatirTipi hâlâ Urun'dur ama artık taşınır kartı EKSİKTİR. Bu da yazma-anı doğrulamasını
        // atlatmış bir tutarsızlığı simüle eder.
        await dbContext.SatisBelgesiSatirlari
            .Where(x => x.SatisBelgesiId == onaylanmis.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.TasinirKartId, (int?)null));

        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);
        var ex = await Assert.ThrowsAsync<BaseException>(
            () => fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None));
        Assert.Contains("taşınır kart seçimi zorunludur", ex.Message);

        await SatisBelgesiMuhasebeTestSupport.AssertHicMuhasebeKaydiOlusmadiAsync(dbContext, onaylanmis.Id!.Value);
    }
}
