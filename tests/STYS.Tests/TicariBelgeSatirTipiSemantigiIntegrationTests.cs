using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Depolar.Entities;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.MuhasebeVergiHesapEslemeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Services;
using STYS.Muhasebe.TasinirKartlari.Entities;
using STYS.Muhasebe.TasinirKodlari.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// SatirTipi'nin ticari belge satırlarında OTORİTER hale getirilmesini (bkz. 67e613f sonrası görev)
/// GERÇEK SQL Server'a karşı doğrulayan hedefli entegrasyon testleri:
///
/// 1. SatirTipi=Urun taşınır kart + depoyu ZORUNLU kılar; Urun DIŞINDAKİ (hizmet) satırlar bu
///    ikisinin gönderilmesini REDDEDER - hem create hem update'te, aynı merkezi doğrulamada.
/// 2. Hizmet satırı hiçbir koşulda stok hesabına yazılmaz/stok hareketi oluşturmaz.
/// 3. KDV oran çözümlemesinde tesis özel eşleme, AYNI oran için global eşlemeden ÖNCELİKLİDİR;
///    başka bir tesise ait KDV hesabı hiçbir fişe yazılmaz.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class TicariBelgeSatirTipiSemantigiIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "TBSATIRTIPI-958";

    private string _uniqueSuffix = TestMarker;
    private int _kurumId;
    private int _ilId;
    private int _tesisId;
    private int _tesisBId;
    private int _gelirHesapId;
    private int _giderHesapId;
    private int _stokHesapId;
    private int _kdvAlisHesapId;
    private int _kdvSatisHesapTesisOzelId;
    private int _kdvSatisHesapGlobalId;
    private int _kdvSatisHesap391Id;
    private int _kdvSatisHesapTesisBId;
    private int _musteriKartId;
    private int _musteriHesapId;
    private int _tedarikciKartId;
    private int _tedarikciHesapId;
    private int _depoId;
    private int _tasinirKartId;
    private int _tasinirKodId;
    private readonly List<int> _olusturulanEslemeIdleri = [];

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

        var tesisB = new Tesis
        {
            KurumId = kurum.Id, IlId = il.Id, Ad = "Test Tesis B " + _uniqueSuffix,
            Telefon = "0000", Adres = "Test Adres B", AktifMi = true
        };
        dbContext.Tesisler.Add(tesisB);
        await dbContext.SaveChangesAsync();
        _tesisBId = tesisB.Id;

        var gelirHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.GelirSatis, "GELIR", _tesisId);
        var giderHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.GiderHizmetMaliyet, "GIDER", _tesisId);
        var stokHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.StokTicariMal, "STOK", _tesisId);
        var kdvAlisHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.KDVIndirilecek, "KDVA", _tesisId);
        // Aynı %20 oranı için İKİ farklı aday hesap: biri tesis özel eşlemenin, biri global
        // eşlemenin işaret edeceği - tesis özel önceliğini kanıtlamak için.
        var kdvSatisHesapTesisOzel = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "KDVS-OZEL", _tesisId);
        var kdvSatisHesapGlobal = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "KDVS-GLOBAL", _tesisId);
        // Tesis A'nın KENDİ 391 (ana kod) hesabı - eşleme reddedildiğinde (yanlış tesise ait
        // hesaba işaret ettiğinde) düşülecek fallback için gereklidir.
        var kdvSatisHesap391 = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.KDVHesaplanan, "KDVS-391", _tesisId);
        // Tesis B'ye özel bir KDV hesabı - global eşleme buna işaret ederse tesis A'nın fişinde
        // KULLANILMAMALIDIR.
        var kdvSatisHesapTesisB = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.KDVHesaplanan, "KDVS-B", _tesisBId);
        var musteriHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "MUS", _tesisId);
        var tedarikciHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "TED", _tesisId);
        dbContext.MuhasebeHesapPlanlari.AddRange(
            gelirHesap, giderHesap, stokHesap, kdvAlisHesap, kdvSatisHesapTesisOzel, kdvSatisHesapGlobal, kdvSatisHesap391, kdvSatisHesapTesisB, musteriHesap, tedarikciHesap);
        await dbContext.SaveChangesAsync();
        _gelirHesapId = gelirHesap.Id;
        _giderHesapId = giderHesap.Id;
        _stokHesapId = stokHesap.Id;
        _kdvAlisHesapId = kdvAlisHesap.Id;
        _kdvSatisHesapTesisOzelId = kdvSatisHesapTesisOzel.Id;
        _kdvSatisHesapGlobalId = kdvSatisHesapGlobal.Id;
        _kdvSatisHesap391Id = kdvSatisHesap391.Id;
        _kdvSatisHesapTesisBId = kdvSatisHesapTesisB.Id;
        _musteriHesapId = musteriHesap.Id;
        _tedarikciHesapId = tedarikciHesap.Id;

        var musteri = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "MUS", CariKartTipleri.Musteri, _tesisId, musteriHesap.Id);
        var tedarikci = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "TED", CariKartTipleri.Tedarikci, _tesisId, tedarikciHesap.Id);
        tedarikci.VergiNoTckn = "6666666666";
        dbContext.CariKartlar.AddRange(musteri, tedarikci);
        await dbContext.SaveChangesAsync();
        _musteriKartId = musteri.Id;
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
        if (_olusturulanEslemeIdleri.Count > 0)
        {
            await dbContext.MuhasebeVergiHesapEslemeleri
                .Where(x => _olusturulanEslemeIdleri.Contains(x.Id))
                .ExecuteDeleteAsync();
        }
        // İKİ AŞAMALI temizlik: belgeler/satırlar (FK) önce, taşınır kart/kod/depo sonra, tesis/il/
        // kurum en son - bkz. TicariBelgeMuhasebeEtkileriIntegrationTests'teki aynı desen.
        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, _uniqueSuffix);
        await dbContext.TasinirKartlar.Where(x => x.Id == _tasinirKartId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.MuhasebeHesapPlaniId, (int?)null));
        await dbContext.StokHareketleri.Where(x => x.TasinirKartId == _tasinirKartId).ExecuteDeleteAsync();
        await dbContext.TasinirKartlar.Where(x => x.Id == _tasinirKartId).ExecuteDeleteAsync();
        await dbContext.TasinirKodlar.Where(x => x.Id == _tasinirKodId).ExecuteDeleteAsync();
        await dbContext.Depolar.Where(x => x.Id == _depoId).ExecuteDeleteAsync();
        await dbContext.MuhasebeHesapPlanlari.Where(x => x.Id == _kdvSatisHesapTesisBId).ExecuteDeleteAsync();
        await dbContext.Tesisler.Where(x => x.Id == _tesisBId).ExecuteDeleteAsync();
        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, _uniqueSuffix, _tesisId, _kurumId, _ilId);
    }

    private CreateSatisBelgesiRequest YeniSatisRequest(List<CreateSatisBelgesiSatiriRequest> satirlar) => new()
    {
        BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
        BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
        TesisId = _tesisId,
        CariKartId = _musteriKartId,
        BelgeTarihi = new DateTime(2026, 3, 1),
        Satirlar = satirlar
    };

    // ─────────────────────────────────────────────────────────────
    // 1: Hizmet satırında taşınır kart/depo reddedilir (create + update); ürün satırında
    //    taşınır kart eksikse erken reddedilir.
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task HizmetSatirindaTasinirKartVeDepoGonderilirse_CreateReddedilir()
    {
        var request = YeniSatisRequest(
        [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Hizmet ama tasinir kartli", SatirTipi = SatisBelgesiSatirTipi.EkHizmet,
                TasinirKartId = _tasinirKartId, DepoId = _depoId,
                Miktar = 1, BirimFiyat = 500m, KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
            }
        ]);

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => satisService.CreateAsync(request, CancellationToken.None));
        Assert.Contains("taşınır kart seçilemez", ex.Message);
    }

    [IntegrationFact]
    public async Task HizmetSatirindaTasinirKartVeDepoGonderilirse_UpdateReddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var taslak = await satisService.CreateAsync(YeniSatisRequest(
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
                    SiraNo = 1, Aciklama = "Hizmet ama tasinir kartli", SatirTipi = SatisBelgesiSatirTipi.EkHizmet,
                    TasinirKartId = _tasinirKartId, DepoId = _depoId,
                    Miktar = 1, BirimFiyat = 500m, KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        };

        var ex = await Assert.ThrowsAsync<BaseException>(() => satisService.UpdateAsync(taslak.Id!.Value, updateRequest, CancellationToken.None));
        Assert.Contains("taşınır kart seçilemez", ex.Message);
    }

    [IntegrationFact]
    public async Task UrunSatirindaTasinirKartEksikse_CreateErkenReddedilir()
    {
        var request = YeniSatisRequest(
        [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Urun ama tasinir kartsiz", SatirTipi = SatisBelgesiSatirTipi.Urun,
                DepoId = _depoId,
                Miktar = 1, BirimFiyat = 500m, KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
            }
        ]);

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => satisService.CreateAsync(request, CancellationToken.None));
        Assert.Contains("taşınır kart seçimi zorunludur", ex.Message);
    }

    // ─────────────────────────────────────────────────────────────
    // 2: Hizmet satırı stok hesabına yazılmaz ve stok hareketi oluşturmaz
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task HizmetSatiriStokHesabinaYazilmazVeStokHareketiOlusturmaz()
    {
        var request = new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.AlisFaturasi,
            TesisId = _tesisId,
            CariKartId = _tedarikciKartId,
            BelgeTarihi = new DateTime(2026, 3, 1),
            KarsiTarafFaturaNo = $"TED-{Guid.NewGuid():N}"[..20],
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Urun satiri", SatirTipi = SatisBelgesiSatirTipi.Urun,
                    TasinirKartId = _tasinirKartId, DepoId = _depoId,
                    Miktar = 2, BirimFiyat = 100m, KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                },
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 2, Aciklama = "Hizmet satiri", SatirTipi = SatisBelgesiSatirTipi.EkHizmet,
                    Miktar = 1, BirimFiyat = 500m, KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        };

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, request);

        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);
        var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);

        var fis = await dbContext.MuhasebeFisler.AsNoTracking().Include(x => x.Satirlar)
            .FirstAsync(x => x.Id == dto.MuhasebeFisId);

        // Stok hesabına yalnızca ürün satırının tutarı (200) yazılmış olmalı - hizmet satırının
        // 500'ü stok hesabına KARIŞMAMALI.
        var stokSatir = Assert.Single(fis.Satirlar, s => s.MuhasebeHesapPlaniId == _stokHesapId);
        Assert.Equal(200m, stokSatir.Borc);

        var giderSatir = Assert.Single(fis.Satirlar, s => s.MuhasebeHesapPlaniId == _giderHesapId);
        Assert.Equal(500m, giderSatir.Borc);

        // Tam olarak BİR stok hareketi (yalnızca ürün satırı için) oluşmuş olmalı.
        var stokHareketleri = await dbContext.StokHareketleri.AsNoTracking()
            .Where(x => x.KaynakId == onaylanmis.Id && x.KaynakModul == MuhasebeKaynakModulleri.SatisBelgesi)
            .ToListAsync();
        var stokHareketi = Assert.Single(stokHareketleri);
        Assert.Equal(2m, stokHareketi.Miktar);
        Assert.Equal(200m, stokHareketi.Tutar);
    }

    // ─────────────────────────────────────────────────────────────
    // 3: Aynı oranda tesis özel eşleme, global eşlemeden önceliklidir; başka tesise ait KDV
    //    hesabı hiçbir fişe yazılmaz.
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task AyniOrandaTesisOzelVeGlobalEslemeBirlikteVarken_TesisOzelHesapSecilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();

        var tesisOzelEsleme = new MuhasebeVergiHesapEsleme
        {
            TesisId = _tesisId, VergiTipi = "KDV", Oran = 20m,
            AlisKdvHesapId = _kdvSatisHesapTesisOzelId, SatisKdvHesapId = _kdvSatisHesapTesisOzelId, AktifMi = true
        };
        var globalEsleme = new MuhasebeVergiHesapEsleme
        {
            TesisId = null, VergiTipi = "KDV", Oran = 20m,
            AlisKdvHesapId = _kdvSatisHesapGlobalId, SatisKdvHesapId = _kdvSatisHesapGlobalId, AktifMi = true
        };
        dbContext.MuhasebeVergiHesapEslemeleri.AddRange(tesisOzelEsleme, globalEsleme);
        await dbContext.SaveChangesAsync();
        _olusturulanEslemeIdleri.Add(tesisOzelEsleme.Id);
        _olusturulanEslemeIdleri.Add(globalEsleme.Id);

        var request = YeniSatisRequest(
        [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Kdv oncelik testi", SatirTipi = SatisBelgesiSatirTipi.EkHizmet,
                Miktar = 1, BirimFiyat = 500m, KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
            }
        ]);

        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, request);

        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);
        var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);

        var fis = await dbContext.MuhasebeFisler.AsNoTracking().Include(x => x.Satirlar)
            .FirstAsync(x => x.Id == dto.MuhasebeFisId);

        Assert.Contains(fis.Satirlar, s => s.MuhasebeHesapPlaniId == _kdvSatisHesapTesisOzelId);
        Assert.DoesNotContain(fis.Satirlar, s => s.MuhasebeHesapPlaniId == _kdvSatisHesapGlobalId);
    }

    [IntegrationFact]
    public async Task BaskaTesiseAitKdvHesabi_GlobalEslemeUzerindenBileHicbirFiseYazilmaz()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();

        // GLOBAL bir eşleme, SatisKdvHesapId olarak TESİS B'ye özel bir hesabı gösteriyor - bu,
        // tesis A için oluşturulan fişte KULLANILMAMALIDIR (tesis-scoping doğrulaması).
        var esleme = new MuhasebeVergiHesapEsleme
        {
            TesisId = null, VergiTipi = "KDV", Oran = 20m,
            AlisKdvHesapId = _kdvSatisHesapTesisBId, SatisKdvHesapId = _kdvSatisHesapTesisBId, AktifMi = true
        };
        dbContext.MuhasebeVergiHesapEslemeleri.Add(esleme);
        await dbContext.SaveChangesAsync();
        _olusturulanEslemeIdleri.Add(esleme.Id);

        var request = YeniSatisRequest(
        [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Baska tesis kdv testi", SatirTipi = SatisBelgesiSatirTipi.EkHizmet,
                Miktar = 1, BirimFiyat = 500m, KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
            }
        ]);

        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, request);

        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);
        var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);

        var fis = await dbContext.MuhasebeFisler.AsNoTracking().Include(x => x.Satirlar)
            .FirstAsync(x => x.Id == dto.MuhasebeFisId);

        Assert.DoesNotContain(fis.Satirlar, s => s.MuhasebeHesapPlaniId == _kdvSatisHesapTesisBId);
        // Reddedilen eşlemenin yerine, tesisin kendi 391 hesabına (fallback ile) düşülmüş olmalı.
        Assert.Contains(fis.Satirlar, s => s.MuhasebeHesapPlaniId == _kdvSatisHesap391Id);
    }
}
