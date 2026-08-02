using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.CariHareketler.Mapping;
using STYS.Muhasebe.CariHareketler.Repositories;
using STYS.Muhasebe.CariHareketler.Services;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.CariKartlar.Mapping;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.MuhasebeDonemleri.Mapping;
using STYS.Muhasebe.MuhasebeDonemleri.Repositories;
using STYS.Muhasebe.MuhasebeDonemleri.Services;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using STYS.Muhasebe.MuhasebeFisleri.Mapping;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Mapping;
using STYS.Muhasebe.SatisBelgeleri.Services;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Mapping;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Repositories;
using STYS.TicariBelgeler.Services;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// c799337 sonrası görev: "ticari belge iptalindeki yarış koşullarını kapat" - ISatisBelgesiService.
/// OperasyonelIptalEtAsync'in artık transaction-dışı bir GetByIdAsync ön kontrolüne DEĞİL, ortak
/// iptal transaction'ı içinde WITH (UPDLOCK, ROWLOCK) ile alınan GÜNCEL bir DB okumasına dayandığını,
/// ve cari hareket kapaması ile belge iptalinin AYNI satır üzerinde uyumlu kilitleme disipliniyle
/// (SatisBelgesiService.IptalEtCariHareketleriAsync ↔ CariHareketKapamaService.
/// TahsilatOdemeIcinCariHareketOlusturVeKapatAsync) birbirini dışladığını GERÇEK SQL Server'a karşı,
/// İKİ AYRI DbContext ile GERÇEKTEN eşzamanlı çalıştırarak kanıtlayan hedefli entegrasyon testleri.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class TicariBelgeIptalYarisKosuluIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "TBIYARIS-944";

    private string _uniqueSuffix = TestMarker;
    private int _kurumId;
    private int _ilId;
    private int _tesisId;
    private int _gelirHesapId;
    private int _kdvSatisHesapId;
    private int _musteriKartId;
    private int _musteriHesapId;

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
        var kdvSatisHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.KDVHesaplanan, "KDVS", _tesisId);
        var musteriHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "MUS", _tesisId);
        dbContext.MuhasebeHesapPlanlari.AddRange(gelirHesap, kdvSatisHesap, musteriHesap);
        await dbContext.SaveChangesAsync();
        _gelirHesapId = gelirHesap.Id;
        _kdvSatisHesapId = kdvSatisHesap.Id;
        _musteriHesapId = musteriHesap.Id;

        var musteri = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "MUS", CariKartTipleri.Musteri, _tesisId, musteriHesap.Id);
        dbContext.CariKartlar.Add(musteri);
        await dbContext.SaveChangesAsync();
        _musteriKartId = musteri.Id;
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString) || _kurumId <= 0)
        {
            return;
        }

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await dbContext.MuhasebeHesapBakiyeleri.Where(x => x.TesisId == _tesisId).ExecuteDeleteAsync();

        // TahsilatOdemeBelgeleri.KapatilacakCariHareketId -> CariHareketler Restrict FK ile bağlı -
        // bu yüzden cari hareketlerden (SatisBelgesiMuhasebeTestSupport.CleanupAsync içinde
        // silinir) ÖNCE tahsilat/ödeme belgeleri silinmelidir.
        await dbContext.TahsilatOdemeBelgeleri
            .Where(x => x.BelgeNo.Contains(_uniqueSuffix))
            .ExecuteDeleteAsync();

        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, _uniqueSuffix, _tesisId, _kurumId, _ilId);
    }

    // ─────────────────────────────────────────────────────────────
    // Yardımcılar
    // ─────────────────────────────────────────────────────────────

    private CreateSatisBelgesiRequest YeniHizmetBelgesiRequest() => new()
    {
        BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
        BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
        TesisId = _tesisId,
        CariKartId = _musteriKartId,
        BelgeTarihi = new DateTime(2026, 3, 1),
        Satirlar =
        [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Hizmet satiri", SatirTipi = SatisBelgesiSatirTipi.EkHizmet,
                Miktar = 1, BirimFiyat = 1000m,
                KdvUygulamaTipi = (int)STYS.Muhasebe.Kdv.Enums.KdvUygulamaTipi.Kdvli, KdvOrani = 20m
            }
        ]
    };

    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<SatisBelgesiProfile>();
            cfg.AddProfile<MuhasebeDonemProfile>();
            cfg.AddProfile<MuhasebeFisProfile>();
            cfg.AddProfile<TahsilatOdemeBelgesiProfile>();
            cfg.AddProfile<CariKartProfile>();
            cfg.AddProfile<CariHareketProfile>();
        }, NullLoggerFactory.Instance);
        return config.CreateMapper();
    }

    private static ICariHareketKapamaService CreateCariHareketKapamaService(StysAppDbContext dbContext)
    {
        var mapper = CreateMapper();
        var tahsilatRepo = new TahsilatOdemeBelgesiRepository(dbContext, mapper);
        var cariHareketRepo = new CariHareketRepository(dbContext, mapper);
        var muhasebeDonemService = CreateRealMuhasebeDonemService(dbContext);
        return new CariHareketKapamaService(
            dbContext, tahsilatRepo, cariHareketRepo, muhasebeDonemService,
            new SatisBelgesiMuhasebeTestSupport.FakeUserAccessScopeService(), mapper);
    }

    private static IMuhasebeDonemService CreateRealMuhasebeDonemService(StysAppDbContext dbContext)
    {
        var mapper = CreateMapper();
        var repo = new MuhasebeDonemRepository(dbContext, mapper);
        return new MuhasebeDonemService(repo, mapper, dbContext, new SatisBelgesiMuhasebeTestSupport.FakeMuhasebeTesisScopeService());
    }

    /// <summary>
    /// Yalnızca BEKLENEN bir reddi (BaseException) yakalar ve (false, mesaj) döner; başarıda
    /// (true, null) döner. Başka bir istisna tipi (ör. NullReferenceException, veri tutarsızlığı)
    /// KOŞULSUZ YUTULMAZ - yukarı fırlatılır, test gerçek bir hatayı "beklenen yarış kaybı" sanıp
    /// gizlemez.
    /// </summary>
    private static async Task<(bool Basarili, string? HataMesaji)> SafeCallAsync(Func<Task> action)
    {
        try
        {
            await action();
            return (true, null);
        }
        catch (BaseException ex)
        {
            return (false, ex.Message);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 1: Operasyonel ön okuma sonrası eşzamanlı onay+fiş oluşturma - iptal reddedilir, ters fiş oluşmaz
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task OperasyonelIptal_EszamanliFisOlusturmaIleYarisirsa_ReddedilirVeTersFisOlusmaz()
    {
        await using var setupCtx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var setupSatisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(setupCtx);
        var created = await setupSatisService.CreateAsync(YeniHizmetBelgesiRequest());
        await setupSatisService.MuhasebeOnayinaGonderAsync(created.Id!.Value);
        await setupSatisService.MuhasebeOnaylaAsync(created.Id!.Value);
        // Bu noktada MuhasebeDurumu=Onaylandi, MuhasebeFisId hâlâ null - fiş oluşturma ve
        // operasyonel iptal artık AYNI satırı hedefleyen, gerçekten eşzamanlı iki transaction
        // olarak yarışabilir.

        await using var ctx1 = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await using var ctx2 = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var satisServiceA = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(ctx1);
        var ticariBelgeServiceA = new TicariBelgeService(
            satisServiceA, taslakOlusturmaService: null!, new SatisBelgesiMuhasebeTestSupport.FakeUserAccessScopeService(), mapper: null!);
        var fisServiceB = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(ctx2);

        var taskA = SafeCallAsync(() => ticariBelgeServiceA.IptalEtAsync(created.Id!.Value, CancellationToken.None));
        var taskB = fisServiceB.MuhasebeFisiOlusturAsync(created.Id!.Value, CancellationToken.None);

        var (aBasarili, aHata) = await taskA;
        var bDto = await taskB;

        // Operasyonel iptal, belge zaten Onaylandi olduğundan (fiş oluşturma ile eşzamanlı
        // çalışsa dahi, kilitli/güncel okuma bunu HER durumda görür) reddedilmelidir.
        Assert.False(aBasarili);
        Assert.Contains("operasyon ekranından iptal edilemez", aHata);
        Assert.NotNull(bDto.MuhasebeFisId);

        await using var verifyCtx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var belgeDb = await verifyCtx.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.Id == created.Id);
        Assert.Equal(TicariBelgeDurumu.Hazir, belgeDb.TicariDurum);
        Assert.Equal(TicariBelgeMuhasebeDurumu.Onaylandi, belgeDb.MuhasebeDurumu);
        Assert.True(belgeDb.MuhasebeFisId.HasValue);

        // Ters fiş (veya herhangi bir iptal edilen fiş) hiç oluşmamalı - operasyonel iptal bu
        // belgeye ait fiş üzerinde SatisBelgesiFisiIptalEtAsync'i ASLA çağırmaz.
        Assert.False(await verifyCtx.MuhasebeFisler.AsNoTracking()
            .AnyAsync(x => x.KaynakId == created.Id && x.Durum != MuhasebeFisDurumlari.Taslak && x.Durum != MuhasebeFisDurumlari.Onayli));
        Assert.False(await verifyCtx.MuhasebeFisler.AsNoTracking().AnyAsync(x => x.IptalEdilenFisId != null && x.KaynakId == created.Id));
    }

    // ─────────────────────────────────────────────────────────────
    // 2: Cari kapama ile belge iptali eşzamanlı çalışırsa yalnızca biri başarılı olur
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task CariKapamaIleBelgeIptali_EszamanliCalisirsaYalnizcaBiriBasarili()
    {
        await using var setupCtx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (satisServiceSetup, muhasebeFisServiceSetup) = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiServiceWithMuhasebeFisIptal(setupCtx);
        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisServiceSetup, YeniHizmetBelgesiRequest());

        var fisServiceSetup = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(setupCtx);
        var fisDto = await fisServiceSetup.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);
        await muhasebeFisServiceSetup.OnaylaAsync(fisDto.MuhasebeFisId!.Value, CancellationToken.None);

        var cariHareketId = (await setupCtx.CariHareketler.AsNoTracking()
            .SingleAsync(x => x.KaynakId == onaylanmis.Id && x.KaynakModul == MuhasebeKaynakModulleri.SatisBelgesi)).Id;

        var tahsilatBelge = new TahsilatOdemeBelgesi
        {
            BelgeNo = $"THS-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTarihi = new DateTime(2026, 3, 10),
            BelgeTipi = TahsilatOdemeBelgeTipleri.Tahsilat,
            CariKartId = _musteriKartId,
            Tutar = onaylanmis.GenelToplam,
            ParaBirimi = "TRY",
            OdemeYontemi = OdemeYontemleri.Nakit,
            KapatilacakCariHareketId = cariHareketId,
            Durum = TahsilatOdemeBelgeDurumlari.Aktif
        };
        setupCtx.TahsilatOdemeBelgeleri.Add(tahsilatBelge);
        await setupCtx.SaveChangesAsync();

        await using var ctx1 = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await using var ctx2 = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (satisServiceA, _) = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiServiceWithMuhasebeFisIptal(ctx1);
        var kapamaServiceB = CreateCariHareketKapamaService(ctx2);

        var taskA = SafeCallAsync(() => satisServiceA.IptalEtAsync(onaylanmis.Id!.Value, CancellationToken.None));
        var taskB = SafeCallAsync(async () =>
            await kapamaServiceB.TahsilatOdemeIcinCariHareketOlusturVeKapatAsync(tahsilatBelge.Id, CancellationToken.None));

        var (aBasarili, aHata) = await taskA;
        var (bBasarili, bHata) = await taskB;

        Assert.True(aBasarili ^ bBasarili, $"Tam olarak biri başarılı olmalı. A={aBasarili} ({aHata}), B={bBasarili} ({bHata})");

        await using var verifyCtx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var cariHareketDb = await verifyCtx.CariHareketler.AsNoTracking().SingleAsync(x => x.Id == cariHareketId);
        var yetimKapamaVarMi = await verifyCtx.CariHareketler.AsNoTracking()
            .AnyAsync(x => x.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi && x.KaynakId == tahsilatBelge.Id);
        var fisDb = await verifyCtx.MuhasebeFisler.AsNoTracking().FirstAsync(x => x.Id == fisDto.MuhasebeFisId!.Value);
        var belgeDb = await verifyCtx.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.Id == onaylanmis.Id);

        if (aBasarili)
        {
            // İptal kazandı: cari hareket İptal olur, kapama YETİM olarak oluşmamıştır (B'nin
            // kendi kilitli okuması, iptal commit olduktan SONRA GÜNCEL - artık Aktif olmayan -
            // durumu görüp reddetmiştir).
            Assert.Equal(CariHareketDurumlari.Iptal, cariHareketDb.Durum);
            Assert.False(yetimKapamaVarMi);
            Assert.Contains("aktif", bHata, StringComparison.OrdinalIgnoreCase);

            Assert.Equal(MuhasebeFisDurumlari.Iptal, fisDb.Durum);
            Assert.True(fisDb.TersKayitFisId.HasValue);
            Assert.Equal(TicariBelgeDurumu.IptalEdildi, belgeDb.TicariDurum);
        }
        else
        {
            // Kapama kazandı: cari hareket kapalı/kısmi kapalı kalır, iptal reddedilmiştir -
            // fiş/belge/stok TAMAMEN tutarlı (iptal transaction'ı tam geri alınmış) kalır.
            Assert.True(cariHareketDb.KapandiMi || cariHareketDb.KapananTutar > 0m);
            Assert.True(yetimKapamaVarMi);
            Assert.Contains("kapat", aHata, StringComparison.OrdinalIgnoreCase);

            Assert.Equal(MuhasebeFisDurumlari.Onayli, fisDb.Durum);
            Assert.False(fisDb.TersKayitFisId.HasValue);
            Assert.False(await verifyCtx.MuhasebeFisler.AsNoTracking().AnyAsync(x => x.IptalEdilenFisId == fisDb.Id));
            Assert.Equal(TicariBelgeDurumu.Hazir, belgeDb.TicariDurum);
            Assert.Equal(TicariBelgeMuhasebeDurumu.Onaylandi, belgeDb.MuhasebeDurumu);
        }

        // İkinci bir "temizlik" denemesi olarak: iptal edilmiş bir hareketin sonradan
        // kapatılması VEYA kapatılmış bir hareketin iptal edilmesi mümkün OLMAMALI - hangi taraf
        // kazanmış olursa olsun, KAYBEDEN tarafın işlemi TEKRAR denendiğinde de aynı şekilde
        // reddedildiğini doğrula (idempotent ret, veri tutarsızlığı yaratmaz). YENİ, taze bir
        // DbContext kullanılır - ctx1/ctx2'nin EF change tracker'ı, İLK (rollback ile geri alınmış)
        // denemeden kalma bayat izlenen örnekler barındırabilir; production'da da her istek YENİ
        // bir scoped DbContext ile gelir, aynı (başarısız) DbContext asla tekrar kullanılmaz.
        await using var retryCtx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        if (aBasarili)
        {
            var kapamaServiceRetry = CreateCariHareketKapamaService(retryCtx);
            var tekrarKapama = await SafeCallAsync(async () =>
                await kapamaServiceRetry.TahsilatOdemeIcinCariHareketOlusturVeKapatAsync(tahsilatBelge.Id, CancellationToken.None));
            Assert.False(tekrarKapama.Basarili);
        }
        else
        {
            var (satisServiceRetry, _) = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiServiceWithMuhasebeFisIptal(retryCtx);
            var tekrarIptal = await SafeCallAsync(() => satisServiceRetry.IptalEtAsync(onaylanmis.Id!.Value, CancellationToken.None));
            Assert.False(tekrarIptal.Basarili);
            Assert.Contains("kapat", tekrarIptal.HataMesaji!, StringComparison.OrdinalIgnoreCase);
        }
    }
}
