using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Depolar.Entities;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Services;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.TasinirKartlari.Entities;
using STYS.Muhasebe.TasinirKodlari.Entities;
using STYS.TicariBelgeler.Services;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// aeeb913 sonrası görev: "ticari belge iptal ve ters kayıt sınırlarını güvenli hale getir" - dört
/// desteklenen belge tipi (SatisFaturasi/AlisFaturasi/SatisIadeFaturasi/AlisIadeFaturasi) için
/// oluştur → onayla → fiş oluştur → muhasebe ekranından iptal akışını GERÇEK SQL Server'a karşı
/// uçtan uca doğrular (orijinal fiş Iptal, tek bir TersKayit fişi + çift yönlü bağlantı, satır
/// bazlı borç/alacak ters çevrimi, üç otoriter durumun IptalEdildi olması, cari/stok
/// hareketlerinin Iptal olması, ilgili hesapların net bakiyesinin sıfırlanması, ikinci iptalin
/// yeni ters kayıt üretmeden reddedilmesi). Ayrıca genel fiş iptal endpoint'inin SatisBelgesi
/// kaynaklı fişleri, operasyonel /ui/ticari-belgeler iptalinin muhasebeleştirilmiş belgeleri, ve
/// kapatılmış cari hareketin belge iptalini TAM ROLLBACK ile engellediğini kanıtlar.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class TicariBelgeIptalVeTersKayitIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "TBIPTAL-931";

    private string _uniqueSuffix = TestMarker;
    private int _kurumId;
    private int _ilId;
    private int _tesisId;
    private int _gelirHesapId;
    private int _kdvSatisHesapId;
    private int _kdvAlisHesapId;
    private int _giderHesapId;
    private int _satisIadeHesapId;
    private int _stokHesapId;
    private int _musteriKartId;
    private int _musteriHesapId;
    private int _tedarikciKartId;
    private int _tedarikciHesapId;
    private int _depoId;
    private int _tasinirKartId;
    private int _tasinirKodId;
    private readonly List<int> _olusturulanAnaHesapIdleri = [];

    /// <summary>
    /// MuhasebeHesapBakiyeGuncellemeService.FisBakiyeleriniIsleAsync, bir fiş ONAYLANDIĞINDA
    /// (yalnızca oluşturulduğunda DEĞİL) hareket gören her hesabın TÜM üst kod zincirinin
    /// (GetUstHesapKodlari) gerçekten var olmasını ZORUNLU kılar ("Üst muhasebe hesabı
    /// bulunamadı: ..."). Bu görev, bu iptal/ters kayıt testlerinde İLK KEZ fişin ONAYLANMASINI
    /// (Taslak → Onaylı) gerektiriyor - önceki turların testleri yalnızca Taslak fiş oluşturmayı
    /// doğruluyordu, üst hesap zincirinin tamamı hiç tetiklenmemişti. Paylaşılan test DB'sindeki
    /// ana hesap planında "1.53" (StokTicariMal) ve "7.40" (GiderHizmetMaliyet) ara-seviye kodları
    /// eksik olduğundan, bunlar burada (yalnızca eksikse) GLOBAL ana hesap olarak seed edilir ve
    /// yalnızca BU test bunları oluşturduysa DisposeAsync'te geri silinir.
    /// </summary>
    private async Task EnsureAnaHesapVarAsync(StysAppDbContext dbContext, string tamKod, string ad)
    {
        var mevcut = await dbContext.MuhasebeHesapPlanlari.FirstOrDefaultAsync(x => x.TamKod == tamKod && !x.IsDeleted);
        if (mevcut is not null)
        {
            return;
        }

        var hesap = new MuhasebeHesapPlani
        {
            Kod = tamKod, TamKod = tamKod, Ad = ad, HesapTipi = HesapTipi.AnaHesap,
            AktifMi = true, DetayHesapMi = false, HareketGorebilirMi = false, TesisId = null
        };
        dbContext.MuhasebeHesapPlanlari.Add(hesap);
        await dbContext.SaveChangesAsync();
        _olusturulanAnaHesapIdleri.Add(hesap.Id);
    }

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

        await EnsureAnaHesapVarAsync(dbContext, "1.53", "TAŞINIR MALLAR VE HİZMETLER");
        await EnsureAnaHesapVarAsync(dbContext, "1.53.153", "TİCARİ MALLAR");
        await EnsureAnaHesapVarAsync(dbContext, "7.40", "HİZMET ÜRETİM MALİYETİ");
        await EnsureAnaHesapVarAsync(dbContext, "7.40.740", "HİZMET ÜRETİM MALİYETİ");

        var gelirHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.GelirSatis, "GELIR", _tesisId);
        var kdvSatisHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.KDVHesaplanan, "KDVS", _tesisId);
        var kdvAlisHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.KDVIndirilecek, "KDVA", _tesisId);
        var giderHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.GiderHizmetMaliyet, "GIDER", _tesisId);
        var satisIadeHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.SatisIade, "IADE", _tesisId);
        var stokHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.StokTicariMal, "STOK", _tesisId);
        var musteriHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "MUS", _tesisId);
        var tedarikciHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "TED", _tesisId);
        dbContext.MuhasebeHesapPlanlari.AddRange(gelirHesap, kdvSatisHesap, kdvAlisHesap, giderHesap, satisIadeHesap, stokHesap, musteriHesap, tedarikciHesap);
        await dbContext.SaveChangesAsync();
        _gelirHesapId = gelirHesap.Id;
        _kdvSatisHesapId = kdvSatisHesap.Id;
        _kdvAlisHesapId = kdvAlisHesap.Id;
        _giderHesapId = giderHesap.Id;
        _satisIadeHesapId = satisIadeHesap.Id;
        _stokHesapId = stokHesap.Id;
        _musteriHesapId = musteriHesap.Id;
        _tedarikciHesapId = tedarikciHesap.Id;

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
        await dbContext.MuhasebeHesapBakiyeleri.Where(x => x.TesisId == _tesisId).ExecuteDeleteAsync();
        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, _uniqueSuffix);
        await dbContext.TasinirKartlar.Where(x => x.Id == _tasinirKartId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.MuhasebeHesapPlaniId, (int?)null));
        await dbContext.StokHareketleri.Where(x => x.TasinirKartId == _tasinirKartId).ExecuteDeleteAsync();
        await dbContext.TasinirKartlar.Where(x => x.Id == _tasinirKartId).ExecuteDeleteAsync();
        await dbContext.TasinirKodlar.Where(x => x.Id == _tasinirKodId).ExecuteDeleteAsync();
        await dbContext.Depolar.Where(x => x.Id == _depoId).ExecuteDeleteAsync();
        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, _uniqueSuffix, _tesisId, _kurumId, _ilId);

        // Yalnızca BU test örneğinin EnsureAnaHesapVarAsync ile yeni oluşturduğu global ana
        // hesaplar silinir - önceden var olan (başka bir çalıştırmadan kalan) kayıtlara dokunulmaz.
        if (_olusturulanAnaHesapIdleri.Count > 0)
        {
            await dbContext.MuhasebeHesapPlanlari.Where(x => _olusturulanAnaHesapIdleri.Contains(x.Id)).ExecuteDeleteAsync();
        }
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

    /// <summary>
    /// Bir belgenin bağlı fişini muhasebe ekranından (SatisBelgesiService.IptalEtAsync üzerinden,
    /// gerçek SatisBelgesiFisiIptalEtAsync ters kayıt zincirini çalıştırarak) iptal eder ve görev
    /// 4'ün TÜM detaylı beklentilerini doğrular; ardından ikinci iptalin reddedildiğini ve yeni bir
    /// ters kayıt fişi ÜRETİLMEDİĞİNİ de kanıtlar.
    /// </summary>
    private static async Task AssertIptalTersKayitTamDogruAsync(
        ISatisBelgesiService satisService, StysAppDbContext dbContext, int belgeId, int fisId)
    {
        var oncekiFisSatirlari = await dbContext.MuhasebeFisSatirlari.AsNoTracking()
            .Where(x => x.MuhasebeFisId == fisId && !x.IsDeleted).ToListAsync();
        Assert.NotEmpty(oncekiFisSatirlari);
        var ilgiliHesapIdler = oncekiFisSatirlari.Select(x => x.MuhasebeHesapPlaniId).Distinct().ToList();

        // Ters kaydın, hesap bakiyesine yalnızca BU fişin (iade senaryolarında paylaşılan bir
        // hesabı - ör. aynı müşteri/KDV hesabını - asıl fişle BİRLİKTE etkilemiş olabilir) NET
        // KATKISINI sıfırladığı doğrulanır - hesabın TOPLAM bakiyesinin mutlak sıfıra dönmesi
        // DEĞİL (paylaşılan hesaplarda bu yanlış olurdu), iptal ÖNCESİ bakiyeden bu fişin kendi
        // Borç-Alacak net katkısının çıkarılmasıyla elde edilen değere dönmesi beklenir.
        var fisTesisId = oncekiFisSatirlari.Count > 0
            ? (await dbContext.MuhasebeFisler.AsNoTracking().Where(x => x.Id == fisId).Select(x => new { x.TesisId, x.MaliYil, x.Donem }).FirstAsync())
            : null;
        var bakiyelerOnce = await dbContext.MuhasebeHesapBakiyeleri.AsNoTracking()
            .Where(x => x.TesisId == fisTesisId!.TesisId && x.MaliYil == fisTesisId.MaliYil && x.Donem == fisTesisId.Donem
                        && ilgiliHesapIdler.Contains(x.MuhasebeHesapPlaniId))
            .ToDictionaryAsync(x => x.MuhasebeHesapPlaniId, x => x.NetBakiye);
        foreach (var hesapId in ilgiliHesapIdler)
        {
            bakiyelerOnce.TryAdd(hesapId, 0m);
        }
        var beklenenNetKatki = oncekiFisSatirlari
            .GroupBy(x => x.MuhasebeHesapPlaniId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Borc - x.Alacak));

        await satisService.IptalEtAsync(belgeId, CancellationToken.None);

        // (a) Orijinal fiş Iptal olur.
        var orijinalFis = await dbContext.MuhasebeFisler.AsNoTracking().FirstAsync(x => x.Id == fisId);
        Assert.Equal(MuhasebeFisDurumlari.Iptal, orijinalFis.Durum);
        Assert.True(orijinalFis.TersKayitFisId.HasValue);

        // (b) Tek bir TersKayit fişi oluşur, çift yönlü bağlantı doğrudur.
        var tersFisler = await dbContext.MuhasebeFisler.AsNoTracking()
            .Where(x => x.IptalEdilenFisId == fisId).ToListAsync();
        var tersFis = Assert.Single(tersFisler);
        Assert.Equal(orijinalFis.TersKayitFisId!.Value, tersFis.Id);
        Assert.Equal(MuhasebeFisDurumlari.TersKayit, tersFis.Durum);
        Assert.Equal(fisId, tersFis.IptalEdilenFisId);

        // (c) Ters fiş satırlarında hesaplar korunur, borç/alacak tam ters çevrilir.
        var tersSatirlar = await dbContext.MuhasebeFisSatirlari.AsNoTracking()
            .Where(x => x.MuhasebeFisId == tersFis.Id && !x.IsDeleted).ToListAsync();
        Assert.Equal(oncekiFisSatirlari.Count, tersSatirlar.Count);
        foreach (var orjSatir in oncekiFisSatirlari)
        {
            var tersSatir = tersSatirlar.Single(x => x.SiraNo == orjSatir.SiraNo);
            Assert.Equal(orjSatir.MuhasebeHesapPlaniId, tersSatir.MuhasebeHesapPlaniId);
            Assert.Equal(orjSatir.Alacak, tersSatir.Borc);
            Assert.Equal(orjSatir.Borc, tersSatir.Alacak);
        }

        // (d) Belgenin üç otoriter durumu IptalEdildi olur.
        var belgeDb = await dbContext.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.Id == belgeId);
        Assert.Equal(TicariBelgeDurumu.IptalEdildi, belgeDb.TicariDurum);
        Assert.Equal(TicariBelgeMuhasebeDurumu.IptalEdildi, belgeDb.MuhasebeDurumu);
        Assert.Equal(TicariBelgeFaturalamaDurumu.IptalEdildi, belgeDb.FaturalamaDurumu);

        // (e) Cari hareketler Iptal olur.
        var cariHareketler = await dbContext.CariHareketler.AsNoTracking()
            .Where(x => x.KaynakId == belgeId && x.KaynakModul == MuhasebeKaynakModulleri.SatisBelgesi).ToListAsync();
        Assert.NotEmpty(cariHareketler);
        Assert.All(cariHareketler, x => Assert.Equal(CariHareketDurumlari.Iptal, x.Durum));

        // (f) Stok hareketleri Iptal olur.
        var stokHareketler = await dbContext.StokHareketleri.AsNoTracking()
            .Where(x => x.KaynakId == belgeId && x.KaynakModul == MuhasebeKaynakModulleri.SatisBelgesi).ToListAsync();
        Assert.NotEmpty(stokHareketler);
        Assert.All(stokHareketler, x => Assert.Equal(StokHareketDurumlari.Iptal, x.Durum));

        // (g) Muhasebe hesap bakiyesinin net etkisi sıfırlanır: ters kayıt, iptal ÖNCESİ bakiyeden
        // TAM OLARAK bu fişin kendi Borç-Alacak net katkısını düşer - paylaşılan hesaplarda (ör.
        // iade fişinin asıl fişle aynı müşteri/KDV hesabını kullanması) mutlak sıfır BEKLENMEZ.
        var bakiyelerSonra = await dbContext.MuhasebeHesapBakiyeleri.AsNoTracking()
            .Where(x => x.TesisId == orijinalFis.TesisId && x.MaliYil == orijinalFis.MaliYil && x.Donem == orijinalFis.Donem
                        && ilgiliHesapIdler.Contains(x.MuhasebeHesapPlaniId))
            .ToDictionaryAsync(x => x.MuhasebeHesapPlaniId, x => x.NetBakiye);
        foreach (var hesapId in ilgiliHesapIdler)
        {
            var beklenenSonraki = bakiyelerOnce[hesapId] - beklenenNetKatki[hesapId];
            var gercekSonraki = bakiyelerSonra.TryGetValue(hesapId, out var v) ? v : 0m;
            Assert.Equal(beklenenSonraki, gercekSonraki);
        }

        // (h) İkinci iptal yeni ters kayıt fişi oluşturmadan reddedilir.
        var ex = await Assert.ThrowsAsync<BaseException>(() => satisService.IptalEtAsync(belgeId, CancellationToken.None));
        Assert.Contains("zaten iptal edilmiş", ex.Message);
        var tersFislerSonra = await dbContext.MuhasebeFisler.AsNoTracking()
            .Where(x => x.IptalEdilenFisId == fisId).ToListAsync();
        Assert.Single(tersFislerSonra);
    }

    // ─────────────────────────────────────────────────────────────
    // 1-4: Dört belge tipi için tam iptal/ters kayıt akışı
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task SatisFaturasi_MuhasebeEkranindanIptal_TersKayitVeBakiyeSifirlanmasiDogru()
    {
        var request = YeniBelgeRequest(SatisBelgesiTipi.SatisFaturasi, _musteriKartId, UrunVeHizmetSatirlari(100m, 500m, 20m));

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (satisService, muhasebeFisService) = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiServiceWithMuhasebeFisIptal(dbContext);
        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, request);

        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);
        var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);
        await muhasebeFisService.OnaylaAsync(dto.MuhasebeFisId!.Value, CancellationToken.None);

        await AssertIptalTersKayitTamDogruAsync(satisService, dbContext, onaylanmis.Id!.Value, dto.MuhasebeFisId!.Value);
    }

    [IntegrationFact]
    public async Task AlisFaturasi_MuhasebeEkranindanIptal_TersKayitVeBakiyeSifirlanmasiDogru()
    {
        var request = YeniBelgeRequest(SatisBelgesiTipi.AlisFaturasi, _tedarikciKartId, UrunVeHizmetSatirlari(100m, 500m, 20m));
        request.KarsiTarafFaturaNo = $"TED-{Guid.NewGuid():N}"[..20];

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (satisService, muhasebeFisService) = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiServiceWithMuhasebeFisIptal(dbContext);
        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, request);

        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);
        var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);
        await muhasebeFisService.OnaylaAsync(dto.MuhasebeFisId!.Value, CancellationToken.None);

        await AssertIptalTersKayitTamDogruAsync(satisService, dbContext, onaylanmis.Id!.Value, dto.MuhasebeFisId!.Value);
    }

    [IntegrationFact]
    public async Task SatisIadeFaturasi_MuhasebeEkranindanIptal_TersKayitVeBakiyeSifirlanmasiDogru()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (satisService, muhasebeFisService) = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiServiceWithMuhasebeFisIptal(dbContext);
        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);

        var asilRequest = YeniBelgeRequest(SatisBelgesiTipi.SatisFaturasi, _musteriKartId, UrunVeHizmetSatirlari(100m, 500m, 20m));
        var asilOnaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, asilRequest);
        var asilFisDto = await fisService.MuhasebeFisiOlusturAsync(asilOnaylanmis.Id!.Value, CancellationToken.None);
        await muhasebeFisService.OnaylaAsync(asilFisDto.MuhasebeFisId!.Value, CancellationToken.None);

        dbContext.KurumFaturaNumaraSayaclari.Add(new KurumFaturaNumaraSayaci
        {
            KurumId = _kurumId, MaliYil = 2026, SeriKodu = "TBI", SonNumara = 0, AktifMi = true
        });
        await dbContext.SaveChangesAsync();
        var asilFaturaKesildi = await satisService.FaturaKesAsync(asilOnaylanmis.Id!.Value, new FaturaKesRequest { SeriKodu = "TBI" });

        var iadeRequest = YeniBelgeRequest(SatisBelgesiTipi.SatisIadeFaturasi, _musteriKartId, UrunVeHizmetSatirlari(100m, 500m, 20m));
        iadeRequest.KarsiTarafFaturaNo = $"MUS-{Guid.NewGuid():N}"[..20];
        iadeRequest.IadeEdilenBelgeId = asilFaturaKesildi.Id;
        iadeRequest.Satirlar[0].KaynakSatirId = asilFaturaKesildi.Satirlar[0].Id!.Value.ToString();
        iadeRequest.Satirlar[1].KaynakSatirId = asilFaturaKesildi.Satirlar[1].Id!.Value.ToString();

        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, iadeRequest);
        var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);
        await muhasebeFisService.OnaylaAsync(dto.MuhasebeFisId!.Value, CancellationToken.None);

        await AssertIptalTersKayitTamDogruAsync(satisService, dbContext, onaylanmis.Id!.Value, dto.MuhasebeFisId!.Value);
    }

    [IntegrationFact]
    public async Task AlisIadeFaturasi_MuhasebeEkranindanIptal_TersKayitVeBakiyeSifirlanmasiDogru()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (satisService, muhasebeFisService) = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiServiceWithMuhasebeFisIptal(dbContext);
        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);

        var asilRequest = YeniBelgeRequest(SatisBelgesiTipi.AlisFaturasi, _tedarikciKartId, UrunVeHizmetSatirlari(100m, 500m, 20m));
        asilRequest.KarsiTarafFaturaNo = $"TED-{Guid.NewGuid():N}"[..20];
        var asilOnaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, asilRequest);
        var asilFisDto = await fisService.MuhasebeFisiOlusturAsync(asilOnaylanmis.Id!.Value, CancellationToken.None);
        await muhasebeFisService.OnaylaAsync(asilFisDto.MuhasebeFisId!.Value, CancellationToken.None);

        var iadeRequest = YeniBelgeRequest(SatisBelgesiTipi.AlisIadeFaturasi, _tedarikciKartId, UrunVeHizmetSatirlari(100m, 500m, 20m));
        iadeRequest.IadeEdilenBelgeId = asilOnaylanmis.Id;
        iadeRequest.Satirlar[0].KaynakSatirId = asilOnaylanmis.Satirlar[0].Id!.Value.ToString();
        iadeRequest.Satirlar[1].KaynakSatirId = asilOnaylanmis.Satirlar[1].Id!.Value.ToString();

        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, iadeRequest);
        var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);
        await muhasebeFisService.OnaylaAsync(dto.MuhasebeFisId!.Value, CancellationToken.None);

        await AssertIptalTersKayitTamDogruAsync(satisService, dbContext, onaylanmis.Id!.Value, dto.MuhasebeFisId!.Value);
    }

    // ─────────────────────────────────────────────────────────────
    // 5a: Genel fiş endpoint'inden SatisBelgesi kaynaklı fiş iptali reddedilir
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task GenelFisIptalEndpointi_SatisBelgesiKaynakliFisiReddederVeHicbirKayitDegismez()
    {
        var request = YeniBelgeRequest(SatisBelgesiTipi.SatisFaturasi, _musteriKartId, UrunVeHizmetSatirlari(100m, 500m, 20m));

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (satisService, muhasebeFisService) = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiServiceWithMuhasebeFisIptal(dbContext);
        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, request);

        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);
        var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);
        await muhasebeFisService.OnaylaAsync(dto.MuhasebeFisId!.Value, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<BaseException>(
            () => muhasebeFisService.IptalEtAsync(dto.MuhasebeFisId!.Value, null, CancellationToken.None));
        Assert.Contains("genel fiş iptali ile iptal edilemez", ex.Message);

        var fisDb = await dbContext.MuhasebeFisler.AsNoTracking().FirstAsync(x => x.Id == dto.MuhasebeFisId!.Value);
        Assert.Equal(MuhasebeFisDurumlari.Onayli, fisDb.Durum);
        Assert.False(fisDb.TersKayitFisId.HasValue);
        Assert.False(await dbContext.MuhasebeFisler.AsNoTracking().AnyAsync(x => x.IptalEdilenFisId == fisDb.Id));

        var belgeDb = await dbContext.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.Id == onaylanmis.Id);
        Assert.Equal(TicariBelgeMuhasebeDurumu.Onaylandi, belgeDb.MuhasebeDurumu);
        Assert.True(belgeDb.MuhasebeFisId.HasValue);
    }

    // ─────────────────────────────────────────────────────────────
    // 5b: Operasyon endpoint'inden muhasebeleştirilmiş belge iptali reddedilir
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task OperasyonIptalEndpointi_MuhasebelestirilmisBelgeyiReddederVeHicbirKayitDegismez()
    {
        var request = YeniBelgeRequest(SatisBelgesiTipi.SatisFaturasi, _musteriKartId, UrunVeHizmetSatirlari(100m, 500m, 20m));

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (satisService, muhasebeFisService) = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiServiceWithMuhasebeFisIptal(dbContext);
        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, request);

        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);
        var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);
        await muhasebeFisService.OnaylaAsync(dto.MuhasebeFisId!.Value, CancellationToken.None);

        var ticariBelgeService = new TicariBelgeService(
            satisService,
            taslakOlusturmaService: null!,
            new SatisBelgesiMuhasebeTestSupport.FakeUserAccessScopeService(),
            mapper: null!);

        var ex = await Assert.ThrowsAsync<BaseException>(
            () => ticariBelgeService.IptalEtAsync(onaylanmis.Id!.Value, CancellationToken.None));
        Assert.Contains("operasyon ekranından iptal edilemez", ex.Message);

        var fisDb = await dbContext.MuhasebeFisler.AsNoTracking().FirstAsync(x => x.Id == dto.MuhasebeFisId!.Value);
        Assert.Equal(MuhasebeFisDurumlari.Onayli, fisDb.Durum);

        var belgeDb = await dbContext.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.Id == onaylanmis.Id);
        Assert.Equal(TicariBelgeDurumu.Hazir, belgeDb.TicariDurum);
        Assert.Equal(TicariBelgeMuhasebeDurumu.Onaylandi, belgeDb.MuhasebeDurumu);
    }

    // ─────────────────────────────────────────────────────────────
    // 5c: Kapatılmış cari hareket iptali engeller, transaction tamamen geri alınır
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task KapatilmisCariHareketVarken_IptalReddedilirVeTransactionTamamenGeriAlinir()
    {
        var request = YeniBelgeRequest(SatisBelgesiTipi.SatisFaturasi, _musteriKartId, UrunVeHizmetSatirlari(100m, 500m, 20m));

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (satisService, muhasebeFisService) = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiServiceWithMuhasebeFisIptal(dbContext);
        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, request);

        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);
        var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);
        await muhasebeFisService.OnaylaAsync(dto.MuhasebeFisId!.Value, CancellationToken.None);

        // Cari hareket, servis akışını BAYPAS EDEREK kısmen kapatılmış olarak işaretlenir - bir
        // tahsilat/mahsup ile eşleştirilmiş olduğunu simüle eder. ExecuteUpdateAsync (ham SQL)
        // YERİNE, dbContext'in İZLEDİĞİ (fiş oluşturma sırasında Add edilmiş) örnek doğrudan
        // GÜNCELLENİR - aksi halde ExecuteUpdateAsync veritabanını değiştirse de, aynı dbContext
        // üzerinden IptalEtCariHareketleriAsync'in yaptığı sorgu EF'in identity map'indeki BAYAT
        // izlenen örneği döndürür, ham SQL güncellemesi hiç görünmez.
        var hareket = await dbContext.CariHareketler
            .SingleAsync(x => x.KaynakId == onaylanmis.Id && x.KaynakModul == MuhasebeKaynakModulleri.SatisBelgesi);
        hareket.KapandiMi = true;
        hareket.KapananTutar = 840m;
        hareket.KalanTutar = 0m;
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BaseException>(() => satisService.IptalEtAsync(onaylanmis.Id!.Value, CancellationToken.None));
        Assert.Contains("kapatılmış", ex.Message);

        // Transaction TAMAMEN geri alınmış olmalı: fiş hâlâ Onaylı, ters kayıt YOK, belge
        // durumları DEĞİŞMEMİŞ, stok hareketi hâlâ Aktif.
        var fisDb = await dbContext.MuhasebeFisler.AsNoTracking().FirstAsync(x => x.Id == dto.MuhasebeFisId!.Value);
        Assert.Equal(MuhasebeFisDurumlari.Onayli, fisDb.Durum);
        Assert.False(fisDb.TersKayitFisId.HasValue);
        Assert.False(await dbContext.MuhasebeFisler.AsNoTracking().AnyAsync(x => x.IptalEdilenFisId == fisDb.Id));

        var belgeDb = await dbContext.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.Id == onaylanmis.Id);
        Assert.Equal(TicariBelgeMuhasebeDurumu.Onaylandi, belgeDb.MuhasebeDurumu);

        var stokHareketler = await dbContext.StokHareketleri.AsNoTracking()
            .Where(x => x.KaynakId == onaylanmis.Id && x.KaynakModul == MuhasebeKaynakModulleri.SatisBelgesi).ToListAsync();
        Assert.NotEmpty(stokHareketler);
        Assert.All(stokHareketler, x => Assert.Equal(StokHareketDurumlari.Aktif, x.Durum));
    }
}
