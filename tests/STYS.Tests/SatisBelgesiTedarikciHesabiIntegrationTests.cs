using Microsoft.EntityFrameworkCore;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Part D — ff66146'nın ürettiği "alış belgesi, tesisteki ilk 320 hesabı yerine belgenin kendi
/// tedarikçisinin hesabına kaydedilir" davranışını GERÇEK SQL Server üzerinde, gerçek public
/// servis akışlarıyla (CreateAsync → MuhasebeOnayinaGonderAsync → MuhasebeOnaylaAsync →
/// MuhasebeFisiOlusturAsync) doğrulayan regresyon testleri.
///
/// Mevcut stale-tracking testi (SatisBelgesiAlisTedarikciHesabiTransactionSnapshotIntegrationTests,
/// f7a44fe) buradan AYRIDIR ve KORUNMUŞTUR — burada kopyası oluşturulmadı.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class SatisBelgesiTedarikciHesabiIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "TEDHESAP-266";

    private string _uniqueSuffix = TestMarker;
    private int _kurumId;
    private int _ilId;
    private int _tesisId;
    private int _giderHesapId;
    private int _kdvHesapId;
    private int _tevkifatKarsiligiHesapId;
    private int _tedarikci1Id;
    private int _tedarikci1HesapId;
    private int _tedarikci2Id;
    private int _tedarikci2HesapId;

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

        var giderHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.GiderHizmetMaliyet, "GIDER", tesis.Id);
        var kdvHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.KDVIndirilecek, "KDV", tesis.Id);
        var stokHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.StokTicariMal, "STOK", tesis.Id);
        var tevkifatKarsiligiHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "TEVK", tesis.Id);
        var tedarikci1Hesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "T1", tesis.Id);
        var tedarikci2Hesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "T2", tesis.Id);
        dbContext.MuhasebeHesapPlanlari.AddRange(giderHesap, kdvHesap, stokHesap, tevkifatKarsiligiHesap, tedarikci1Hesap, tedarikci2Hesap);
        await dbContext.SaveChangesAsync();
        _giderHesapId = giderHesap.Id;
        _kdvHesapId = kdvHesap.Id;
        _tevkifatKarsiligiHesapId = tevkifatKarsiligiHesap.Id;
        _tedarikci1HesapId = tedarikci1Hesap.Id;
        _tedarikci2HesapId = tedarikci2Hesap.Id;

        var tedarikci1 = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "T1", CariKartTipleri.Tedarikci, tesis.Id, tedarikci1Hesap.Id);
        tedarikci1.VergiNoTckn = "2222222222";
        var tedarikci2 = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "T2", CariKartTipleri.Tedarikci, tesis.Id, tedarikci2Hesap.Id);
        tedarikci2.VergiNoTckn = "3333333333";
        dbContext.CariKartlar.AddRange(tedarikci1, tedarikci2);
        await dbContext.SaveChangesAsync();
        _tedarikci1Id = tedarikci1.Id;
        _tedarikci2Id = tedarikci2.Id;
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString) || _kurumId <= 0)
        {
            return;
        }

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, _uniqueSuffix, _tesisId, _kurumId, _ilId);
    }

    [IntegrationFact]
    public async Task AlisFaturasi_IkinciTedarikciSeciliyken_TedarikciHesabiVeCariKartIdIkinciTedarikciyiGosterir()
    {
        var request = YeniAlisBelgeRequest(SatisBelgesiTipi.AlisFaturasi, _tedarikci2Id, [
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

        await AssertIkinciTedarikciKullanildiAsync(dbContext, onaylanmis.Id!.Value, dto.MuhasebeFisId);
    }

    [IntegrationFact]
    public async Task AlisTevkifatliFatura_IkinciTedarikciSeciliyken_TedarikciHesabiVeCariKartIdIkinciTedarikciyiGosterir()
    {
        var request = YeniAlisBelgeRequest(SatisBelgesiTipi.AlisFaturasi, _tedarikci2Id, [
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

        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext, tevkifatKarsiligiHesapPlaniId: _tevkifatKarsiligiHesapId);
        var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);

        await AssertIkinciTedarikciKullanildiAsync(dbContext, onaylanmis.Id!.Value, dto.MuhasebeFisId);
    }

    [IntegrationFact]
    public async Task AlisIadeFaturasi_IkinciTedarikciSeciliyken_TedarikciHesabiVeCariKartIdIkinciTedarikciyiGosterir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);

        // AlisIadeFaturasi artık geçerli bir IadeEdilenBelgeId (aynı tedarikçiye ait, muhasebe
        // onaylı ve geçerli fişli bir AlisFaturasi) gerektirir - önce asıl faturayı oluştur.
        var asilRequest = YeniAlisBelgeRequest(SatisBelgesiTipi.AlisFaturasi, _tedarikci2Id, [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Asil alis", Miktar = 1, BirimFiyat = 500m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
            }
        ]);
        var asilOnaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, asilRequest);
        await fisService.MuhasebeFisiOlusturAsync(asilOnaylanmis.Id!.Value, CancellationToken.None);

        var request = YeniAlisBelgeRequest(SatisBelgesiTipi.AlisIadeFaturasi, _tedarikci2Id, [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Iade edilen hizmet", Miktar = 1, BirimFiyat = 500m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m,
                KaynakSatirId = asilOnaylanmis.Satirlar[0].Id!.Value.ToString()
            }
        ], iadeEdilenBelgeId: asilOnaylanmis.Id);

        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, request);

        var dto = await fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);

        await AssertIkinciTedarikciKullanildiAsync(dbContext, onaylanmis.Id!.Value, dto.MuhasebeFisId);
    }

    [IntegrationFact]
    public async Task TedarikciHesabiMuhasebeOnayindanSonraPasifYapilirsa_MuhasebeFisiEngellenirVeHicbirKayitOlusmaz()
    {
        // Teste özel, GEÇERLİ tedarikçi (tedarikci1) ve hesap planıyla, gerçek public servis
        // akışının tamamı çalıştırılır: belge oluştur -> muhasebe onayına gönder -> muhasebe
        // onayını tamamla. Bu noktaya kadar hesap AKTİF olduğundan hiçbir adım başarısız olmaz.
        var request = YeniAlisBelgeRequest(SatisBelgesiTipi.AlisFaturasi, _tedarikci1Id, [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Hizmet alimi", Miktar = 1, BirimFiyat = 1000m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
            }
        ]);

        await using (var olusturmaDbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext())
        {
            var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(olusturmaDbContext);
            var onaylanmisBelgeId = (await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, request)).Id!.Value;

            // Belge onaylandıktan SONRA, AYRI bir DbContext üzerinden tedarikçinin bağlı hesap
            // planı pasif hâle getirilir - yani belge oluşturulurken/onaylanırken geçerli olan
            // tedarikçi, muhasebeleştirme ANINDA artık geçersizdir.
            await using (var gecersizlestirmeDbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext())
            {
                var hesapPlani = await gecersizlestirmeDbContext.MuhasebeHesapPlanlari
                    .FirstAsync(x => x.Id == _tedarikci1HesapId);
                hesapPlani.AktifMi = false;
                await gecersizlestirmeDbContext.SaveChangesAsync();
            }

            // Muhasebe fişi servisi TAMAMEN YENİ bir DbContext ile oluşturulur - böylece önceden
            // (bu metodun üst kısmında) izlenmiş olabilecek herhangi bir entity değeri
            // kullanılmaz; MuhasebeFisiOlusturAsync'in DB'den GERÇEKTEN güncel/geçersiz durumu
            // okuduğu doğrulanır.
            await using var fisDbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
            var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(fisDbContext);

            var ex = await Assert.ThrowsAsync<BaseException>(
                () => fisService.MuhasebeFisiOlusturAsync(onaylanmisBelgeId, CancellationToken.None));
            Assert.Contains("aktif/hareket görebilir/detay hesap değil", ex.Message);

            // Yeni/no-tracking bir doğrulama context'iyle: MuhasebeFis, MuhasebeFisSatir,
            // CariHareket, StokHareketi kayıtlarının HİÇBİRİ oluşmamış ve belge.MuhasebeFisId
            // null kalmış olmalı.
            await using var dogrulamaDbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
            await SatisBelgesiMuhasebeTestSupport.AssertHicMuhasebeKaydiOlusmadiAsync(dogrulamaDbContext, onaylanmisBelgeId);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Yardımcılar
    // ─────────────────────────────────────────────────────────────

    private CreateSatisBelgesiRequest YeniAlisBelgeRequest(
        SatisBelgesiTipi belgeTipi, int cariKartId, List<CreateSatisBelgesiSatiriRequest> satirlar,
        int? iadeEdilenBelgeId = null) => new()
    {
        BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
        BelgeTipi = belgeTipi,
        TesisId = _tesisId,
        CariKartId = cariKartId,
        BelgeTarihi = new DateTime(2026, 1, 15),
        // AlisFaturasi (gelen belge) artık onay aşamasında KarsiTarafFaturaNo zorunlu tutar.
        KarsiTarafFaturaNo = belgeTipi == SatisBelgesiTipi.AlisFaturasi
            ? $"TED-{Guid.NewGuid():N}"[..20]
            : null,
        IadeEdilenBelgeId = iadeEdilenBelgeId,
        Satirlar = satirlar
    };

    private async Task AssertIkinciTedarikciKullanildiAsync(
        STYS.Infrastructure.EntityFramework.StysAppDbContext dbContext, int belgeId, int? muhasebeFisId)
    {
        Assert.NotNull(muhasebeFisId);
        var fis = await dbContext.MuhasebeFisler.AsNoTracking().Include(x => x.Satirlar)
            .FirstAsync(x => x.Id == muhasebeFisId);

        var tedarikciSatiri = Assert.Single(fis.Satirlar, s => s.CariKartId == _tedarikci2Id);
        Assert.Equal(_tedarikci2HesapId, tedarikciSatiri.MuhasebeHesapPlaniId);

        // Birinci tedarikcinin hesabi/karti HICBIR sekilde fişte bulunmamalı.
        Assert.DoesNotContain(fis.Satirlar, s => s.MuhasebeHesapPlaniId == _tedarikci1HesapId);
        Assert.DoesNotContain(fis.Satirlar, s => s.CariKartId == _tedarikci1Id);

        var cariHareket = await dbContext.CariHareketler.AsNoTracking().FirstAsync(x => x.KaynakId == belgeId);
        Assert.Equal(_tedarikci2Id, cariHareket.CariKartId);

        // Fiş ve cari hareket AYNI tedarikçiyi kullanmalı.
        Assert.Equal(tedarikciSatiri.CariKartId, cariHareket.CariKartId);

        Assert.Equal(fis.ToplamBorc, fis.ToplamAlacak);
    }
}
