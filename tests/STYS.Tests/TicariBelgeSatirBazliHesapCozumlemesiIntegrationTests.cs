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
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Ticari belge hesap çözümlemesinin SATIR BAZLI hale getirilmesini (bkz. 79f75ce sonrası görev)
/// GERÇEK SQL Server'a karşı doğrulayan hedefli entegrasyon testleri:
///
/// 1. Alış/alış iade context'i artık stok ve hizmet hesaplarını KOŞULSUZ aramaz - yalnızca ürün
///    satırı varsa stok hesabı, yalnızca hizmet satırı varsa hizmet gider hesabı aranır; taşınır
///    kartın kendi doğrudan hesabı/eşlemesi varsa global 153 hesabı hiç ZORUNLU tutulmaz.
/// 2. KDV hesapları artık ORAN BAZINDA çözülür - aynı belgede farklı oranlı KDV içeren satırlar,
///    HER oran için ayrı (ve doğru) 391/191 hesabına, doğru tutarla yazılır.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class TicariBelgeSatirBazliHesapCozumlemesiIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "TBSATIR-931";

    private string _uniqueSuffix = TestMarker;
    private int _kurumId;
    private int _ilId;
    private int _tesisId;
    private int _gelirHesapId;
    private int _satisIadeHesapId;
    private int _giderHesapId;
    private int _stokHesapId;
    private int _kdvSatisHesap10Id;
    private int _kdvSatisHesap20Id;
    private int _kdvAlisHesap10Id;
    private int _kdvAlisHesap20Id;
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

        var gelirHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.GelirSatis, "GELIR", _tesisId);
        var satisIadeHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.SatisIade, "IADE", _tesisId);
        var giderHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.GiderHizmetMaliyet, "GIDER", _tesisId);
        var stokHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.StokTicariMal, "STOK", _tesisId);
        // Oran bazlı çözümlemeyi kanıtlamak için %10 ve %20 için AYRI (farklı) hesaplar - eğer
        // eski (oran'dan bağımsız, tek hesap) davranış hâlâ yürürlükte olsaydı, iki oranın da
        // AYNI hesaba düştüğü görülür ve aşağıdaki testler başarısız olurdu.
        var kdvSatisHesap10 = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "KDVS10", _tesisId);
        var kdvSatisHesap20 = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "KDVS20", _tesisId);
        var kdvAlisHesap10 = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "KDVA10", _tesisId);
        var kdvAlisHesap20 = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "KDVA20", _tesisId);
        var musteriHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "MUS", _tesisId);
        var tedarikciHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "TED", _tesisId);
        dbContext.MuhasebeHesapPlanlari.AddRange(
            gelirHesap, satisIadeHesap, giderHesap, stokHesap,
            kdvSatisHesap10, kdvSatisHesap20, kdvAlisHesap10, kdvAlisHesap20,
            musteriHesap, tedarikciHesap);
        await dbContext.SaveChangesAsync();
        _gelirHesapId = gelirHesap.Id;
        _satisIadeHesapId = satisIadeHesap.Id;
        _giderHesapId = giderHesap.Id;
        _stokHesapId = stokHesap.Id;
        _kdvSatisHesap10Id = kdvSatisHesap10.Id;
        _kdvSatisHesap20Id = kdvSatisHesap20.Id;
        _kdvAlisHesap10Id = kdvAlisHesap10.Id;
        _kdvAlisHesap20Id = kdvAlisHesap20.Id;
        _musteriHesapId = musteriHesap.Id;
        _tedarikciHesapId = tedarikciHesap.Id;

        // Tesis özel oran eşlemeleri - "aynı tesis ve oran eşlemesini global eşlemeden önce
        // kullan" gereksinimi için tesis özel (global DEĞİL) eşlemeler oluşturulur.
        var esleme10 = new MuhasebeVergiHesapEsleme
        {
            TesisId = _tesisId, VergiTipi = "KDV", Oran = 10m,
            AlisKdvHesapId = _kdvAlisHesap10Id, SatisKdvHesapId = _kdvSatisHesap10Id, AktifMi = true
        };
        var esleme20 = new MuhasebeVergiHesapEsleme
        {
            TesisId = _tesisId, VergiTipi = "KDV", Oran = 20m,
            AlisKdvHesapId = _kdvAlisHesap20Id, SatisKdvHesapId = _kdvSatisHesap20Id, AktifMi = true
        };
        dbContext.MuhasebeVergiHesapEslemeleri.AddRange(esleme10, esleme20);
        await dbContext.SaveChangesAsync();
        _olusturulanEslemeIdleri.Add(esleme10.Id);
        _olusturulanEslemeIdleri.Add(esleme20.Id);

        var musteri = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "MUS", CariKartTipleri.Musteri, _tesisId, musteriHesap.Id);
        var tedarikci = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "TED", CariKartTipleri.Tedarikci, _tesisId, tedarikciHesap.Id);
        tedarikci.VergiNoTckn = "4444444444";
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
            // TasinirKodlar.Kod kolonu en fazla 16 karakter - uniqueSuffix'i barindiramaz,
            // bu yuzden kisa bir rastgele deger kullanilir; TamKod (64) tanimlayici kalir.
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
        // Global (TesisId=null) kayıtları CreatedAt zaman aralığıyla silmek yerine, oluşturulan
        // eşlemelerin ID'leri SAKLANIR ve yalnızca O kayıtlar silinir - paylaşılan test
        // veritabanında eşzamanlı çalışan başka bir testin (varsa) global bir eşlemesini
        // yanlışlıkla silmemek için (bkz. görev 4).
        if (_olusturulanEslemeIdleri.Count > 0)
        {
            await dbContext.MuhasebeVergiHesapEslemeleri
                .Where(x => _olusturulanEslemeIdleri.Contains(x.Id))
                .ExecuteDeleteAsync();
        }
        // İKİ AŞAMALI temizlik: (1) SatisBelgesiSatirlari.TasinirKartId Restrict FK'si yüzünden
        // belgeler TASINIR KART'TAN önce silinmeli - bu yüzden CleanupAsync ÖNCE tesisId=null ile
        // çağrılır (yalnızca belgeleri/fişleri siler, Tesisler'e DOKUNMAZ). (2) TasinirKartlar.
        // MuhasebeHesapPlaniId de Restrict FK ile bağlı olduğundan önce null'lanır, sonra taşınır
        // kart/kod/depo silinir (Depolar->Tesisler FK'si için Tesis hâlâ mevcut olmalı). (3) Son
        // olarak CleanupAsync GERÇEK tesisId/kurumId/ilId ile tekrar çağrılarak hesap planı/cari/
        // tesis/il/kurum temizlenir.
        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, _uniqueSuffix);
        await dbContext.TasinirKartlar.Where(x => x.Id == _tasinirKartId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.MuhasebeHesapPlaniId, (int?)null));
        await dbContext.StokHareketleri.Where(x => x.TasinirKartId == _tasinirKartId).ExecuteDeleteAsync();
        await dbContext.TasinirKartlar.Where(x => x.Id == _tasinirKartId).ExecuteDeleteAsync();
        await dbContext.TasinirKodlar.Where(x => x.Id == _tasinirKodId).ExecuteDeleteAsync();
        await dbContext.Depolar.Where(x => x.Id == _depoId).ExecuteDeleteAsync();
        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, _uniqueSuffix, _tesisId, _kurumId, _ilId);
    }

    // ─────────────────────────────────────────────────────────────
    // Yardımcılar
    // ─────────────────────────────────────────────────────────────

    /// <summary>Urun satırı (%20 KDV, TasinirKartId+DepoId) + hizmet satırı (%10 KDV) - iki farklı oran.</summary>
    private List<CreateSatisBelgesiSatiriRequest> IkiOranliSatirlar() =>
    [
        new CreateSatisBelgesiSatiriRequest
        {
            SiraNo = 1, Aciklama = "Urun satiri %20", SatirTipi = SatisBelgesiSatirTipi.Urun,
            TasinirKartId = _tasinirKartId, DepoId = _depoId,
            Miktar = 2, BirimFiyat = 100m, KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
        },
        new CreateSatisBelgesiSatiriRequest
        {
            SiraNo = 2, Aciklama = "Hizmet satiri %10", SatirTipi = SatisBelgesiSatirTipi.EkHizmet,
            Miktar = 1, BirimFiyat = 500m, KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 10m
        }
    ];

    private CreateSatisBelgesiRequest YeniBelgeRequest(
        SatisBelgesiTipi belgeTipi, int cariKartId, List<CreateSatisBelgesiSatiriRequest> satirlar) => new()
    {
        BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
        BelgeTipi = belgeTipi,
        TesisId = _tesisId,
        CariKartId = cariKartId,
        BelgeTarihi = new DateTime(2026, 3, 1),
        Satirlar = satirlar
    };

    // ─────────────────────────────────────────────────────────────
    // 1. Ürün-only alış/alış iade — hizmet hesabı YOKKEN
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task HizmetHesabiOlmadanUrunOnlyAlisFaturasi_FisBasariylaOlusur()
    {
        var (dbContext, tesisId, kurumId, ilId, uniqueSuffix, tedarikciId) =
            await SeedIzoleTesisAsync("HIZYOK-ALIS", hizmetHesabiSeedEt: false, stokHesabiSeedEt: false, tasinirKartDogrudanHesapli: true);
        try
        {
            var request = new CreateSatisBelgesiRequest
            {
                BelgeNo = $"BLG-{uniqueSuffix}-{Guid.NewGuid():N}"[..40],
                BelgeTipi = SatisBelgesiTipi.AlisFaturasi,
                TesisId = tesisId,
                CariKartId = tedarikciId,
                BelgeTarihi = new DateTime(2026, 3, 1),
                KarsiTarafFaturaNo = $"TED-{Guid.NewGuid():N}"[..20],
                Satirlar =
                [
                    new CreateSatisBelgesiSatiriRequest
                    {
                        SiraNo = 1, Aciklama = "Urun-only", SatirTipi = SatisBelgesiSatirTipi.Urun,
                        TasinirKartId = _izoleTasinirKartId, DepoId = _izoleDepoId,
                        Miktar = 1, BirimFiyat = 500m, KdvUygulamaTipi = (int)KdvUygulamaTipi.KdvKapsamDisi, KdvOrani = 0m, KdvIstisnaTanimId = _izoleKdvKapsamDisiIstisnaId
                    }
                ]
            };

            var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
            var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, request);

            var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);
            var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);
            Assert.NotNull(dto.MuhasebeFisId);
        }
        finally
        {
            await CleanupIzoleTesisAsync(dbContext, uniqueSuffix, tesisId, kurumId, ilId);
        }
    }

    [IntegrationFact]
    public async Task HizmetHesabiOlmadanUrunOnlyAlisIadeFaturasi_FisBasariylaOlusur()
    {
        var (dbContext, tesisId, kurumId, ilId, uniqueSuffix, tedarikciId) =
            await SeedIzoleTesisAsync("HIZYOK-IADE", hizmetHesabiSeedEt: false, stokHesabiSeedEt: false, tasinirKartDogrudanHesapli: true);
        try
        {
            var asilRequest = new CreateSatisBelgesiRequest
            {
                BelgeNo = $"BLG-{uniqueSuffix}-ASIL-{Guid.NewGuid():N}"[..40],
                BelgeTipi = SatisBelgesiTipi.AlisFaturasi,
                TesisId = tesisId,
                CariKartId = tedarikciId,
                BelgeTarihi = new DateTime(2026, 3, 1),
                KarsiTarafFaturaNo = $"TED-{Guid.NewGuid():N}"[..20],
                Satirlar =
                [
                    new CreateSatisBelgesiSatiriRequest
                    {
                        SiraNo = 1, Aciklama = "Urun-only asil", SatirTipi = SatisBelgesiSatirTipi.Urun,
                        TasinirKartId = _izoleTasinirKartId, DepoId = _izoleDepoId,
                        Miktar = 1, BirimFiyat = 500m, KdvUygulamaTipi = (int)KdvUygulamaTipi.KdvKapsamDisi, KdvOrani = 0m, KdvIstisnaTanimId = _izoleKdvKapsamDisiIstisnaId
                    }
                ]
            };

            var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
            var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);
            var asilOnaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, asilRequest);
            await fisService.MuhasebeFisiOlusturAsync(asilOnaylanmis.Id!.Value, CancellationToken.None);

            var iadeRequest = new CreateSatisBelgesiRequest
            {
                BelgeNo = $"BLG-{uniqueSuffix}-IADE-{Guid.NewGuid():N}"[..40],
                BelgeTipi = SatisBelgesiTipi.AlisIadeFaturasi,
                TesisId = tesisId,
                CariKartId = tedarikciId,
                BelgeTarihi = new DateTime(2026, 3, 1),
                IadeEdilenBelgeId = asilOnaylanmis.Id,
                Satirlar =
                [
                    new CreateSatisBelgesiSatiriRequest
                    {
                        SiraNo = 1, Aciklama = "Urun-only iade", SatirTipi = SatisBelgesiSatirTipi.Urun,
                        TasinirKartId = _izoleTasinirKartId, DepoId = _izoleDepoId,
                        Miktar = 1, BirimFiyat = 500m, KdvUygulamaTipi = (int)KdvUygulamaTipi.KdvKapsamDisi, KdvOrani = 0m, KdvIstisnaTanimId = _izoleKdvKapsamDisiIstisnaId,
                        KaynakSatirId = asilOnaylanmis.Satirlar[0].Id!.Value.ToString()
                    }
                ]
            };

            var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, iadeRequest);
            var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);
            Assert.NotNull(dto.MuhasebeFisId);
        }
        finally
        {
            await CleanupIzoleTesisAsync(dbContext, uniqueSuffix, tesisId, kurumId, ilId);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 2. Global 153 hesabı yokken doğrudan hesaplı ürün satırı — başarılı
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task Global153HesabiOlmadanDogrudanHesapliUrunSatiri_FisBasariylaOlusur()
    {
        // Taşınır kartın MuhasebeHesapPlaniId'si DOĞRUDAN verilir - hesap planında AYRICA
        // global/tesis 153 (StokTicariMal) kodlu BAŞKA bir hesap HİÇ YOKTUR. Eski (koşulsuz
        // global 153 arayan) davranışta bu senaryo "153 hesabı bulunamadı" ile REDDEDİLİRDİ.
        var (dbContext, tesisId, kurumId, ilId, uniqueSuffix, tedarikciId) =
            await SeedIzoleTesisAsync("153YOK", hizmetHesabiSeedEt: true, stokHesabiSeedEt: false, tasinirKartDogrudanHesapli: true);
        try
        {
            var request = new CreateSatisBelgesiRequest
            {
                BelgeNo = $"BLG-{uniqueSuffix}-{Guid.NewGuid():N}"[..40],
                BelgeTipi = SatisBelgesiTipi.AlisFaturasi,
                TesisId = tesisId,
                CariKartId = tedarikciId,
                BelgeTarihi = new DateTime(2026, 3, 1),
                KarsiTarafFaturaNo = $"TED-{Guid.NewGuid():N}"[..20],
                Satirlar =
                [
                    new CreateSatisBelgesiSatiriRequest
                    {
                        SiraNo = 1, Aciklama = "Dogrudan hesapli urun", SatirTipi = SatisBelgesiSatirTipi.Urun,
                        TasinirKartId = _izoleTasinirKartId, DepoId = _izoleDepoId,
                        Miktar = 1, BirimFiyat = 500m, KdvUygulamaTipi = (int)KdvUygulamaTipi.KdvKapsamDisi, KdvOrani = 0m, KdvIstisnaTanimId = _izoleKdvKapsamDisiIstisnaId
                    }
                ]
            };

            var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
            var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, request);

            var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);
            var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);
            Assert.NotNull(dto.MuhasebeFisId);

            var fis = await dbContext.MuhasebeFisler.AsNoTracking().Include(x => x.Satirlar).FirstAsync(x => x.Id == dto.MuhasebeFisId);
            Assert.Contains(fis.Satirlar, s => s.MuhasebeHesapPlaniId == _izoleTasinirKartDogrudanHesapId);
        }
        finally
        {
            await CleanupIzoleTesisAsync(dbContext, uniqueSuffix, tesisId, kurumId, ilId);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 3. Stok hesabı olmadan hizmet-only alış — başarılı
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task StokHesabiOlmadanHizmetOnlyAlisFaturasi_FisBasariylaOlusur()
    {
        var (dbContext, tesisId, kurumId, ilId, uniqueSuffix, tedarikciId) =
            await SeedIzoleTesisAsync("STOKYOK", hizmetHesabiSeedEt: true, stokHesabiSeedEt: false, tasinirKartDogrudanHesapli: false);
        try
        {
            var request = new CreateSatisBelgesiRequest
            {
                BelgeNo = $"BLG-{uniqueSuffix}-{Guid.NewGuid():N}"[..40],
                BelgeTipi = SatisBelgesiTipi.AlisFaturasi,
                TesisId = tesisId,
                CariKartId = tedarikciId,
                BelgeTarihi = new DateTime(2026, 3, 1),
                KarsiTarafFaturaNo = $"TED-{Guid.NewGuid():N}"[..20],
                Satirlar =
                [
                    new CreateSatisBelgesiSatiriRequest
                    {
                        SiraNo = 1, Aciklama = "Hizmet-only", SatirTipi = SatisBelgesiSatirTipi.EkHizmet,
                        Miktar = 1, BirimFiyat = 500m, KdvUygulamaTipi = (int)KdvUygulamaTipi.KdvKapsamDisi, KdvOrani = 0m, KdvIstisnaTanimId = _izoleKdvKapsamDisiIstisnaId
                    }
                ]
            };

            var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
            var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, request);

            var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);
            var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);
            Assert.NotNull(dto.MuhasebeFisId);
        }
        finally
        {
            await CleanupIzoleTesisAsync(dbContext, uniqueSuffix, tesisId, kurumId, ilId);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 4. %10 ve %20 KDV içeren belgelerde dört belge tipi — oran bazlı hesap/tutar
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task SatisFaturasi_IkiFarkliKdvOrani_HerOranDogruHesabaVeTutaraYazilir()
    {
        var request = YeniBelgeRequest(SatisBelgesiTipi.SatisFaturasi, _musteriKartId, IkiOranliSatirlar());

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, request);
        Assert.Equal(90m, onaylanmis.ToplamKdv); // 40 (%20) + 50 (%10)

        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);
        var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);

        var fis = await dbContext.MuhasebeFisler.AsNoTracking().Include(x => x.Satirlar).FirstAsync(x => x.Id == dto.MuhasebeFisId);

        var kdv20Satir = Assert.Single(fis.Satirlar, s => s.MuhasebeHesapPlaniId == _kdvSatisHesap20Id);
        Assert.Equal(40m, kdv20Satir.Alacak);

        var kdv10Satir = Assert.Single(fis.Satirlar, s => s.MuhasebeHesapPlaniId == _kdvSatisHesap10Id);
        Assert.Equal(50m, kdv10Satir.Alacak);
        Assert.Equal(4, fis.Satirlar.Count); // cari + gelir + kdv%20 + kdv%10
    }

    [IntegrationFact]
    public async Task AlisFaturasi_IkiFarkliKdvOrani_HerOranDogruHesabaVeTutaraYazilir()
    {
        var request = YeniBelgeRequest(SatisBelgesiTipi.AlisFaturasi, _tedarikciKartId, IkiOranliSatirlar());
        request.KarsiTarafFaturaNo = $"TED-{Guid.NewGuid():N}"[..20];

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, request);

        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);
        var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);

        var fis = await dbContext.MuhasebeFisler.AsNoTracking().Include(x => x.Satirlar).FirstAsync(x => x.Id == dto.MuhasebeFisId);

        var kdv20Satir = Assert.Single(fis.Satirlar, s => s.MuhasebeHesapPlaniId == _kdvAlisHesap20Id);
        Assert.Equal(40m, kdv20Satir.Borc);

        var kdv10Satir = Assert.Single(fis.Satirlar, s => s.MuhasebeHesapPlaniId == _kdvAlisHesap10Id);
        Assert.Equal(50m, kdv10Satir.Borc);
        Assert.Equal(5, fis.Satirlar.Count); // stok + gider + kdv%20 + kdv%10 + cari
    }

    [IntegrationFact]
    public async Task SatisIadeFaturasi_IkiFarkliKdvOrani_HerOranDogruHesabaVeTutaraYazilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);

        var asilRequest = YeniBelgeRequest(SatisBelgesiTipi.SatisFaturasi, _musteriKartId, IkiOranliSatirlar());
        var asilOnaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, asilRequest);
        await fisService.MuhasebeFisiOlusturAsync(asilOnaylanmis.Id!.Value, CancellationToken.None);

        dbContext.KurumFaturaNumaraSayaclari.Add(new KurumFaturaNumaraSayaci
        {
            KurumId = _kurumId, MaliYil = 2026, SeriKodu = "TBS", SonNumara = 0, AktifMi = true
        });
        await dbContext.SaveChangesAsync();
        var asilFaturaKesildi = await satisService.FaturaKesAsync(asilOnaylanmis.Id!.Value, new FaturaKesRequest { SeriKodu = "TBS" });

        var iadeRequest = YeniBelgeRequest(SatisBelgesiTipi.SatisIadeFaturasi, _musteriKartId, IkiOranliSatirlar());
        iadeRequest.KarsiTarafFaturaNo = $"MUS-{Guid.NewGuid():N}"[..20];
        iadeRequest.IadeEdilenBelgeId = asilFaturaKesildi.Id;
        iadeRequest.Satirlar[0].KaynakSatirId = asilFaturaKesildi.Satirlar[0].Id!.Value.ToString();
        iadeRequest.Satirlar[1].KaynakSatirId = asilFaturaKesildi.Satirlar[1].Id!.Value.ToString();

        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, iadeRequest);
        var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);

        var fis = await dbContext.MuhasebeFisler.AsNoTracking().Include(x => x.Satirlar).FirstAsync(x => x.Id == dto.MuhasebeFisId);

        var kdv20Satir = Assert.Single(fis.Satirlar, s => s.MuhasebeHesapPlaniId == _kdvSatisHesap20Id);
        Assert.Equal(40m, kdv20Satir.Borc);

        var kdv10Satir = Assert.Single(fis.Satirlar, s => s.MuhasebeHesapPlaniId == _kdvSatisHesap10Id);
        Assert.Equal(50m, kdv10Satir.Borc);
        Assert.Equal(4, fis.Satirlar.Count); // iade + kdv%20 + kdv%10 + cari
    }

    [IntegrationFact]
    public async Task AlisIadeFaturasi_IkiFarkliKdvOrani_HerOranDogruHesabaVeTutaraYazilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);

        var asilRequest = YeniBelgeRequest(SatisBelgesiTipi.AlisFaturasi, _tedarikciKartId, IkiOranliSatirlar());
        asilRequest.KarsiTarafFaturaNo = $"TED-{Guid.NewGuid():N}"[..20];
        var asilOnaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, asilRequest);
        await fisService.MuhasebeFisiOlusturAsync(asilOnaylanmis.Id!.Value, CancellationToken.None);

        var iadeRequest = YeniBelgeRequest(SatisBelgesiTipi.AlisIadeFaturasi, _tedarikciKartId, IkiOranliSatirlar());
        iadeRequest.IadeEdilenBelgeId = asilOnaylanmis.Id;
        iadeRequest.Satirlar[0].KaynakSatirId = asilOnaylanmis.Satirlar[0].Id!.Value.ToString();
        iadeRequest.Satirlar[1].KaynakSatirId = asilOnaylanmis.Satirlar[1].Id!.Value.ToString();

        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, iadeRequest);
        var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);

        var fis = await dbContext.MuhasebeFisler.AsNoTracking().Include(x => x.Satirlar).FirstAsync(x => x.Id == dto.MuhasebeFisId);

        var kdv20Satir = Assert.Single(fis.Satirlar, s => s.MuhasebeHesapPlaniId == _kdvAlisHesap20Id);
        Assert.Equal(40m, kdv20Satir.Alacak);

        var kdv10Satir = Assert.Single(fis.Satirlar, s => s.MuhasebeHesapPlaniId == _kdvAlisHesap10Id);
        Assert.Equal(50m, kdv10Satir.Alacak);
        Assert.Equal(5, fis.Satirlar.Count); // stok + gider + cari + kdv%20 + kdv%10
    }

    // ─────────────────────────────────────────────────────────────
    // İzole tesis seed yardımcısı (item 1-3 için)
    // ─────────────────────────────────────────────────────────────

    private int _izoleDepoId;
    private int _izoleTasinirKartId;
    private int _izoleTasinirKodId;
    private int _izoleTasinirKartDogrudanHesapId;
    private int _izoleKdvKapsamDisiIstisnaId;

    private async Task CleanupIzoleTesisAsync(StysAppDbContext dbContext, string uniqueSuffix, int tesisId, int kurumId, int ilId)
    {
        // İKİ AŞAMALI temizlik - bkz. DisposeAsync'teki açıklama: (1) belgeler önce (tesisId=null)
        // silinir, (2) taşınır kart/kod/depo temizlenir (Tesis hâlâ var), (3) tesis/kurum/il vb.
        // GERÇEK id'lerle son kez temizlenir.
        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, uniqueSuffix);
        await dbContext.TasinirKartlar.Where(x => x.Id == _izoleTasinirKartId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.MuhasebeHesapPlaniId, (int?)null));
        await dbContext.StokHareketleri.Where(x => x.TasinirKartId == _izoleTasinirKartId).ExecuteDeleteAsync();
        await dbContext.TasinirKartlar.Where(x => x.Id == _izoleTasinirKartId).ExecuteDeleteAsync();
        await dbContext.TasinirKodlar.Where(x => x.Id == _izoleTasinirKodId).ExecuteDeleteAsync();
        await dbContext.Depolar.Where(x => x.Id == _izoleDepoId).ExecuteDeleteAsync();
        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, uniqueSuffix, tesisId, kurumId, ilId);
        await dbContext.DisposeAsync();
    }

    private async Task<(StysAppDbContext DbContext, int TesisId, int KurumId, int IlId, string UniqueSuffix, int TedarikciId)> SeedIzoleTesisAsync(
        string etiket, bool hizmetHesabiSeedEt, bool stokHesabiSeedEt, bool tasinirKartDogrudanHesapli)
    {
        // Kısa tutulur - BelgeNo (max 50) "BLG-{uniqueSuffix}-ASIL/IADE-{guid}" gibi önekler
        // ekleyerek kullanır; uzun bir uniqueSuffix, [..40]/[..50] kırpması sırasında rastgele
        // GUID kısmını tamamen SİLEREK aynı asıl/iade belge numarasının üretilmesine (ve "belge
        // numarası zaten kullanılıyor" hatasına) yol açabilir.
        var uniqueSuffix = $"{etiket}-{Guid.NewGuid():N}"[..20];
        var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (kurum, il, tesis) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(dbContext, uniqueSuffix);

        var hesaplar = new List<STYS.Muhasebe.MuhasebeHesapPlanlari.Entities.MuhasebeHesapPlani>();
        var tedarikciHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(uniqueSuffix, "TED", tesis.Id);
        hesaplar.Add(tedarikciHesap);

        int? dogrudanHesapId = null;
        if (tasinirKartDogrudanHesapli)
        {
            var dogrudanHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(uniqueSuffix, "DOGRUDAN", tesis.Id);
            hesaplar.Add(dogrudanHesap);
            dbContext.MuhasebeHesapPlanlari.AddRange(hesaplar);
            await dbContext.SaveChangesAsync();
            dogrudanHesapId = dogrudanHesap.Id;
        }
        else if (stokHesabiSeedEt)
        {
            var stokHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(uniqueSuffix, MuhasebeAnaHesapKodlari.StokTicariMal, "STOK", tesis.Id);
            hesaplar.Add(stokHesap);
            dbContext.MuhasebeHesapPlanlari.AddRange(hesaplar);
            await dbContext.SaveChangesAsync();
        }
        else
        {
            dbContext.MuhasebeHesapPlanlari.AddRange(hesaplar);
            await dbContext.SaveChangesAsync();
        }

        if (hizmetHesabiSeedEt)
        {
            var giderHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(uniqueSuffix, MuhasebeAnaHesapKodlari.GiderHizmetMaliyet, "GIDER", tesis.Id);
            dbContext.MuhasebeHesapPlanlari.Add(giderHesap);
            await dbContext.SaveChangesAsync();
        }

        var tedarikci = SatisBelgesiMuhasebeTestSupport.BuildCariKart(uniqueSuffix, "TED", CariKartTipleri.Tedarikci, tesis.Id, tedarikciHesap.Id);
        tedarikci.VergiNoTckn = $"5{Guid.NewGuid():N}"[..10];
        dbContext.CariKartlar.Add(tedarikci);
        await dbContext.SaveChangesAsync();

        var depo = new Depo { TesisId = tesis.Id, Kod = $"DP-{uniqueSuffix}", Ad = "Izole Depo " + uniqueSuffix, AktifMi = true };
        dbContext.Depolar.Add(depo);
        await dbContext.SaveChangesAsync();
        _izoleDepoId = depo.Id;

        var tasinirKod = new TasinirKod
        {
            // TasinirKodlar.Kod kolonu en fazla 16 karakter.
            TamKod = $"TK-{uniqueSuffix}", Kod = Guid.NewGuid().ToString("N")[..16], Ad = "Izole Kod " + uniqueSuffix, DuzeyNo = 1, AktifMi = true
        };
        dbContext.TasinirKodlar.Add(tasinirKod);
        await dbContext.SaveChangesAsync();
        _izoleTasinirKodId = tasinirKod.Id;

        var tasinirKart = new TasinirKart
        {
            TesisId = tesis.Id, TasinirKodId = tasinirKod.Id,
            MuhasebeHesapPlaniId = dogrudanHesapId,
            StokKodu = $"SK-{uniqueSuffix}", Ad = "Izole Urun " + uniqueSuffix, Birim = "Adet", AktifMi = true
        };
        dbContext.TasinirKartlar.Add(tasinirKart);
        await dbContext.SaveChangesAsync();
        _izoleTasinirKartId = tasinirKart.Id;
        _izoleTasinirKartDogrudanHesapId = dogrudanHesapId ?? 0;

        // KdvKapsamDisi satırlar KDV istisna tanımı ZORUNLU kılar (bkz. SatisBelgesiService.
        // ValidateSatirRequestAsync) - bu testler KDV'yi sıfır tutmak için bu tipi kullanır,
        // dolayısıyla alış işlemlerinde kullanılabilir aktif bir tanım seed edilir.
        var kdvIstisnaTanim = new STYS.Muhasebe.Kdv.Entities.KdvIstisnaTanim
        {
            Kod = $"KDVKAP-{uniqueSuffix}"[..Math.Min(50, $"KDVKAP-{uniqueSuffix}".Length)],
            Ad = "Izole KDV Kapsam Dışı " + uniqueSuffix,
            UygulamaTipi = KdvUygulamaTipi.KdvKapsamDisi,
            SatisIslemlerindeKullanilirMi = true,
            AlisIslemlerindeKullanilirMi = true,
            AktifMi = true
        };
        dbContext.KdvIstisnaTanimlari.Add(kdvIstisnaTanim);
        await dbContext.SaveChangesAsync();
        _izoleKdvKapsamDisiIstisnaId = kdvIstisnaTanim.Id;

        return (dbContext, tesis.Id, kurum.Id, il.Id, uniqueSuffix, tedarikci.Id);
    }
}
