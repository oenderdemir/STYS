using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Depolar.Entities;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.MuhasebeVergiHesapEslemeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Services;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.TasinirKartlari.Entities;
using STYS.Muhasebe.TasinirKodlari.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Ticari belgelerin (SatisFaturasi/AlisFaturasi/SatisIadeFaturasi/AlisIadeFaturasi) GERÇEK muhasebe
/// etkilerini - yalnızca satır sayısı/borç-alacak dengesi değil, hesap ID'leri ve tutarları, cari
/// hareket yönü/tutarı/kalan tutarı/kaynak bağlantısı ve stok hareketlerinin (miktar/tutar/depo/
/// taşınır kart/kaynak) doğruluğunu - GERÇEK SQL Server'a karşı, gerçek public servis akışlarıyla
/// uçtan uca doğrulayan hedefli entegrasyon testleri. Ayrıca bu turda düzeltilen 3 hatayı (ToplamKdv=0
/// iken KDV hesabı zorunlu tutulmaması, vergi-hesap eşlemesinin tesis kapsamına saygı göstermesi,
/// satış tarafı cari doğrulamasının transaction içindeki güncel kayıt üzerinden yapılması) ve
/// herhangi bir hesap/cari/stok doğrulaması başarısız olduğunda TAM rollback'i (fiş/cari/stok
/// oluşmaz, MuhasebeFisId yazılmaz) regresyon olarak kanıtlar.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class TicariBelgeMuhasebeEtkileriIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "TBMUH-905";

    private string _uniqueSuffix = TestMarker;
    private int _kurumId;
    private int _ilId;
    private int _tesisId;
    private int _tesisBId;
    private int _gelirHesapId;
    private int _kdvSatisHesapId;
    private int _kdvAlisHesapId;
    private int _giderHesapId;
    private int _satisIadeHesapId;
    private int _stokHesapId;
    private int _kdvSatisHesapTesisBId;
    private int _musteriKartId;
    private int _musteriHesapId;
    private int _tedarikciKartId;
    private int _tedarikciHesapId;
    private int _depoId;
    private int _tasinirKartId;

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
            KurumId = kurum.Id,
            IlId = il.Id,
            Ad = "Test Tesis B " + _uniqueSuffix,
            Telefon = "0000",
            Adres = "Test Adres B",
            AktifMi = true
        };
        dbContext.Tesisler.Add(tesisB);
        await dbContext.SaveChangesAsync();
        _tesisBId = tesisB.Id;

        var gelirHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.GelirSatis, "GELIR", _tesisId);
        var kdvSatisHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.KDVHesaplanan, "KDVS", _tesisId);
        var kdvAlisHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.KDVIndirilecek, "KDVA", _tesisId);
        var giderHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.GiderHizmetMaliyet, "GIDER", _tesisId);
        var satisIadeHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.SatisIade, "IADE", _tesisId);
        var stokHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.StokTicariMal, "STOK", _tesisId);
        // Tesis B'ye ÖZEL bir KDV hesabı - global bir vergi eşlemesi yanlışlıkla buna işaret
        // ederse, tesis A için oluşturulan fişte KULLANILMAMALIDIR (bkz. görev 4.2 testi).
        var kdvSatisHesapTesisB = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.KDVHesaplanan, "KDVS-B", _tesisBId);
        var musteriHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "MUS", _tesisId);
        var tedarikciHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "TED", _tesisId);
        dbContext.MuhasebeHesapPlanlari.AddRange(
            gelirHesap, kdvSatisHesap, kdvAlisHesap, giderHesap, satisIadeHesap, stokHesap, kdvSatisHesapTesisB, musteriHesap, tedarikciHesap);
        await dbContext.SaveChangesAsync();
        _gelirHesapId = gelirHesap.Id;
        _kdvSatisHesapId = kdvSatisHesap.Id;
        _kdvAlisHesapId = kdvAlisHesap.Id;
        _giderHesapId = giderHesap.Id;
        _satisIadeHesapId = satisIadeHesap.Id;
        _stokHesapId = stokHesap.Id;
        _kdvSatisHesapTesisBId = kdvSatisHesapTesisB.Id;
        _musteriHesapId = musteriHesap.Id;
        _tedarikciHesapId = tedarikciHesap.Id;

        var musteri = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "MUS", CariKartTipleri.Musteri, _tesisId, musteriHesap.Id);
        var tedarikci = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "TED", CariKartTipleri.Tedarikci, _tesisId, tedarikciHesap.Id);
        tedarikci.VergiNoTckn = "2222222222";
        dbContext.CariKartlar.AddRange(musteri, tedarikci);
        await dbContext.SaveChangesAsync();
        _musteriKartId = musteri.Id;
        _tedarikciKartId = tedarikci.Id;

        var depo = new Depo
        {
            TesisId = _tesisId,
            Kod = $"DP-{_uniqueSuffix}",
            Ad = "Test Depo " + _uniqueSuffix,
            AktifMi = true
        };
        dbContext.Depolar.Add(depo);
        await dbContext.SaveChangesAsync();
        _depoId = depo.Id;

        var tasinirKod = new TasinirKod
        {
            TamKod = $"TK-{_uniqueSuffix}",
            Kod = $"TK-{_uniqueSuffix}",
            Ad = "Test Taşınır Kod " + _uniqueSuffix,
            DuzeyNo = 1,
            AktifMi = true
        };
        dbContext.TasinirKodlar.Add(tasinirKod);
        await dbContext.SaveChangesAsync();

        var tasinirKart = new TasinirKart
        {
            TesisId = _tesisId,
            TasinirKodId = tasinirKod.Id,
            // Hesap planı bağlantısı DOĞRUDAN verilir - ResolveSatirHesabiAsync bu yüzden
            // ITasinirKodMuhasebeHesapEslemeService'e (testlerde NotImplementedException fırlatan
            // fake) hiç düşmez.
            MuhasebeHesapPlaniId = stokHesap.Id,
            StokKodu = $"SK-{_uniqueSuffix}",
            Ad = "Test Ürün " + _uniqueSuffix,
            Birim = "Adet",
            AktifMi = true
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
        await dbContext.MuhasebeVergiHesapEslemeleri
            .Where(x => x.TesisId == _tesisId || x.TesisId == null || x.TesisId == _tesisBId)
            .Where(x => x.CreatedAt >= DateTime.UtcNow.AddHours(-1))
            .ExecuteDeleteAsync();
        await dbContext.StokHareketleri.Where(x => x.TasinirKartId == _tasinirKartId).ExecuteDeleteAsync();
        await dbContext.TasinirKartlar.Where(x => x.Id == _tasinirKartId).ExecuteDeleteAsync();
        await dbContext.TasinirKodlar.Where(x => x.Kod != null && x.Kod.Contains(_uniqueSuffix)).ExecuteDeleteAsync();
        await dbContext.Depolar.Where(x => x.Id == _depoId).ExecuteDeleteAsync();
        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, _uniqueSuffix, _tesisId, _kurumId, _ilId);
        await dbContext.Tesisler.Where(x => x.Id == _tesisBId).ExecuteDeleteAsync();
    }

    // ─────────────────────────────────────────────────────────────
    // Yardımcılar
    // ─────────────────────────────────────────────────────────────

    private List<CreateSatisBelgesiSatiriRequest> UrunVeHizmetSatirlari(decimal urunBirimFiyat, decimal hizmetBirimFiyat, decimal kdvOrani) =>
    [
        new CreateSatisBelgesiSatiriRequest
        {
            SiraNo = 1, Aciklama = "Urun satiri", SatirTipi = SatisBelgesiSatirTipi.Urun,
            TasinirKartId = _tasinirKartId, DepoId = _depoId,
            Miktar = 2, BirimFiyat = urunBirimFiyat,
            KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = kdvOrani
        },
        new CreateSatisBelgesiSatiriRequest
        {
            SiraNo = 2, Aciklama = "Hizmet satiri", SatirTipi = SatisBelgesiSatirTipi.EkHizmet,
            Miktar = 1, BirimFiyat = hizmetBirimFiyat,
            KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = kdvOrani
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

    private static async Task<CariHareket> ReadTekAktifCariHareketAsync(StysAppDbContext dbContext, int belgeId)
    {
        var hareketler = await dbContext.CariHareketler.AsNoTracking()
            .Where(x => x.KaynakId == belgeId && x.KaynakModul == MuhasebeKaynakModulleri.SatisBelgesi && x.Durum == CariHareketDurumlari.Aktif)
            .ToListAsync();
        return Assert.Single(hareketler);
    }

    private static async Task<StokHareket> ReadTekStokHareketAsync(StysAppDbContext dbContext, int belgeId)
    {
        var hareketler = await dbContext.StokHareketleri.AsNoTracking()
            .Where(x => x.KaynakId == belgeId && x.KaynakModul == MuhasebeKaynakModulleri.SatisBelgesi && x.Durum == StokHareketDurumlari.Aktif)
            .ToListAsync();
        return Assert.Single(hareketler);
    }

    // ─────────────────────────────────────────────────────────────
    // 1-4: Dört belge tipi için hesap/tutar + cari hareket + stok etkisi
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task SatisFaturasi_FisHesaplariCariHareketVeStokEtkisiDogru()
    {
        // Urun: 2x100=200 matrah, %20 kdv=40 ; Hizmet: 1x500=500 matrah, %20 kdv=100
        // Toplam matrah=700, kdv=140, genel toplam=840
        var request = YeniBelgeRequest(SatisBelgesiTipi.SatisFaturasi, _musteriKartId, UrunVeHizmetSatirlari(100m, 500m, 20m));

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, request);
        Assert.Equal(700m, onaylanmis.ToplamMatrah);
        Assert.Equal(140m, onaylanmis.ToplamKdv);
        Assert.Equal(840m, onaylanmis.GenelToplam);

        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);
        var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);

        var fis = await dbContext.MuhasebeFisler.AsNoTracking().Include(x => x.Satirlar)
            .FirstAsync(x => x.Id == dto.MuhasebeFisId);

        var cariSatir = Assert.Single(fis.Satirlar, s => s.MuhasebeHesapPlaniId == _musteriHesapId);
        Assert.Equal(840m, cariSatir.Borc);
        Assert.Equal(0m, cariSatir.Alacak);
        Assert.Equal(_musteriKartId, cariSatir.CariKartId);

        var gelirSatir = Assert.Single(fis.Satirlar, s => s.MuhasebeHesapPlaniId == _gelirHesapId);
        Assert.Equal(0m, gelirSatir.Borc);
        Assert.Equal(700m, gelirSatir.Alacak);

        var kdvSatir = Assert.Single(fis.Satirlar, s => s.MuhasebeHesapPlaniId == _kdvSatisHesapId);
        Assert.Equal(0m, kdvSatir.Borc);
        Assert.Equal(140m, kdvSatir.Alacak);
        Assert.Equal(3, fis.Satirlar.Count);

        var cariHareket = await ReadTekAktifCariHareketAsync(dbContext, onaylanmis.Id.Value);
        Assert.Equal(840m, cariHareket.BorcTutari);
        Assert.Equal(0m, cariHareket.AlacakTutari);
        Assert.Equal(840m, cariHareket.KalanTutar);
        Assert.Equal(_musteriKartId, cariHareket.CariKartId);
        Assert.Equal(onaylanmis.Id, cariHareket.KaynakId);

        var stokHareket = await ReadTekStokHareketAsync(dbContext, onaylanmis.Id.Value);
        Assert.Equal(StokHareketTipleri.Cikis, stokHareket.HareketTipi);
        Assert.Equal(2m, stokHareket.Miktar);
        Assert.Equal(200m, stokHareket.Tutar);
        Assert.Equal(_depoId, stokHareket.DepoId);
        Assert.Equal(_tasinirKartId, stokHareket.TasinirKartId);
    }

    [IntegrationFact]
    public async Task AlisFaturasi_FisHesaplariCariHareketVeStokEtkisiDogru()
    {
        var request = YeniBelgeRequest(SatisBelgesiTipi.AlisFaturasi, _tedarikciKartId, UrunVeHizmetSatirlari(100m, 500m, 20m));
        request.KarsiTarafFaturaNo = $"TED-{Guid.NewGuid():N}"[..20];

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, request);

        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);
        var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);

        var fis = await dbContext.MuhasebeFisler.AsNoTracking().Include(x => x.Satirlar)
            .FirstAsync(x => x.Id == dto.MuhasebeFisId);

        var stokSatir = Assert.Single(fis.Satirlar, s => s.MuhasebeHesapPlaniId == _stokHesapId);
        Assert.Equal(200m, stokSatir.Borc);
        Assert.Equal(0m, stokSatir.Alacak);

        var giderSatir = Assert.Single(fis.Satirlar, s => s.MuhasebeHesapPlaniId == _giderHesapId);
        Assert.Equal(500m, giderSatir.Borc);
        Assert.Equal(0m, giderSatir.Alacak);

        var kdvSatir = Assert.Single(fis.Satirlar, s => s.MuhasebeHesapPlaniId == _kdvAlisHesapId);
        Assert.Equal(140m, kdvSatir.Borc);
        Assert.Equal(0m, kdvSatir.Alacak);

        var cariSatir = Assert.Single(fis.Satirlar, s => s.MuhasebeHesapPlaniId == _tedarikciHesapId);
        Assert.Equal(0m, cariSatir.Borc);
        Assert.Equal(840m, cariSatir.Alacak);
        Assert.Equal(_tedarikciKartId, cariSatir.CariKartId);
        Assert.Equal(4, fis.Satirlar.Count);

        var cariHareket = await ReadTekAktifCariHareketAsync(dbContext, onaylanmis.Id.Value);
        Assert.Equal(0m, cariHareket.BorcTutari);
        Assert.Equal(840m, cariHareket.AlacakTutari);
        Assert.Equal(840m, cariHareket.KalanTutar);
        Assert.Equal(_tedarikciKartId, cariHareket.CariKartId);

        var stokHareket = await ReadTekStokHareketAsync(dbContext, onaylanmis.Id.Value);
        Assert.Equal(StokHareketTipleri.Giris, stokHareket.HareketTipi);
        Assert.Equal(2m, stokHareket.Miktar);
        Assert.Equal(200m, stokHareket.Tutar);
        Assert.Equal(_depoId, stokHareket.DepoId);
        Assert.Equal(_tasinirKartId, stokHareket.TasinirKartId);
    }

    [IntegrationFact]
    public async Task SatisIadeFaturasi_FisHesaplariCariHareketVeStokEtkisiDogru()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);

        var asilRequest = YeniBelgeRequest(SatisBelgesiTipi.SatisFaturasi, _musteriKartId, UrunVeHizmetSatirlari(100m, 500m, 20m));
        var asilOnaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, asilRequest);
        await fisService.MuhasebeFisiOlusturAsync(asilOnaylanmis.Id!.Value, CancellationToken.None);

        dbContext.KurumFaturaNumaraSayaclari.Add(new KurumFaturaNumaraSayaci
        {
            KurumId = _kurumId, MaliYil = 2026, SeriKodu = "TBM", SonNumara = 0, AktifMi = true
        });
        await dbContext.SaveChangesAsync();
        var asilFaturaKesildi = await satisService.FaturaKesAsync(asilOnaylanmis.Id!.Value, new FaturaKesRequest { SeriKodu = "TBM" });

        var iadeRequest = YeniBelgeRequest(SatisBelgesiTipi.SatisIadeFaturasi, _musteriKartId, UrunVeHizmetSatirlari(100m, 500m, 20m));
        iadeRequest.KarsiTarafFaturaNo = $"MUS-{Guid.NewGuid():N}"[..20];
        iadeRequest.IadeEdilenBelgeId = asilFaturaKesildi.Id;
        iadeRequest.Satirlar[0].KaynakSatirId = asilFaturaKesildi.Satirlar[0].Id!.Value.ToString();
        iadeRequest.Satirlar[1].KaynakSatirId = asilFaturaKesildi.Satirlar[1].Id!.Value.ToString();

        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, iadeRequest);
        var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);

        var fis = await dbContext.MuhasebeFisler.AsNoTracking().Include(x => x.Satirlar)
            .FirstAsync(x => x.Id == dto.MuhasebeFisId);

        var iadeSatir = Assert.Single(fis.Satirlar, s => s.MuhasebeHesapPlaniId == _satisIadeHesapId);
        Assert.Equal(700m, iadeSatir.Borc);
        Assert.Equal(0m, iadeSatir.Alacak);

        var kdvSatir = Assert.Single(fis.Satirlar, s => s.MuhasebeHesapPlaniId == _kdvSatisHesapId);
        Assert.Equal(140m, kdvSatir.Borc);
        Assert.Equal(0m, kdvSatir.Alacak);

        var cariSatir = Assert.Single(fis.Satirlar, s => s.MuhasebeHesapPlaniId == _musteriHesapId);
        Assert.Equal(0m, cariSatir.Borc);
        Assert.Equal(840m, cariSatir.Alacak);
        Assert.Equal(_musteriKartId, cariSatir.CariKartId);

        var cariHareket = await ReadTekAktifCariHareketAsync(dbContext, onaylanmis.Id.Value);
        Assert.Equal(0m, cariHareket.BorcTutari);
        Assert.Equal(840m, cariHareket.AlacakTutari);
        Assert.Equal(840m, cariHareket.KalanTutar);

        var stokHareket = await ReadTekStokHareketAsync(dbContext, onaylanmis.Id.Value);
        Assert.Equal(StokHareketTipleri.Giris, stokHareket.HareketTipi);
        Assert.Equal(2m, stokHareket.Miktar);
        Assert.Equal(200m, stokHareket.Tutar);
        Assert.Equal(_depoId, stokHareket.DepoId);
        Assert.Equal(_tasinirKartId, stokHareket.TasinirKartId);
    }

    [IntegrationFact]
    public async Task AlisIadeFaturasi_FisHesaplariCariHareketVeStokEtkisiDogru()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);

        var asilRequest = YeniBelgeRequest(SatisBelgesiTipi.AlisFaturasi, _tedarikciKartId, UrunVeHizmetSatirlari(100m, 500m, 20m));
        asilRequest.KarsiTarafFaturaNo = $"TED-{Guid.NewGuid():N}"[..20];
        var asilOnaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, asilRequest);
        await fisService.MuhasebeFisiOlusturAsync(asilOnaylanmis.Id!.Value, CancellationToken.None);

        var iadeRequest = YeniBelgeRequest(SatisBelgesiTipi.AlisIadeFaturasi, _tedarikciKartId, UrunVeHizmetSatirlari(100m, 500m, 20m));
        iadeRequest.IadeEdilenBelgeId = asilOnaylanmis.Id;
        iadeRequest.Satirlar[0].KaynakSatirId = asilOnaylanmis.Satirlar[0].Id!.Value.ToString();
        iadeRequest.Satirlar[1].KaynakSatirId = asilOnaylanmis.Satirlar[1].Id!.Value.ToString();

        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, iadeRequest);
        var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);

        var fis = await dbContext.MuhasebeFisler.AsNoTracking().Include(x => x.Satirlar)
            .FirstAsync(x => x.Id == dto.MuhasebeFisId);

        var stokSatir = Assert.Single(fis.Satirlar, s => s.MuhasebeHesapPlaniId == _stokHesapId);
        Assert.Equal(0m, stokSatir.Borc);
        Assert.Equal(200m, stokSatir.Alacak);

        var giderSatir = Assert.Single(fis.Satirlar, s => s.MuhasebeHesapPlaniId == _giderHesapId);
        Assert.Equal(0m, giderSatir.Borc);
        Assert.Equal(500m, giderSatir.Alacak);

        var cariSatir = Assert.Single(fis.Satirlar, s => s.MuhasebeHesapPlaniId == _tedarikciHesapId);
        Assert.Equal(840m, cariSatir.Borc);
        Assert.Equal(0m, cariSatir.Alacak);
        Assert.Equal(_tedarikciKartId, cariSatir.CariKartId);

        var kdvSatir = Assert.Single(fis.Satirlar, s => s.MuhasebeHesapPlaniId == _kdvAlisHesapId);
        Assert.Equal(0m, kdvSatir.Borc);
        Assert.Equal(140m, kdvSatir.Alacak);

        var cariHareket = await ReadTekAktifCariHareketAsync(dbContext, onaylanmis.Id.Value);
        Assert.Equal(840m, cariHareket.BorcTutari);
        Assert.Equal(0m, cariHareket.AlacakTutari);
        Assert.Equal(840m, cariHareket.KalanTutar);

        var stokHareket = await ReadTekStokHareketAsync(dbContext, onaylanmis.Id.Value);
        Assert.Equal(StokHareketTipleri.Cikis, stokHareket.HareketTipi);
        Assert.Equal(2m, stokHareket.Miktar);
        Assert.Equal(200m, stokHareket.Tutar);
    }

    // ─────────────────────────────────────────────────────────────
    // 5: ToplamKdv=0 → KDV hesabı aranmaz/zorunlu tutulmaz, KDV satırı oluşmaz
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task ToplamKdvSifirOlanSatisFaturasi_KdvHesabiZorunluDegilVeFisKdvSatiriIcermez()
    {
        // Tam istisna -> KdvTutari=0, dolayısıyla ToplamKdv=0. Yalnızca gelir hesabına ihtiyaç
        // duyulur; KDV hesabı hiç aranmamalı ve fiş bu belge için başarıyla oluşmalıdır.
        var request = YeniBelgeRequest(SatisBelgesiTipi.SatisFaturasi, _musteriKartId,
        [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Tam istisna satis", SatirTipi = SatisBelgesiSatirTipi.EkHizmet,
                Miktar = 1, BirimFiyat = 500m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.TamIstisna, KdvOrani = 0m
            }
        ]);

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, request);
        Assert.Equal(0m, onaylanmis.ToplamKdv);

        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);
        var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);

        var fis = await dbContext.MuhasebeFisler.AsNoTracking().Include(x => x.Satirlar)
            .FirstAsync(x => x.Id == dto.MuhasebeFisId);

        Assert.Equal(2, fis.Satirlar.Count);
        Assert.DoesNotContain(fis.Satirlar, s => s.MuhasebeHesapPlaniId == _kdvSatisHesapId);
    }

    // ─────────────────────────────────────────────────────────────
    // 6: Vergi-hesap eşlemesi başka tesisin hesabına işaret ederse kullanılmaz
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task VergiHesapEslemesiBaskaTesisinHesabinaIsaretEderse_KullanilmazVeTesisinKendi391HesabinaDuser()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();

        // GLOBAL (TesisId=null) bir eşleme, SatisKdvHesapId olarak TESİS B'ye özel bir hesabı
        // gösteriyor - bu, tesis A için oluşturulan fişte KULLANILMAMALIDIR.
        dbContext.MuhasebeVergiHesapEslemeleri.Add(new MuhasebeVergiHesapEsleme
        {
            TesisId = null,
            VergiTipi = "KDV",
            Oran = 20m,
            AlisKdvHesapId = _kdvAlisHesapId,
            SatisKdvHesapId = _kdvSatisHesapTesisBId,
            AktifMi = true
        });
        await dbContext.SaveChangesAsync();

        var request = YeniBelgeRequest(SatisBelgesiTipi.SatisFaturasi, _musteriKartId,
        [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Kdv esleme testi", SatirTipi = SatisBelgesiSatirTipi.EkHizmet,
                Miktar = 1, BirimFiyat = 500m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
            }
        ]);

        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, request);

        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);
        var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);

        var fis = await dbContext.MuhasebeFisler.AsNoTracking().Include(x => x.Satirlar)
            .FirstAsync(x => x.Id == dto.MuhasebeFisId);

        // Yanlış (tesis B'ye ait) eşleme hesabı KULLANILMAMIŞ, tesisin KENDİ 391 hesabına
        // (fallback ile) düşülmüş olmalı.
        Assert.Contains(fis.Satirlar, s => s.MuhasebeHesapPlaniId == _kdvSatisHesapId);
        Assert.DoesNotContain(fis.Satirlar, s => s.MuhasebeHesapPlaniId == _kdvSatisHesapTesisBId);
    }

    // ─────────────────────────────────────────────────────────────
    // 7-8: Satış tarafı cari doğrulaması transaction içindeki GÜNCEL kayıt üzerinden yapılır +
    // herhangi bir doğrulama başarısız olursa TAM rollback
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task FisOlusturmaZamanindaMusteriCariPasiflesmisse_ReddedilirVeHicbirMuhasebeKaydiOlusmaz()
    {
        var request = YeniBelgeRequest(SatisBelgesiTipi.SatisFaturasi, _musteriKartId,
        [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Pasif cari testi", SatirTipi = SatisBelgesiSatirTipi.EkHizmet,
                Miktar = 1, BirimFiyat = 500m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
            }
        ]);

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, request);

        // Belge onaylandıktan SONRA, fiş oluşturmadan HEMEN ÖNCE, cari kart pasifleştirilir -
        // bu, "transaction içindeki güncel kayıt üzerinden doğrulama" gerekliliğinin (bkz. görev
        // 4.3) tam olarak hedeflediği senaryodur: doğrulama, belge onaylanırken DEĞİL, fiş
        // oluşturma anında GÜNCEL cari durumuna bakmalıdır.
        await dbContext.CariKartlar.Where(x => x.Id == _musteriKartId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.AktifMi, false));

        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);
        await Assert.ThrowsAsync<BaseException>(() => fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None));

        await SatisBelgesiMuhasebeTestSupport.AssertHicMuhasebeKaydiOlusmadiAsync(dbContext, onaylanmis.Id!.Value);

        // Geri al - sonraki testlerin/temizliğin etkilenmemesi için.
        await dbContext.CariKartlar.Where(x => x.Id == _musteriKartId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.AktifMi, true));
    }

    [IntegrationFact]
    public async Task FisOlusturmaZamanindaMusteriCariTedarikciyeDonusturulmusse_Reddedilir()
    {
        var request = YeniBelgeRequest(SatisBelgesiTipi.SatisFaturasi, _musteriKartId,
        [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Cari tipi degisimi testi", SatirTipi = SatisBelgesiSatirTipi.EkHizmet,
                Miktar = 1, BirimFiyat = 500m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
            }
        ]);

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, request);

        // Cari kartın tipi, belge onaylandıktan SONRA (ör. veri düzeltmesi/yanlış kullanım ile)
        // Tedarikçi'ye çevrilmiş olsun - satış tarafı artık bunu transaction içinde GÜNCEL kayıt
        // üzerinden reddetmelidir (eskiden bu kontrol satış tarafında hiç yapılmıyordu).
        await dbContext.CariKartlar.Where(x => x.Id == _musteriKartId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.CariTipi, CariKartTipleri.Tedarikci));

        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);
        await Assert.ThrowsAsync<BaseException>(() => fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None));

        await SatisBelgesiMuhasebeTestSupport.AssertHicMuhasebeKaydiOlusmadiAsync(dbContext, onaylanmis.Id!.Value);

        await dbContext.CariKartlar.Where(x => x.Id == _musteriKartId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.CariTipi, CariKartTipleri.Musteri));
    }

    [IntegrationFact]
    public async Task GiderHesabiBulunamazsa_TransactionTamamenGeriAlinirVeHicbirMuhasebeKaydiOlusmaz()
    {
        // Alış tarafında ne HizmetGiderHesapPlaniId (GiderHizmetMaliyet/GiderGenelYonetim) ne de
        // stok hesabı tanımlı OLMAYAN ayrı bir tesis - hizmet satırı hesap çözümlemesi başarısız
        // olmalı ve BU NOKTAYA KADAR (satır strateji üretimi sırasında) tracked hiçbir stok/cari/fiş
        // kaydı SaveChanges ile kalıcı hale gelmemiş olmalıdır (bkz. görev 5).
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();

        var (kurum, il, tesis) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(dbContext, $"{_uniqueSuffix}-IZOLE");
        var tedarikciHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap($"{_uniqueSuffix}-IZOLE", "TED", tesis.Id);
        dbContext.MuhasebeHesapPlanlari.Add(tedarikciHesap);
        await dbContext.SaveChangesAsync();
        var tedarikci = SatisBelgesiMuhasebeTestSupport.BuildCariKart($"{_uniqueSuffix}-IZOLE", "TED", CariKartTipleri.Tedarikci, tesis.Id, tedarikciHesap.Id);
        tedarikci.VergiNoTckn = "3333333333";
        dbContext.CariKartlar.Add(tedarikci);
        await dbContext.SaveChangesAsync();

        var request = new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-IZOLE-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.AlisFaturasi,
            TesisId = tesis.Id,
            CariKartId = tedarikci.Id,
            BelgeTarihi = new DateTime(2026, 3, 1),
            KarsiTarafFaturaNo = $"TED-{Guid.NewGuid():N}"[..20],
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Hizmet - hesap eslemesi yok", SatirTipi = SatisBelgesiSatirTipi.EkHizmet,
                    Miktar = 1, BirimFiyat = 500m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.KdvKapsamDisi, KdvOrani = 0m
                }
            ]
        };

        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, request);

        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);
        await Assert.ThrowsAsync<BaseException>(() => fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None));

        await SatisBelgesiMuhasebeTestSupport.AssertHicMuhasebeKaydiOlusmadiAsync(dbContext, onaylanmis.Id!.Value);

        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, $"{_uniqueSuffix}-IZOLE", tesis.Id, kurum.Id, il.Id);
    }
}
