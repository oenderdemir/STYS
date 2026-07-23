using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Iller.Entities;
using STYS.Kurumlar.Entities;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Muhasebe.MuhasebeDonemleri.Entities;
using STYS.Muhasebe.MuhasebeDonemleri.Mapping;
using STYS.Muhasebe.MuhasebeDonemleri.Repositories;
using STYS.Muhasebe.MuhasebeDonemleri.Services;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using STYS.Muhasebe.MuhasebeFisleri.Mapping;
using STYS.Muhasebe.MuhasebeFisleri.Repositories;
using STYS.Muhasebe.MuhasebeFisleri.Services;
using STYS.Muhasebe.MuhasebeHesapBakiyeleri.Services;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.PosTahsilatValorleri.Entities;
using STYS.Muhasebe.PosTahsilatValorleri.Services;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.AspNetCore.Logging;
using TOD.Platform.Licensing.Abstractions;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Tests;

/// <summary>
/// POS valor takip/aktarim ozelliginin eszamanlilik, tesis yetkisi ve fis yasam-dongusu
/// senaryolarini GERCEK SQL Server test veritabanina karsi dogrular. Kod incelemesi
/// bulgularinin (bkz. commit 903fa6a duzeltmeleri) dogrulanmasi icin yazilmistir:
///   1) Eszamanli iki aktarim - yalnizca biri basarili olmali.
///   2) Iki "instance"in ayni tesis/mali yil icin fis no uretmesi - cakisma olmamali.
///   3) Aktarim ile tahsilat iptalinin yarismasi - tutarsiz durum olusmamali.
///   4) Ayni Aktarildi kayda iki duzeltme-ters-kayit istegi - yalnizca bir ters kayit uretilmeli.
///   5) Negatif manuel tutarlar reddedilmeli, kayit degismeden kalmali.
///   6) Farkli tesise ait kayda erisim 403 ile reddedilmeli.
///   7) Transfer fisinin onay/ters-kayit yasam dongusu (Taslak birakilmaz, hemen Onayli olur;
///      duzeltme-ters-kayit sonrasi orijinal Iptal, yeni fis TersKayit).
/// </summary>
[Trait("Category", "Integration")]
public class PosTahsilatValorIntegrationTests : IAsyncLifetime
{
    private static readonly string? ConnectionString =
        Environment.GetEnvironmentVariable(IntegrationFactAttribute.ConnectionStringEnvVar);

    private const string TestMarker = "PVI-970";

    private int KurumId;
    private int TesisAId;
    private int TesisBId;
    private int HesapPlaniPosId;
    private int HesapPlaniBankaId;
    private int HesapPlaniKomisyonId;
    private int KasaBankaBankaAId;
    private int KasaBankaPosAId; // KomisyonOrani = null (manuel komisyon gerektirir)
    private int KasaBankaPosKomisyonluAId; // KomisyonOrani = 2 (otomatik hesaplama)
    private int CariKartAId;
    private int TesisBBankaId;
    private int TesisBPosId;
    private int CariKartBId;

    private string _uniqueSuffix = TestMarker;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            return;
        }

        await using var dbContext = CreateDbContext();

        var uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];
        _uniqueSuffix = uniqueSuffix;

        var kurum = new Kurum { Kod = uniqueSuffix, Ad = "Test Kurum " + uniqueSuffix, AktifMi = true };
        dbContext.Kurumlar.Add(kurum);
        var il = new Il { Ad = "Test Il " + uniqueSuffix, AktifMi = true };
        dbContext.Iller.Add(il);
        await dbContext.SaveChangesAsync();
        KurumId = kurum.Id;

        var tesisA = new Tesis { KurumId = kurum.Id, IlId = il.Id, Ad = "Test Tesis A " + uniqueSuffix, Telefon = "0000", Adres = "Test Adres", AktifMi = true };
        var tesisB = new Tesis { KurumId = kurum.Id, IlId = il.Id, Ad = "Test Tesis B " + uniqueSuffix, Telefon = "0000", Adres = "Test Adres", AktifMi = true };
        dbContext.Tesisler.AddRange(tesisA, tesisB);
        await dbContext.SaveChangesAsync();
        TesisAId = tesisA.Id;
        TesisBId = tesisB.Id;

        // Zero-dot TamKod: MuhasebeHesapBakiyeGuncellemeService ust-hesap aramasini atlar (yalnizca
        // "." ile ayrilan gercek ust segmentler icin kayit arar), bu yuzden ekstra ana hesap seed
        // etmeye gerek yok. 109 kod dogrulamasi icin TamKod "1.10.109" ile baslamali.
        var hesapPos = new MuhasebeHesapPlani { Kod = uniqueSuffix + "-POS", TamKod = "1.10.109" + uniqueSuffix, Ad = "Test POS 109", AktifMi = true, DetayHesapMi = true, HareketGorebilirMi = true, HesapTipi = HesapTipi.DetayHesap };
        var hesapBanka = new MuhasebeHesapPlani { Kod = uniqueSuffix + "-BANKA", TamKod = "BANKA" + uniqueSuffix, Ad = "Test Banka 102", AktifMi = true, DetayHesapMi = true, HareketGorebilirMi = true, HesapTipi = HesapTipi.DetayHesap };
        var hesapKomisyon = new MuhasebeHesapPlani { Kod = uniqueSuffix + "-GIDER", TamKod = "GIDER" + uniqueSuffix, Ad = "Test Komisyon Gideri", AktifMi = true, DetayHesapMi = true, HareketGorebilirMi = true, HesapTipi = HesapTipi.DetayHesap };
        dbContext.MuhasebeHesapPlanlari.AddRange(hesapPos, hesapBanka, hesapKomisyon);
        await dbContext.SaveChangesAsync();
        HesapPlaniPosId = hesapPos.Id;
        HesapPlaniBankaId = hesapBanka.Id;
        HesapPlaniKomisyonId = hesapKomisyon.Id;

        var kasaBankaA = new KasaBankaHesap { TesisId = TesisAId, Tip = KasaBankaHesapTipleri.Banka, Kod = uniqueSuffix + "-BNK-A", Ad = "Test Banka A", ParaBirimi = "TRY", AktifMi = true, MuhasebeHesapPlaniId = HesapPlaniBankaId };
        dbContext.KasaBankaHesaplari.Add(kasaBankaA);
        await dbContext.SaveChangesAsync();
        KasaBankaBankaAId = kasaBankaA.Id;

        var kasaPosA = new KasaBankaHesap
        {
            TesisId = TesisAId,
            Tip = KasaBankaHesapTipleri.KrediKarti,
            Kod = uniqueSuffix + "-POS-A",
            Ad = "Test POS A (komisyon belirsiz)",
            ParaBirimi = "TRY",
            AktifMi = true,
            MuhasebeHesapPlaniId = HesapPlaniPosId,
            BagliBankaHesapId = KasaBankaBankaAId,
            ValorGunSayisi = 0,
            KomisyonOrani = null,
            KomisyonGiderHesapPlaniId = HesapPlaniKomisyonId
        };
        var kasaPosKomisyonluA = new KasaBankaHesap
        {
            TesisId = TesisAId,
            Tip = KasaBankaHesapTipleri.KrediKarti,
            Kod = uniqueSuffix + "-POS-A-KMSY",
            Ad = "Test POS A (komisyon %2)",
            ParaBirimi = "TRY",
            AktifMi = true,
            MuhasebeHesapPlaniId = HesapPlaniPosId,
            BagliBankaHesapId = KasaBankaBankaAId,
            ValorGunSayisi = 0,
            KomisyonOrani = 2m,
            KomisyonGiderHesapPlaniId = HesapPlaniKomisyonId
        };
        dbContext.KasaBankaHesaplari.AddRange(kasaPosA, kasaPosKomisyonluA);
        await dbContext.SaveChangesAsync();
        KasaBankaPosAId = kasaPosA.Id;
        KasaBankaPosKomisyonluAId = kasaPosKomisyonluA.Id;

        var cariA = new CariKart { TesisId = TesisAId, CariTipi = CariKartTipleri.Musteri, CariKodu = uniqueSuffix + "-A1", UnvanAdSoyad = "Test Musteri A1", AktifMi = true };
        dbContext.CariKartlar.Add(cariA);
        await dbContext.SaveChangesAsync();
        CariKartAId = cariA.Id;

        // Tesis B icin ayri POS/Banka/Cari - "farkli tesise erisim" testinde kullanilir.
        var kasaBankaB = new KasaBankaHesap { TesisId = TesisBId, Tip = KasaBankaHesapTipleri.Banka, Kod = uniqueSuffix + "-BNK-B", Ad = "Test Banka B", ParaBirimi = "TRY", AktifMi = true, MuhasebeHesapPlaniId = HesapPlaniBankaId };
        dbContext.KasaBankaHesaplari.Add(kasaBankaB);
        await dbContext.SaveChangesAsync();
        TesisBBankaId = kasaBankaB.Id;

        var kasaPosB = new KasaBankaHesap
        {
            TesisId = TesisBId,
            Tip = KasaBankaHesapTipleri.KrediKarti,
            Kod = uniqueSuffix + "-POS-B",
            Ad = "Test POS B",
            ParaBirimi = "TRY",
            AktifMi = true,
            MuhasebeHesapPlaniId = HesapPlaniPosId,
            BagliBankaHesapId = TesisBBankaId,
            ValorGunSayisi = 0,
            KomisyonOrani = 0m
        };
        dbContext.KasaBankaHesaplari.Add(kasaPosB);
        await dbContext.SaveChangesAsync();
        TesisBPosId = kasaPosB.Id;

        var cariB = new CariKart { TesisId = TesisBId, CariTipi = CariKartTipleri.Musteri, CariKodu = uniqueSuffix + "-B1", UnvanAdSoyad = "Test Musteri B1", AktifMi = true };
        dbContext.CariKartlar.Add(cariB);
        await dbContext.SaveChangesAsync();
        CariKartBId = cariB.Id;

        dbContext.MuhasebeDonemler.AddRange(
            new MuhasebeDonem { TesisId = TesisAId, MaliYil = 2026, DonemNo = 1, BaslangicTarihi = new DateTime(2020, 1, 1), BitisTarihi = new DateTime(2030, 12, 31), KapaliMi = false },
            new MuhasebeDonem { TesisId = TesisBId, MaliYil = 2026, DonemNo = 1, BaslangicTarihi = new DateTime(2020, 1, 1), BitisTarihi = new DateTime(2030, 12, 31), KapaliMi = false });
        await dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            return;
        }

        await using var dbContext = CreateDbContext();
        if (KurumId <= 0)
        {
            return;
        }

        // MuhasebeFisler'i silmeden ONCE, ona referans veren PosTahsilatValorleri.MuhasebeFisId /
        // TersKayitMuhasebeFisId alanlarini NULL'a cekmeliyiz (FK Restrict) - aksi halde DELETE
        // "REFERENCE constraint" hatasiyla basarisiz olur.
        await dbContext.PosTahsilatValorleri
            .Where(x => x.TesisId == TesisAId || x.TesisId == TesisBId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.MuhasebeFisId, (int?)null)
                .SetProperty(x => x.TersKayitMuhasebeFisId, (int?)null));

        var fisIds = await dbContext.MuhasebeFisler
            .Where(x => x.TesisId == TesisAId || x.TesisId == TesisBId)
            .Select(x => x.Id)
            .ToListAsync();
        if (fisIds.Count > 0)
        {
            // Ters kayit <-> orijinal fis çapraz referanslarini (TersKayitFisId/IptalEdilenFisId)
            // da once temizle - ayni sebep.
            await dbContext.MuhasebeFisler.Where(x => fisIds.Contains(x.Id))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.TersKayitFisId, (int?)null)
                    .SetProperty(x => x.IptalEdilenFisId, (int?)null));
            await dbContext.MuhasebeFisSatirlari.Where(x => fisIds.Contains(x.MuhasebeFisId)).ExecuteDeleteAsync();
            await dbContext.MuhasebeFisler.Where(x => fisIds.Contains(x.Id)).ExecuteDeleteAsync();
        }

        await dbContext.PosTahsilatValorDegisiklikGecmisleri
            .Where(x => x.PosTahsilatValor != null && (x.PosTahsilatValor.TesisId == TesisAId || x.PosTahsilatValor.TesisId == TesisBId))
            .ExecuteDeleteAsync();
        await dbContext.PosTahsilatValorleri
            .Where(x => x.TesisId == TesisAId || x.TesisId == TesisBId)
            .ExecuteDeleteAsync();
        await dbContext.TahsilatOdemeBelgeleri
            .Where(x => x.CariKart != null && (x.CariKart.TesisId == TesisAId || x.CariKart.TesisId == TesisBId))
            .ExecuteDeleteAsync();
        await dbContext.PosValorFisNoSayaclari
            .Where(x => x.TesisId == TesisAId || x.TesisId == TesisBId)
            .ExecuteDeleteAsync();
        await dbContext.MuhasebeHesapBakiyeleri
            .Where(x => x.TesisId == TesisAId || x.TesisId == TesisBId)
            .ExecuteDeleteAsync();
        await dbContext.CariKartlar
            .Where(x => x.TesisId == TesisAId || x.TesisId == TesisBId)
            .ExecuteDeleteAsync();
        await dbContext.KasaBankaHesaplari
            .Where(x => x.TesisId == TesisAId || x.TesisId == TesisBId)
            .ExecuteDeleteAsync();
        await dbContext.MuhasebeDonemler
            .Where(x => x.TesisId == TesisAId || x.TesisId == TesisBId)
            .ExecuteDeleteAsync();
        await dbContext.MuhasebeHesapPlanlari
            .Where(x => x.Kod != null && x.Kod.StartsWith(_uniqueSuffix))
            .ExecuteDeleteAsync();
        await dbContext.Tesisler
            .Where(x => x.Id == TesisAId || x.Id == TesisBId)
            .ExecuteDeleteAsync();
        await dbContext.Iller
            .Where(x => x.Ad != null && x.Ad.Contains(_uniqueSuffix))
            .ExecuteDeleteAsync();
        await dbContext.Kurumlar
            .Where(x => x.Id == KurumId)
            .ExecuteDeleteAsync();
    }

    // ─────────────────────────────────────────────────────────────
    // Yardimcilar
    // ─────────────────────────────────────────────────────────────

    private static StysAppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new StysAppDbContext(options, new FakeCurrentUserAccessor(), new FakeCurrentTenantAccessor());
    }

    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<MuhasebeDonemProfile>();
            cfg.AddProfile<MuhasebeFisProfile>();
        }, NullLoggerFactory.Instance);
        return config.CreateMapper();
    }

    private static IMuhasebeDonemService CreateMuhasebeDonemService(StysAppDbContext dbContext)
    {
        var mapper = CreateMapper();
        var repo = new MuhasebeDonemRepository(dbContext, mapper);
        return new MuhasebeDonemService(repo, mapper, dbContext, new FakeMuhasebeTesisScopeService());
    }

    private static IMuhasebeFisService CreateMuhasebeFisService(StysAppDbContext dbContext, IUserAccessScopeService userAccessScopeService)
    {
        var mapper = CreateMapper();
        var repo = new MuhasebeFisRepository(dbContext, mapper);
        return new MuhasebeFisService(
            repo, mapper, dbContext,
            CreateMuhasebeDonemService(dbContext),
            new MuhasebeHesapBakiyeGuncellemeService(dbContext),
            userAccessScopeService,
            new FakeDomainOperationLogger());
    }

    private static IPosTahsilatValorAktarimService CreateAktarimService(StysAppDbContext dbContext, IUserAccessScopeService userAccessScopeService)
    {
        return new PosTahsilatValorAktarimService(
            dbContext,
            CreateMuhasebeDonemService(dbContext),
            CreateMuhasebeFisService(dbContext, userAccessScopeService),
            userAccessScopeService,
            NullLogger<PosTahsilatValorAktarimService>.Instance);
    }

    private async Task<int> SeedValorKaydiAsync(
        StysAppDbContext dbContext, int tesisId, int cariKartId, int krediKartiHesapId, int bagliBankaHesapId,
        decimal brut, decimal? komisyonOrani, int? komisyonGiderHesapPlaniId, string uniqueTag)
    {
        var belge = new TahsilatOdemeBelgesi
        {
            BelgeNo = $"{_uniqueSuffix}-{uniqueTag}",
            BelgeTarihi = DateTime.UtcNow.Date,
            BelgeTipi = TahsilatOdemeBelgeTipleri.Tahsilat,
            CariKartId = cariKartId,
            Tutar = brut,
            ParaBirimi = "TRY",
            OdemeYontemi = OdemeYontemleri.KrediKarti,
            KasaBankaHesapId = krediKartiHesapId,
            Durum = TahsilatOdemeBelgeDurumlari.Aktif
        };
        dbContext.TahsilatOdemeBelgeleri.Add(belge);
        await dbContext.SaveChangesAsync();

        var komisyon = komisyonOrani.HasValue ? Math.Round(brut * komisyonOrani.Value / 100m, 2) : 0m;
        var valor = new PosTahsilatValor
        {
            TesisId = tesisId,
            TahsilatOdemeBelgesiId = belge.Id,
            KrediKartiHesapId = krediKartiHesapId,
            BagliBankaHesapId = bagliBankaHesapId,
            KomisyonGiderHesapPlaniId = komisyonGiderHesapPlaniId,
            OdemeTarihi = belge.BelgeTarihi,
            ValorGunSayisi = 0,
            ValorGunTuru = ValorGunTurleri.TakvimGunu,
            BeklenenValorTarihi = DateOnly.FromDateTime(belge.BelgeTarihi),
            OtomatikAktarimMi = false,
            KomisyonOraniSnapshot = komisyonOrani,
            BrutTutar = brut,
            KomisyonTutari = komisyon,
            NetTutar = brut - komisyon,
            ParaBirimi = "TRY",
            Durum = komisyonOrani.HasValue ? PosTahsilatValorDurumlari.ValorBekliyor : PosTahsilatValorDurumlari.ValorBekliyor
        };
        dbContext.PosTahsilatValorleri.Add(valor);
        await dbContext.SaveChangesAsync();
        return valor.Id;
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 1 — Eszamanli iki aktarim, yalnizca biri basarili
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task Senaryo1_EszamanliIkiAktarim_YalnizcaBiriBasarili()
    {
        await using var seedContext = CreateDbContext();
        var valorId = await SeedValorKaydiAsync(seedContext, TesisAId, CariKartAId, KasaBankaPosKomisyonluAId, KasaBankaBankaAId, 1000m, 2m, HesapPlaniKomisyonId, "S1");

        await using var ctx1 = CreateDbContext();
        await using var ctx2 = CreateDbContext();
        var scope = new FakeUserAccessScopeService(DomainAccessScope.Unscoped());
        var svc1 = CreateAktarimService(ctx1, scope);
        var svc2 = CreateAktarimService(ctx2, scope);

        var task1 = TryAktarAsync(svc1, valorId);
        var task2 = TryAktarAsync(svc2, valorId);
        var sonuclar = await Task.WhenAll(task1, task2);

        // Claim'i kaybeden taraf HesabaAktarAsync'in Adim A'sinda dogrudan BaseException(409)
        // firlatir (donus degeri yoktur) - bu yuzden basari sayaci, "hem basarili sonuc hem de
        // basarisiz-ama-beklenen istisna" toplamini degil, yalnizca GERCEKTEN basarili olan
        // tarafi sayar.
        var basariliSayisi = sonuclar.Count(x => x.Basarili);
        Assert.Equal(1, basariliSayisi);

        await using var verifyContext = CreateDbContext();
        var valor = await verifyContext.PosTahsilatValorleri.SingleAsync(x => x.Id == valorId);
        Assert.Equal(PosTahsilatValorDurumlari.Aktarildi, valor.Durum);
        Assert.NotNull(valor.MuhasebeFisId);

        var fisSayisi = await verifyContext.MuhasebeFisler.CountAsync(x =>
            x.KaynakModul == MuhasebeKaynakModulleri.PosTahsilatValorTransferi && x.KaynakId == valorId);
        Assert.Equal(1, fisSayisi);
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 2 — Iki "instance" ayni tesis/mali yil icin fis no uretiyor
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task Senaryo2_IkiInstance_FarkliValorKayitlariIcinFisNoUretimi_CakismaOlmaz()
    {
        await using var seedContext = CreateDbContext();
        var valorId1 = await SeedValorKaydiAsync(seedContext, TesisAId, CariKartAId, KasaBankaPosAId, KasaBankaBankaAId, 500m, 0m, HesapPlaniKomisyonId, "S2-A");
        var valorId2 = await SeedValorKaydiAsync(seedContext, TesisAId, CariKartAId, KasaBankaPosAId, KasaBankaBankaAId, 700m, 0m, HesapPlaniKomisyonId, "S2-B");

        await using var ctx1 = CreateDbContext();
        await using var ctx2 = CreateDbContext();
        var scope = new FakeUserAccessScopeService(DomainAccessScope.Unscoped());
        var svc1 = CreateAktarimService(ctx1, scope);
        var svc2 = CreateAktarimService(ctx2, scope);

        var task1 = svc1.HesabaAktarAsync(valorId1, null, CancellationToken.None);
        var task2 = svc2.HesabaAktarAsync(valorId2, null, CancellationToken.None);
        var sonuclar = await Task.WhenAll(task1, task2);

        Assert.All(sonuclar, x => Assert.True(x.Basarili, x.HataMesaji));

        await using var verifyContext = CreateDbContext();
        var fisNolar = await verifyContext.MuhasebeFisler
            .Where(x => x.TesisId == TesisAId && (x.KaynakId == valorId1 || x.KaynakId == valorId2)
                        && x.KaynakModul == MuhasebeKaynakModulleri.PosTahsilatValorTransferi)
            .Select(x => x.FisNo)
            .ToListAsync();

        Assert.Equal(2, fisNolar.Count);
        Assert.Equal(2, fisNolar.Distinct().Count()); // fis no'lar farkli olmali (cakisma yok)
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 3 — Negatif manuel tutarlar reddedilir
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task Senaryo3_NegatifManuelKomisyon_422DonerVeKayitDegismez()
    {
        await using var seedContext = CreateDbContext();
        var valorId = await SeedValorKaydiAsync(seedContext, TesisAId, CariKartAId, KasaBankaPosKomisyonluAId, KasaBankaBankaAId, 1000m, 2m, HesapPlaniKomisyonId, "S3");

        await using var ctx = CreateDbContext();
        var scope = new FakeUserAccessScopeService(DomainAccessScope.Unscoped());
        var svc = CreateAktarimService(ctx, scope);

        var ex = await Assert.ThrowsAsync<BaseException>(() => svc.HesabaAktarAsync(
            valorId,
            new STYS.Muhasebe.PosTahsilatValorleri.Dtos.ManuelAktarimGuncellemeDto { KomisyonTutari = -10m, Aciklama = "negatif deneme" },
            CancellationToken.None));

        Assert.Equal(422, ex.ErrorCode);

        await using var verifyContext = CreateDbContext();
        var valor = await verifyContext.PosTahsilatValorleri.SingleAsync(x => x.Id == valorId);
        Assert.Equal(PosTahsilatValorDurumlari.ValorBekliyor, valor.Durum);
        Assert.Equal(0, valor.DenemeSayisi);
        Assert.Null(valor.MuhasebeFisId);
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 4 — Net > Brut manuel girisi reddedilir
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task Senaryo4_NetBruttenBuyuk_422DonerVeKayitDegismez()
    {
        await using var seedContext = CreateDbContext();
        var valorId = await SeedValorKaydiAsync(seedContext, TesisAId, CariKartAId, KasaBankaPosKomisyonluAId, KasaBankaBankaAId, 1000m, 2m, HesapPlaniKomisyonId, "S4");

        await using var ctx = CreateDbContext();
        var scope = new FakeUserAccessScopeService(DomainAccessScope.Unscoped());
        var svc = CreateAktarimService(ctx, scope);

        var ex = await Assert.ThrowsAsync<BaseException>(() => svc.HesabaAktarAsync(
            valorId,
            new STYS.Muhasebe.PosTahsilatValorleri.Dtos.ManuelAktarimGuncellemeDto { NetTutar = 1500m, Aciklama = "net > brut" },
            CancellationToken.None));

        Assert.Equal(422, ex.ErrorCode);

        await using var verifyContext = CreateDbContext();
        var valor = await verifyContext.PosTahsilatValorleri.SingleAsync(x => x.Id == valorId);
        Assert.Equal(PosTahsilatValorDurumlari.ValorBekliyor, valor.Durum);
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 5 — Farkli tesise ait kayda erisim 403 ile reddedilir
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task Senaryo5_FarkliTesisIdErisimi_403DonerVeKayitDegismez()
    {
        await using var seedContext = CreateDbContext();
        var valorIdTesisB = await SeedValorKaydiAsync(seedContext, TesisBId, CariKartBId, TesisBPosId, TesisBBankaId, 300m, 0m, null, "S5");

        await using var ctx = CreateDbContext();
        // Kullanici yalnizca TesisA'ya yetkili - TesisB'ye ait kayda erismeye calisiyor.
        var scope = new FakeUserAccessScopeService(DomainAccessScope.Scoped([], [TesisAId], []));
        var svc = CreateAktarimService(ctx, scope);

        var ex = await Assert.ThrowsAsync<BaseException>(() => svc.HesabaAktarAsync(valorIdTesisB, null, CancellationToken.None));
        Assert.Equal(403, ex.ErrorCode);

        await using var verifyContext = CreateDbContext();
        var valor = await verifyContext.PosTahsilatValorleri.SingleAsync(x => x.Id == valorIdTesisB);
        Assert.Equal(PosTahsilatValorDurumlari.ValorBekliyor, valor.Durum);
        Assert.Null(valor.ClaimToken);
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 6 — Transfer fisinin onay/ters-kayit yasam dongusu
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task Senaryo6_TransferFisi_HemenOnaylanirVeDuzeltmeSonrasiTersKayitOlusur()
    {
        await using var seedContext = CreateDbContext();
        var valorId = await SeedValorKaydiAsync(seedContext, TesisAId, CariKartAId, KasaBankaPosKomisyonluAId, KasaBankaBankaAId, 1000m, 2m, HesapPlaniKomisyonId, "S6");

        var scope = new FakeUserAccessScopeService(DomainAccessScope.Unscoped());

        await using (var ctx = CreateDbContext())
        {
            var svc = CreateAktarimService(ctx, scope);
            var sonuc = await svc.HesabaAktarAsync(valorId, null, CancellationToken.None);
            Assert.True(sonuc.Basarili, sonuc.HataMesaji);
        }

        await using (var verifyContext = CreateDbContext())
        {
            var valor = await verifyContext.PosTahsilatValorleri.SingleAsync(x => x.Id == valorId);
            Assert.Equal(PosTahsilatValorDurumlari.Aktarildi, valor.Durum);

            var fis = await verifyContext.MuhasebeFisler.Include(x => x.Satirlar).SingleAsync(x => x.Id == valor.MuhasebeFisId);
            // Duzeltme #1: fis Taslak birakilmaz, ayni transaction icinde onaylanir.
            Assert.Equal(MuhasebeFisDurumlari.Onayli, fis.Durum);
            Assert.NotNull(fis.YevmiyeNo);
            Assert.Equal(3, fis.Satirlar.Count(s => !s.IsDeleted)); // banka + komisyon + POS
            Assert.Equal(fis.ToplamBorc, fis.ToplamAlacak);
        }

        // Duzeltme-ters-kayit
        await using (var ctx = CreateDbContext())
        {
            var svc = CreateAktarimService(ctx, scope);
            var duzeltmeSonuc = await svc.DuzeltmeTersKayitAsync(valorId, "test - yanlislikla aktarildi", CancellationToken.None);
            Assert.True(duzeltmeSonuc.Basarili, duzeltmeSonuc.HataMesaji);
        }

        await using (var verifyContext = CreateDbContext())
        {
            var valor = await verifyContext.PosTahsilatValorleri.SingleAsync(x => x.Id == valorId);
            Assert.Equal(PosTahsilatValorDurumlari.AktarimFisiIptalEdildi, valor.Durum);
            Assert.NotNull(valor.TersKayitMuhasebeFisId);

            var orijinalFis = await verifyContext.MuhasebeFisler.SingleAsync(x => x.Id == valor.MuhasebeFisId);
            Assert.Equal(MuhasebeFisDurumlari.Iptal, orijinalFis.Durum);
            Assert.Equal(valor.TersKayitMuhasebeFisId, orijinalFis.TersKayitFisId);

            var tersFis = await verifyContext.MuhasebeFisler.SingleAsync(x => x.Id == valor.TersKayitMuhasebeFisId);
            Assert.Equal(MuhasebeFisDurumlari.TersKayit, tersFis.Durum);
            Assert.Equal(orijinalFis.Id, tersFis.IptalEdilenFisId);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 7 — Ayni Aktarildi kayda iki duzeltme-ters-kayit istegi
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task Senaryo7_IkiDuzeltmeTersKayitIstegi_YalnizcaBirTersKayitUretilir()
    {
        await using var seedContext = CreateDbContext();
        var valorId = await SeedValorKaydiAsync(seedContext, TesisAId, CariKartAId, KasaBankaPosAId, KasaBankaBankaAId, 400m, 0m, HesapPlaniKomisyonId, "S7");

        var scope = new FakeUserAccessScopeService(DomainAccessScope.Unscoped());

        await using (var ctx = CreateDbContext())
        {
            var svc = CreateAktarimService(ctx, scope);
            var sonuc = await svc.HesabaAktarAsync(valorId, null, CancellationToken.None);
            Assert.True(sonuc.Basarili, sonuc.HataMesaji);
        }

        await using var ctx1 = CreateDbContext();
        await using var ctx2 = CreateDbContext();
        var svc1 = CreateAktarimService(ctx1, scope);
        var svc2 = CreateAktarimService(ctx2, scope);

        var task1 = SafeDuzeltmeAsync(svc1, valorId, "ilk deneme");
        var task2 = SafeDuzeltmeAsync(svc2, valorId, "ikinci deneme");
        await Task.WhenAll(task1, task2);

        await using var verifyContext = CreateDbContext();
        var valor = await verifyContext.PosTahsilatValorleri.SingleAsync(x => x.Id == valorId);
        Assert.Equal(PosTahsilatValorDurumlari.AktarimFisiIptalEdildi, valor.Durum);

        var tersKayitSayisi = await verifyContext.MuhasebeFisler
            .CountAsync(x => x.IptalEdilenFisId == valor.MuhasebeFisId);
        Assert.Equal(1, tersKayitSayisi);
    }

    private static async Task SafeDuzeltmeAsync(IPosTahsilatValorAktarimService svc, int valorId, string aciklama)
    {
        try
        {
            await svc.DuzeltmeTersKayitAsync(valorId, aciklama, CancellationToken.None);
        }
        catch (BaseException)
        {
            // Yariş kaybeden istek icin beklenen (409) - test yalnizca tek ters kaydin
            // olustugunu dogrular, her iki cagrinin da basarili olmasini beklemez.
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 8 — Aktarim ile tahsilat iptalinin yarismasi (tutarsizlik olusmamali)
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task Senaryo8_AktarimIleTahsilatIptaliYarisi_TutarliSonlanir()
    {
        await using var seedContext = CreateDbContext();
        var valorId = await SeedValorKaydiAsync(seedContext, TesisAId, CariKartAId, KasaBankaPosAId, KasaBankaBankaAId, 250m, 0m, HesapPlaniKomisyonId, "S8");
        var belgeId = await seedContext.PosTahsilatValorleri.Where(x => x.Id == valorId).Select(x => x.TahsilatOdemeBelgesiId).SingleAsync();

        var scope = new FakeUserAccessScopeService(DomainAccessScope.Unscoped());

        await using var ctx1 = CreateDbContext();
        await using var ctx2 = CreateDbContext();
        var aktarimSvc = CreateAktarimService(ctx1, scope);
        var snapshotSvc = new PosTahsilatValorSnapshotService(
            ctx2,
            new ValorTarihHesaplamaService(new NoOpResmiTatilGunuProvider()),
            CreateMuhasebeFisService(ctx2, scope));

        var aktarimTask = SafeAktarAsync(aktarimSvc, valorId);
        var iptalTask = SafeIptalAsync(snapshotSvc, belgeId);
        await Task.WhenAll(aktarimTask, iptalTask);

        await using var verifyContext = CreateDbContext();
        var valor = await verifyContext.PosTahsilatValorleri.SingleAsync(x => x.Id == valorId);

        // Tutarlilik: kayit ya basariyla aktarilmis (Aktarildi) ya da iptal edilmis (Iptal) olmali;
        // hicbir zaman "Aktariliyor"da takili kalmamali ve ClaimToken bos olmali.
        Assert.Contains(valor.Durum, new[] { PosTahsilatValorDurumlari.Aktarildi, PosTahsilatValorDurumlari.Iptal });
        Assert.Null(valor.ClaimToken);

        if (valor.Durum == PosTahsilatValorDurumlari.Aktarildi)
        {
            Assert.NotNull(valor.MuhasebeFisId);
            var fisSayisi = await verifyContext.MuhasebeFisler.CountAsync(x =>
                x.KaynakModul == MuhasebeKaynakModulleri.PosTahsilatValorTransferi && x.KaynakId == valorId);
            Assert.Equal(1, fisSayisi);
        }
    }

    private static async Task SafeAktarAsync(IPosTahsilatValorAktarimService svc, int valorId)
    {
        try { await svc.HesabaAktarAsync(valorId, null, CancellationToken.None); }
        catch (BaseException) { /* yarisi kaybedebilir, beklenen */ }
    }

    /// <summary>HesabaAktarAsync, claim asamasinda kaybeden taraf icin bir sonuc DTO'su DEGIL,
    /// dogrudan BaseException firlatir (Adim A'da "uygun" degilse hemen throw). Bu yardimci,
    /// eszamanlilik testlerinde her iki tarafi da tekdüze bir sonuca (Basarili/HataMesaji)
    /// indirger.</summary>
    private static async Task<STYS.Muhasebe.PosTahsilatValorleri.Dtos.PosTahsilatValorAktarimSonucDto> TryAktarAsync(IPosTahsilatValorAktarimService svc, int valorId)
    {
        try
        {
            return await svc.HesabaAktarAsync(valorId, null, CancellationToken.None);
        }
        catch (BaseException ex)
        {
            return new STYS.Muhasebe.PosTahsilatValorleri.Dtos.PosTahsilatValorAktarimSonucDto { Id = valorId, Basarili = false, HataMesaji = ex.Message };
        }
    }

    private static async Task SafeIptalAsync(IPosTahsilatValorSnapshotService svc, int belgeId)
    {
        try { await svc.IptalEtAsync(belgeId, CancellationToken.None); }
        catch (BaseException) { /* yarisi kaybedebilir, beklenen */ }
    }

    // ─────────────────────────────────────────────────────────────
    // Fake'ler
    // ─────────────────────────────────────────────────────────────

    private sealed class FakeCurrentUserAccessor : TOD.Platform.Security.Auth.Services.ICurrentUserAccessor
    {
        public string? GetCurrentUserName() => "pos-valor-integration-test";
        public Guid? GetCurrentUserId() => Guid.NewGuid();
    }

    private sealed class FakeCurrentTenantAccessor : TOD.Platform.Security.Auth.Services.ICurrentTenantAccessor
    {
        public int? GetCurrentKurumId() => null;
        public IReadOnlyList<int> GetAccessibleKurumIds() => [];
        public bool IsSuperAdmin() => true;
        public bool IsKurumAdmin() => false;
    }

    private sealed class FakeUserAccessScopeService : IUserAccessScopeService
    {
        private readonly DomainAccessScope _scope;
        public FakeUserAccessScopeService(DomainAccessScope scope) => _scope = scope;
        public Task<DomainAccessScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_scope);
    }

    private sealed class FakeMuhasebeTesisScopeService : STYS.Muhasebe.Common.Services.IMuhasebeTesisScopeService
    {
        public Task<int[]> GetEffectiveTesisIdsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<int>());
        public Task<int[]> GetEffectiveTesisIdsAsync(DomainAccessScope scope, CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<int>());
        public Task EnsureCanAccessTesisAsync(int tesisId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeDomainOperationLogger : IDomainOperationLogger
    {
        public void Started(string eventName, object payload) { }
        public void Completed(string eventName, object payload) { }
        public void Warning(string eventName, object payload) { }
        public void Failed(string eventName, Exception exception, object payload) { }
    }
}
