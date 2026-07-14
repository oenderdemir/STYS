using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.CariHareketler.Mapping;
using STYS.Muhasebe.CariHareketler.Repositories;
using STYS.Muhasebe.CariHareketler.Services;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.CariKartlar.Mapping;
using STYS.Muhasebe.CariKartlar.Repositories;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Muhasebe.MuhasebeDonemleri.Entities;
using STYS.Muhasebe.MuhasebeDonemleri.Mapping;
using STYS.Muhasebe.MuhasebeDonemleri.Repositories;
using STYS.Muhasebe.MuhasebeDonemleri.Services;
using STYS.Muhasebe.MuhasebeFisleri.Dtos;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using STYS.Muhasebe.MuhasebeFisleri.Services;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Mapping;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Repositories;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Services;
using STYS.Rezervasyonlar;
using STYS.Rezervasyonlar.Dto;
using STYS.Rezervasyonlar.Entities;
using STYS.Rezervasyonlar.Services;
using STYS.Kurumlar.Entities;
using STYS.Iller.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.AspNetCore.Logging;
using TOD.Platform.Licensing.Abstractions;
using TOD.Platform.Persistence.Rdbms.Dto;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Tests;

/// <summary>
/// [Fact] yerine kullanilir: STYS_INTEGRATION_TEST_CONNECTION_STRING ortam degiskeni tanimli
/// degilse testi calistirmadan "Skipped" olarak isaretler. Boylece normal "dotnet test" akisi
/// yerel/gercek bir SQL Server'a bagimli olmaz; entegrasyon testleri sadece bu degisken acikca
/// verildiginde calisir.
/// </summary>
public sealed class IntegrationFactAttribute : FactAttribute
{
    public const string ConnectionStringEnvVar = "STYS_INTEGRATION_TEST_CONNECTION_STRING";

    public IntegrationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionStringEnvVar)))
        {
            Skip = $"{ConnectionStringEnvVar} ortam degiskeni tanimli degil — entegrasyon testi atlandi. " +
                   "Calistirmak icin: STYS_INTEGRATION_TEST_CONNECTION_STRING=\"...\" dotnet test --filter Category=Integration";
        }
    }
}

/// <summary>
/// Rezervasyon odeme -> muhasebe tahsilat entegrasyonunu GERCEK SQL Server test veritabanina karsi
/// dogrulayan entegrasyon testi. Mevcut proje konvansiyonu (RezervasyonServiceTests, TesisServiceTests)
/// InMemory provider kullaniyor; ancak bu senaryolarin cogu (unique index, FK, savepoint/transaction
/// davranisi) InMemory tarafindan desteklenmedigi icin burada gercek SQL Server kullanilir.
///
/// Calistirma:
///   Normal test       : dotnet test  (bu sinif STYS_INTEGRATION_TEST_CONNECTION_STRING tanimli
///                        degilse otomatik atlanir — yerel SQL Server GEREKMEZ)
///   Entegrasyon testi  : STYS_INTEGRATION_TEST_CONNECTION_STRING="Server=...;Database=...;User Id=...;Password=...;"
///                        dotnet test --filter Category=Integration
///
/// Baglanti dizesi ornegi icin bkz. docs/rezervasyon-odeme-muhasebe-entegrasyonu-bulgular.md.
/// Gercek/hard-coded bir sifre burada TUTULMAZ.
///
/// Not: Test verileri her calistirmada benzersiz (Guid tabanli) degerlerle seed edilir ve
/// DisposeAsync icinde temizlenir.
/// </summary>
[Trait("Category", "Integration")]
public class RezervasyonOdemeMuhasebeIntegrationTests : IAsyncLifetime
{
    private static readonly string? ConnectionString =
        Environment.GetEnvironmentVariable(IntegrationFactAttribute.ConnectionStringEnvVar);

    private const string TestMarker = "IKT-970"; // Kod/Ad alanlarina konan ayirt edici imza — temizlik bu izle yapilir.

    // Tumu IDENTITY kolon oldugundan explicit Id verilmez; InitializeAsync icinde uretilen
    // degerlerle doldurulur (instance alan — xUnit her [Fact] icin yeni sinif ornegi kullanir).
    private int KurumId;
    private int TesisCariId;      // RezervasyonTahsilatAlacakHesapTipi = Cari
    private int TesisAvansId;     // RezervasyonTahsilatAlacakHesapTipi = AlinanAvans

    private int HesapPlaniKasaId;
    private int HesapPlaniBankaId;
    private int HesapPlaniCariAId;

    private int KasaBankaNakitAId;   // TesisCari, NakitKasa
    private int KasaBankaBankaAId;   // TesisCari, Banka (tip uyumsuzluk testi icin)
    private int KasaBankaNakitBId;   // TesisAvans, NakitKasa

    private int CariKartWithHesapAId; // TesisCari, MuhasebeHesapPlaniId dolu
    private int CariKartNoHesapAId;   // TesisCari, MuhasebeHesapPlaniId bos
    private int CariKartNoHesapBId;   // TesisAvans, MuhasebeHesapPlaniId bos

    public async Task InitializeAsync()
    {
        // Savunma amacli ikinci kontrol: IntegrationFactAttribute testi zaten Skip eder, ama xUnit
        // surumleri arasinda IAsyncLifetime cagrilma zamanlamasi farklilasabilir — baglanti dizesi
        // yoksa burada da hicbir DB islemi yapilmadan sessizce cikilir.
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            return;
        }

        await using var dbContext = CreateDbContext();

        // Testler paralel calisabildigi icin (xUnit varsayilani) benzersiz bir suffix kullanilir —
        // Il.Ad ve Kurumlar.Kod alanlarinda unique index oldugundan sabit degerler carpisir.
        var uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        var kurum = new Kurum { Kod = uniqueSuffix, Ad = "Test Kurum " + uniqueSuffix, AktifMi = true };
        dbContext.Kurumlar.Add(kurum);
        var il = new Il { Ad = "Test Il " + uniqueSuffix, AktifMi = true };
        dbContext.Iller.Add(il);
        await dbContext.SaveChangesAsync();
        KurumId = kurum.Id;

        var tesisCari = new Tesis
        {
            KurumId = kurum.Id,
            IlId = il.Id,
            Ad = "Test Tesis Cari " + uniqueSuffix,
            Telefon = "0000",
            Adres = "Test Adres",
            AktifMi = true,
            RezervasyonTahsilatAlacakHesapTipi = RezervasyonTahsilatAlacakHesapTipleri.Cari
        };
        var tesisAvans = new Tesis
        {
            KurumId = kurum.Id,
            IlId = il.Id,
            Ad = "Test Tesis Avans " + uniqueSuffix,
            Telefon = "0000",
            Adres = "Test Adres",
            AktifMi = true,
            RezervasyonTahsilatAlacakHesapTipi = RezervasyonTahsilatAlacakHesapTipleri.AlinanAvans
        };
        dbContext.Tesisler.AddRange(tesisCari, tesisAvans);
        await dbContext.SaveChangesAsync();
        TesisCariId = tesisCari.Id;
        TesisAvansId = tesisAvans.Id;

        var hesapKasa = new MuhasebeHesapPlani { Kod = uniqueSuffix + "-KASA", TamKod = uniqueSuffix + ".KASA", Ad = "Test Kasa", AktifMi = true, DetayHesapMi = true, HareketGorebilirMi = true, HesapTipi = HesapTipi.DetayHesap };
        var hesapBanka = new MuhasebeHesapPlani { Kod = uniqueSuffix + "-BANKA", TamKod = uniqueSuffix + ".BANKA", Ad = "Test Banka", AktifMi = true, DetayHesapMi = true, HareketGorebilirMi = true, HesapTipi = HesapTipi.DetayHesap };
        var hesapCariA = new MuhasebeHesapPlani { Kod = uniqueSuffix + "-CARI", TamKod = uniqueSuffix + ".CARI", Ad = "Test Cari", AktifMi = true, DetayHesapMi = true, HareketGorebilirMi = true, HesapTipi = HesapTipi.DetayHesap };
        dbContext.MuhasebeHesapPlanlari.AddRange(hesapKasa, hesapBanka, hesapCariA);
        await dbContext.SaveChangesAsync();
        HesapPlaniKasaId = hesapKasa.Id;
        HesapPlaniBankaId = hesapBanka.Id;
        HesapPlaniCariAId = hesapCariA.Id;

        var kasaNakitA = new KasaBankaHesap { TesisId = TesisCariId, Tip = KasaBankaHesapTipleri.NakitKasa, Kod = uniqueSuffix + "-NKT-A", Ad = "Test Nakit Kasa A", ParaBirimi = "TRY", AktifMi = true, MuhasebeHesapPlaniId = HesapPlaniKasaId };
        var kasaBankaA = new KasaBankaHesap { TesisId = TesisCariId, Tip = KasaBankaHesapTipleri.Banka, Kod = uniqueSuffix + "-BNK-A", Ad = "Test Banka A", ParaBirimi = "TRY", AktifMi = true, MuhasebeHesapPlaniId = HesapPlaniBankaId };
        var kasaNakitB = new KasaBankaHesap { TesisId = TesisAvansId, Tip = KasaBankaHesapTipleri.NakitKasa, Kod = uniqueSuffix + "-NKT-B", Ad = "Test Nakit Kasa B", ParaBirimi = "TRY", AktifMi = true, MuhasebeHesapPlaniId = HesapPlaniKasaId };
        dbContext.KasaBankaHesaplari.AddRange(kasaNakitA, kasaBankaA, kasaNakitB);
        await dbContext.SaveChangesAsync();
        KasaBankaNakitAId = kasaNakitA.Id;
        KasaBankaBankaAId = kasaBankaA.Id;
        KasaBankaNakitBId = kasaNakitB.Id;

        var cariWithHesapA = new CariKart { TesisId = TesisCariId, CariTipi = CariKartTipleri.Musteri, CariKodu = uniqueSuffix + "-A1", UnvanAdSoyad = "Test Musteri A1", AktifMi = true, MuhasebeHesapPlaniId = HesapPlaniCariAId };
        var cariNoHesapA = new CariKart { TesisId = TesisCariId, CariTipi = CariKartTipleri.Musteri, CariKodu = uniqueSuffix + "-A2", UnvanAdSoyad = "Test Musteri A2 Hesapsiz", AktifMi = true, MuhasebeHesapPlaniId = null };
        var cariNoHesapB = new CariKart { TesisId = TesisAvansId, CariTipi = CariKartTipleri.Musteri, CariKodu = uniqueSuffix + "-B1", UnvanAdSoyad = "Test Musteri B1 Hesapsiz", AktifMi = true, MuhasebeHesapPlaniId = null };
        dbContext.CariKartlar.AddRange(cariWithHesapA, cariNoHesapA, cariNoHesapB);
        await dbContext.SaveChangesAsync();
        CariKartWithHesapAId = cariWithHesapA.Id;
        CariKartNoHesapAId = cariNoHesapA.Id;
        CariKartNoHesapBId = cariNoHesapB.Id;

        dbContext.MuhasebeDonemler.AddRange(
            new MuhasebeDonem { TesisId = TesisCariId, MaliYil = 2026, DonemNo = 1, BaslangicTarihi = new DateTime(2020, 1, 1), BitisTarihi = new DateTime(2030, 12, 31), KapaliMi = false },
            new MuhasebeDonem { TesisId = TesisAvansId, MaliYil = 2026, DonemNo = 1, BaslangicTarihi = new DateTime(2020, 1, 1), BitisTarihi = new DateTime(2030, 12, 31), KapaliMi = false });
        await dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            return;
        }

        await using var dbContext = CreateDbContext();
        await CleanupAsync(dbContext, TesisCariId, TesisAvansId, KurumId);
    }

    private static async Task CleanupAsync(StysAppDbContext dbContext, int tesisCariId, int tesisAvansId, int kurumId)
    {
        if (kurumId <= 0)
        {
            return; // InitializeAsync tamamlanmadan basarisiz oldu — silinecek bir sey yok.
        }

        // ExecuteDeleteAsync: tek SQL DELETE'e cevrilir, navigasyon/degisiklik izleme sorunlarina
        // (severed relationship vb.) girmez — RemoveRange + SaveChanges yerine tercih edildi.
        // Sirali silme: FK bagimliligina gore tersten.
        await dbContext.RezervasyonDegisiklikGecmisleri
            .Where(x => x.Rezervasyon != null && (x.Rezervasyon.TesisId == tesisCariId || x.Rezervasyon.TesisId == tesisAvansId))
            .ExecuteDeleteAsync();
        await dbContext.RezervasyonOdemeler
            .Where(x => x.Rezervasyon != null && (x.Rezervasyon.TesisId == tesisCariId || x.Rezervasyon.TesisId == tesisAvansId))
            .ExecuteDeleteAsync();
        await dbContext.Rezervasyonlar
            .Where(x => x.TesisId == tesisCariId || x.TesisId == tesisAvansId)
            .ExecuteDeleteAsync();
        await dbContext.CariHareketler
            .Where(x => x.CariKart != null && x.CariKart.TesisId == tesisCariId)
            .ExecuteDeleteAsync();
        await dbContext.TahsilatOdemeBelgeleri
            .Where(x => x.CariKart != null && (x.CariKart.TesisId == tesisCariId || x.CariKart.TesisId == tesisAvansId))
            .ExecuteDeleteAsync();
        await dbContext.CariKartlar
            .Where(x => x.TesisId == tesisCariId || x.TesisId == tesisAvansId)
            .ExecuteDeleteAsync();
        await dbContext.KasaBankaHesaplari
            .Where(x => x.TesisId == tesisCariId || x.TesisId == tesisAvansId)
            .ExecuteDeleteAsync();
        await dbContext.MuhasebeDonemler
            .Where(x => x.TesisId == tesisCariId || x.TesisId == tesisAvansId)
            .ExecuteDeleteAsync();
        await dbContext.MuhasebeHesapPlanlari
            .Where(x => x.Kod != null && x.Kod.StartsWith(TestMarker))
            .ExecuteDeleteAsync();
        await dbContext.Tesisler
            .Where(x => x.Id == tesisCariId || x.Id == tesisAvansId)
            .ExecuteDeleteAsync();
        await dbContext.Iller
            .Where(x => x.Ad != null && x.Ad.Contains(TestMarker))
            .ExecuteDeleteAsync();
        await dbContext.Kurumlar
            .Where(x => x.Id == kurumId)
            .ExecuteDeleteAsync();
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 1 — Normal nakit odeme
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task Senaryo1_NormalNakitOdeme_TahsilatOdemeBelgesiOlusur()
    {
        await using var dbContext = CreateDbContext();
        var rezervasyon = await SeedRezervasyonAsync(dbContext, TesisCariId, "Senaryo1 Misafir", "555-0001");
        var rezervasyonId = rezervasyon.Id;
        var service = CreateRezervasyonService(dbContext);

        var ozet = await service.KaydetOdemeAsync(rezervasyonId, new RezervasyonOdemeKaydetRequestDto
        {
            OdemeTutari = 100m,
            OdemeTipi = OdemeTipleri.Nakit,
            KasaBankaHesapId = KasaBankaNakitAId,
            CariKartId = CariKartWithHesapAId
        });

        Assert.Equal(100m, ozet.OdenenTutar);

        var odeme = await dbContext.RezervasyonOdemeler.SingleAsync(x => x.RezervasyonId == rezervasyonId);
        Assert.NotNull(odeme.TahsilatOdemeBelgesiId);

        var belge = await dbContext.TahsilatOdemeBelgeleri.SingleAsync(x => x.Id == odeme.TahsilatOdemeBelgesiId);
        Assert.Equal(MuhasebeKaynakModulleri.Rezervasyon, belge.KaynakModul);
        Assert.Equal(odeme.Id, belge.KaynakId);
        Assert.Equal(TahsilatOdemeBelgeTipleri.Tahsilat, belge.BelgeTipi);
        Assert.Null(belge.MuhasebeFisId); // gelir fisi otomatik olusmamali
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 2 — Kasa/Banka hesabi bos
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task Senaryo2_KasaBankaHesabiBos_400DonerVeHicbirSeyOlusmaz()
    {
        await using var dbContext = CreateDbContext();
        var rezervasyon = await SeedRezervasyonAsync(dbContext, TesisCariId, "Senaryo2 Misafir", "555-0002");
        var rezervasyonId = rezervasyon.Id;
        var service = CreateRezervasyonService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.KaydetOdemeAsync(rezervasyonId, new RezervasyonOdemeKaydetRequestDto
        {
            OdemeTutari = 100m,
            OdemeTipi = OdemeTipleri.Nakit,
            KasaBankaHesapId = null,
            CariKartId = CariKartWithHesapAId
        }));

        Assert.Equal(400, ex.ErrorCode);

        await using var verifyContext = CreateDbContext();
        Assert.False(await verifyContext.RezervasyonOdemeler.AnyAsync(x => x.RezervasyonId == rezervasyonId));
        Assert.False(await verifyContext.TahsilatOdemeBelgeleri.AnyAsync(x => x.KaynakModul == MuhasebeKaynakModulleri.Rezervasyon));
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 3 — Cari kart bulunamiyor
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task Senaryo3_CariKartBulunamiyor_422DonerVeHicbirSeyOlusmaz()
    {
        await using var dbContext = CreateDbContext();
        // TCKN/telefon eslesmeyecek, tesisin varsayilan cari karti yok (TesisCariId icin ayarlanmadi), request'te CariKartId yok.
        var rezervasyon = await SeedRezervasyonAsync(dbContext, TesisCariId, "Eslesmeyen Misafir", "555-9999");
        var rezervasyonId = rezervasyon.Id;
        var service = CreateRezervasyonService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.KaydetOdemeAsync(rezervasyonId, new RezervasyonOdemeKaydetRequestDto
        {
            OdemeTutari = 100m,
            OdemeTipi = OdemeTipleri.Nakit,
            KasaBankaHesapId = KasaBankaNakitAId,
            CariKartId = null
        }));

        Assert.Equal(422, ex.ErrorCode);

        await using var verifyContext = CreateDbContext();
        Assert.False(await verifyContext.RezervasyonOdemeler.AnyAsync(x => x.RezervasyonId == rezervasyonId));
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 4 — Alinan Avans modu (CariKart.MuhasebeHesapPlaniId bos olabilir)
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task Senaryo4_AlinanAvansModu_HesapPlaniBosOlsaDaOdemeEngellenmez()
    {
        await using var dbContext = CreateDbContext();
        var rezervasyon = await SeedRezervasyonAsync(dbContext, TesisAvansId, "Senaryo4 Misafir", "555-0004");
        var rezervasyonId = rezervasyon.Id;
        var service = CreateRezervasyonService(dbContext);

        var ozet = await service.KaydetOdemeAsync(rezervasyonId, new RezervasyonOdemeKaydetRequestDto
        {
            OdemeTutari = 100m,
            OdemeTipi = OdemeTipleri.Nakit,
            KasaBankaHesapId = KasaBankaNakitBId,
            CariKartId = CariKartNoHesapBId // MuhasebeHesapPlaniId = null
        });

        Assert.Equal(100m, ozet.OdenenTutar);

        var odeme = await dbContext.RezervasyonOdemeler.SingleAsync(x => x.RezervasyonId == rezervasyonId);
        Assert.NotNull(odeme.TahsilatOdemeBelgesiId);
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 5 — Cari modu, cari kartin hesap plani bos -> hata beklenir
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task Senaryo5_CariModuHesapPlaniBos_HataDoner()
    {
        await using var dbContext = CreateDbContext();
        var rezervasyon = await SeedRezervasyonAsync(dbContext, TesisCariId, "Senaryo5 Misafir", "555-0005");
        var rezervasyonId = rezervasyon.Id;
        var service = CreateRezervasyonService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.KaydetOdemeAsync(rezervasyonId, new RezervasyonOdemeKaydetRequestDto
        {
            OdemeTutari = 100m,
            OdemeTipi = OdemeTipleri.Nakit,
            KasaBankaHesapId = KasaBankaNakitAId,
            CariKartId = CariKartNoHesapAId // MuhasebeHesapPlaniId = null, Tesis=Cari modu
        }));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Contains("muhasebe hesap plan", ex.Message, StringComparison.OrdinalIgnoreCase);

        await using var verifyContext = CreateDbContext();
        Assert.False(await verifyContext.RezervasyonOdemeler.AnyAsync(x => x.RezervasyonId == rezervasyonId));
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 6 — Odeme iptali
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task Senaryo6_OdemeIptali_DurumIptalOlurVeBakiyeArtar()
    {
        await using var dbContext = CreateDbContext();
        var rezervasyon = await SeedRezervasyonAsync(dbContext, TesisCariId, "Senaryo6 Misafir", "555-0006");
        var rezervasyonId = rezervasyon.Id;
        var service = CreateRezervasyonService(dbContext);

        var ozetOnce = await service.KaydetOdemeAsync(rezervasyonId, new RezervasyonOdemeKaydetRequestDto
        {
            OdemeTutari = 100m,
            OdemeTipi = OdemeTipleri.Nakit,
            KasaBankaHesapId = KasaBankaNakitAId,
            CariKartId = CariKartWithHesapAId
        });
        Assert.Equal(100m, ozetOnce.OdenenTutar);

        var odemeId = (await dbContext.RezervasyonOdemeler.SingleAsync(x => x.RezervasyonId == rezervasyonId)).Id;

        // Nested transaction hatasi olmadan calismali (Fix #1).
        var ozetSonra = await service.IptalOdemeAsync(rezervasyonId, odemeId, new RezervasyonOdemeIptalRequestDto { Aciklama = "Test iptali" });

        Assert.Equal(0m, ozetSonra.OdenenTutar);

        await using var verifyContext = CreateDbContext();
        var odeme = await verifyContext.RezervasyonOdemeler.SingleAsync(x => x.Id == odemeId);
        Assert.Equal(RezervasyonOdemeDurumlari.Iptal, odeme.Durum);
        Assert.NotNull(odeme.IptalTarihi);

        var belge = await verifyContext.TahsilatOdemeBelgeleri.SingleAsync(x => x.Id == odeme.TahsilatOdemeBelgesiId);
        Assert.Equal(TahsilatOdemeBelgeDurumlari.Iptal, belge.Durum);
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 7 — Duplicate kontrol (ayni RezervasyonOdeme.Id icin ikinci belge)
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task Senaryo7_AyniOdemeIcinIkinciBelgeEngellenir()
    {
        await using var dbContext = CreateDbContext();
        var rezervasyon = await SeedRezervasyonAsync(dbContext, TesisCariId, "Senaryo7 Misafir", "555-0007");
        var rezervasyonId = rezervasyon.Id;
        var service = CreateRezervasyonService(dbContext);

        await service.KaydetOdemeAsync(rezervasyonId, new RezervasyonOdemeKaydetRequestDto
        {
            OdemeTutari = 100m,
            OdemeTipi = OdemeTipleri.Nakit,
            KasaBankaHesapId = KasaBankaNakitAId,
            CariKartId = CariKartWithHesapAId
        });

        var odeme = await dbContext.RezervasyonOdemeler.SingleAsync(x => x.RezervasyonId == rezervasyonId);
        var rezervasyonEntity = await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == rezervasyonId);
        var muhasebeService = CreateRezervasyonOdemeMuhasebeService(dbContext);

        // Ayni RezervasyonOdeme icin ikinci kez TahsilatOlusturAsync cagirmayi dene (uygulama seviyesi dedup).
        var ex = await Assert.ThrowsAsync<BaseException>(() => muhasebeService.TahsilatOlusturAsync(
            rezervasyonEntity, odeme, KasaBankaNakitAId, CariKartWithHesapAId));

        Assert.Equal(409, ex.ErrorCode);

        await using var verifyContext = CreateDbContext();
        var belgeSayisi = await verifyContext.TahsilatOdemeBelgeleri.CountAsync(x =>
            x.KaynakModul == MuhasebeKaynakModulleri.Rezervasyon && x.KaynakId == odeme.Id);
        Assert.Equal(1, belgeSayisi);
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 8 — Kasa/Banka hesabi tipi uyumsuz
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task Senaryo8_KasaBankaTipiUyumsuz_400Doner()
    {
        await using var dbContext = CreateDbContext();
        var rezervasyon = await SeedRezervasyonAsync(dbContext, TesisCariId, "Senaryo8 Misafir", "555-0008");
        var rezervasyonId = rezervasyon.Id;
        var service = CreateRezervasyonService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.KaydetOdemeAsync(rezervasyonId, new RezervasyonOdemeKaydetRequestDto
        {
            OdemeTutari = 100m,
            OdemeTipi = OdemeTipleri.Nakit,
            KasaBankaHesapId = KasaBankaBankaAId, // Nakit odeme tipi icin Banka hesabi -> uyumsuz
            CariKartId = CariKartWithHesapAId
        }));

        Assert.Equal(400, ex.ErrorCode);
    }

    // ─────────────────────────────────────────────────────────────
    // Yardimcilar
    // ─────────────────────────────────────────────────────────────

    private static async Task<Rezervasyon> SeedRezervasyonAsync(
        StysAppDbContext dbContext, int tesisId, string misafirAdSoyad, string telefon)
    {
        var rezervasyon = new Rezervasyon
        {
            ReferansNo = $"{TestMarker}-{Guid.NewGuid():N}"[..20],
            TesisId = tesisId,
            KisiSayisi = 1,
            GirisTarihi = new DateTime(2026, 6, 1),
            CikisTarihi = new DateTime(2026, 6, 3),
            ToplamBazUcret = 1000m,
            ToplamUcret = 1000m,
            ParaBirimi = "TRY",
            MisafirAdiSoyadi = misafirAdSoyad,
            MisafirTelefon = telefon,
            RezervasyonDurumu = RezervasyonDurumlari.CheckInTamamlandi,
            AktifMi = true
        };
        dbContext.Rezervasyonlar.Add(rezervasyon);
        await dbContext.SaveChangesAsync();
        return rezervasyon;
    }

    private static StysAppDbContext CreateDbContext()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            // Buraya normalde ulasilmaz: IntegrationFactAttribute baglanti dizesi yoksa testi
            // Skip eder. Yine de dogrudan cagrilirsa sessizce bir yerel DB'ye baglanmak yerine
            // acik bir hata ile durur.
            throw new InvalidOperationException(
                $"{IntegrationFactAttribute.ConnectionStringEnvVar} ortam degiskeni tanimli degil.");
        }

        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new StysAppDbContext(options, new FakeCurrentUserAccessor(), new FakeCurrentTenantAccessor());
    }

    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<TahsilatOdemeBelgesiProfile>();
            cfg.AddProfile<CariKartProfile>();
            cfg.AddProfile<CariHareketProfile>();
            cfg.AddProfile<MuhasebeDonemProfile>();
        }, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

        return config.CreateMapper();
    }

    private static ITahsilatOdemeBelgesiService CreateTahsilatOdemeBelgesiService(StysAppDbContext dbContext)
    {
        var mapper = CreateMapper();
        var tahsilatRepo = new TahsilatOdemeBelgesiRepository(dbContext, mapper);
        var cariKartRepo = new CariKartRepository(dbContext, mapper);
        var cariHareketRepo = new CariHareketRepository(dbContext, mapper);
        var muhasebeDonemService = CreateMuhasebeDonemService(dbContext);
        var userAccessScope = new FakeUserAccessScopeService();
        var cariHareketKapamaService = new CariHareketKapamaService(
            dbContext, tahsilatRepo, cariHareketRepo, muhasebeDonemService, userAccessScope, mapper);

        return new TahsilatOdemeBelgesiService(
            tahsilatRepo, cariKartRepo, cariHareketRepo, cariHareketKapamaService,
            dbContext, muhasebeDonemService, userAccessScope, mapper);
    }

    private static IMuhasebeDonemService CreateMuhasebeDonemService(StysAppDbContext dbContext)
    {
        var mapper = CreateMapper();
        var repo = new MuhasebeDonemRepository(dbContext, mapper);
        return new MuhasebeDonemService(repo, mapper, dbContext, new FakeMuhasebeTesisScopeService());
    }

    private static IRezervasyonOdemeMuhasebeService CreateRezervasyonOdemeMuhasebeService(StysAppDbContext dbContext)
    {
        return new RezervasyonOdemeMuhasebeService(
            dbContext,
            CreateTahsilatOdemeBelgesiService(dbContext),
            new FakeMuhasebeFisService());
    }

    private static RezervasyonService CreateRezervasyonService(StysAppDbContext dbContext)
    {
        var httpContextAccessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        return new RezervasyonService(
            dbContext,
            new FakeUserAccessScopeService(),
            new FakeBildirimService(),
            httpContextAccessor,
            new FakeLicenseService(),
            new FakeCurrentTenantAccessor(),
            new FakeDomainOperationLogger(),
            CreateRezervasyonOdemeMuhasebeService(dbContext));
    }

    private sealed class FakeCurrentUserAccessor : TOD.Platform.Security.Auth.Services.ICurrentUserAccessor
    {
        public string? GetCurrentUserName() => "integration-test";
        public Guid? GetCurrentUserId() => Guid.NewGuid();
    }

    // KurumId henuz atanmamisken (InitializeAsync ilk CreateDbContext cagrisini yaparken) referans
    // vermemek icin CurrentKurumId=null + IsSuperAdmin=true kullanilir; entity.KurumId zaten
    // seed sirasinda acikca atandigindan ApplyTenantRules bu kombinasyonda sorunsuz gecer
    // (TesisServiceTests.MutableCurrentTenantAccessor ile ayni desen).
    private sealed class FakeCurrentTenantAccessor : TOD.Platform.Security.Auth.Services.ICurrentTenantAccessor
    {
        public int? GetCurrentKurumId() => null;
        public IReadOnlyList<int> GetAccessibleKurumIds() => [];
        public bool IsSuperAdmin() => true;
        public bool IsKurumAdmin() => false;
    }

    private sealed class FakeUserAccessScopeService : IUserAccessScopeService
    {
        public Task<DomainAccessScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(DomainAccessScope.Unscoped());
    }

    private sealed class FakeMuhasebeTesisScopeService : IMuhasebeTesisScopeService
    {
        public Task<int[]> GetEffectiveTesisIdsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<int>());

        public Task<int[]> GetEffectiveTesisIdsAsync(DomainAccessScope scope, CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<int>());

        public Task EnsureCanAccessTesisAsync(int tesisId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeBildirimService : STYS.Bildirimler.Services.IBildirimService
    {
        public Task<List<STYS.Bildirimler.Dto.BildirimDto>> GetCurrentUserBildirimlerAsync(int take = 20, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<STYS.Bildirimler.Dto.BildirimDto>());
        public Task<int> GetCurrentUserUnreadCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<STYS.Bildirimler.Dto.BildirimTercihDto> GetCurrentUserTercihAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new STYS.Bildirimler.Dto.BildirimTercihDto());
        public Task<STYS.Bildirimler.Dto.BildirimTercihDto> UpdateCurrentUserTercihAsync(STYS.Bildirimler.Dto.BildirimTercihGuncelleRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(new STYS.Bildirimler.Dto.BildirimTercihDto());
        public Task MarkAsReadAsync(int bildirimId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task MarkAllAsReadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishToTesisUsersAsync(int tesisId, STYS.Bildirimler.Dto.BildirimOlusturRequestDto request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishToUsersAsync(IEnumerable<Guid> userIds, STYS.Bildirimler.Dto.BildirimOlusturRequestDto request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeLicenseService : ILicenseService
    {
        public Task<LicenseValidationResult> GetCurrentStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(LicenseValidationResult.Failure("test"));
        public Task<bool> IsModuleLicensedAsync(string moduleCode, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public void InvalidateCache() { }
        public Task EnsureLicensedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureModuleLicensedAsync(string moduleCode, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeDomainOperationLogger : IDomainOperationLogger
    {
        public void Started(string eventName, object payload) { }
        public void Completed(string eventName, object payload) { }
        public void Warning(string eventName, object payload) { }
        public void Failed(string eventName, Exception exception, object payload) { }
    }

    private sealed class FakeMuhasebeFisService : IMuhasebeFisService
    {
        public Task<IEnumerable<MuhasebeFisDto>> GetAllAsync(Func<IQueryable<MuhasebeFis>, IQueryable<MuhasebeFis>>? include = null) => throw new NotSupportedException();
        public Task<MuhasebeFisDto?> GetByIdAsync(int id, Func<IQueryable<MuhasebeFis>, IQueryable<MuhasebeFis>>? include = null) => throw new NotSupportedException();
        public Task<PagedResult<MuhasebeFisDto>> GetPagedAsync(PagedRequest request, System.Linq.Expressions.Expression<Func<MuhasebeFis, bool>>? predicate = null, Func<IQueryable<MuhasebeFis>, IQueryable<MuhasebeFis>>? include = null, Func<IQueryable<MuhasebeFis>, IOrderedQueryable<MuhasebeFis>>? orderBy = null) => throw new NotSupportedException();
        public Task<MuhasebeFisDto> AddAsync(MuhasebeFisDto dto) => throw new NotSupportedException();
        public Task<MuhasebeFisDto> UpdateAsync(MuhasebeFisDto dto) => throw new NotSupportedException();
        public Task DeleteAsync(int id) => throw new NotSupportedException();
        public Task<IEnumerable<MuhasebeFisDto>> WhereAsync(System.Linq.Expressions.Expression<Func<MuhasebeFis, bool>> predicate, Func<IQueryable<MuhasebeFis>, IQueryable<MuhasebeFis>>? include = null) => throw new NotSupportedException();
        public Task<bool> AnyAsync(System.Linq.Expressions.Expression<Func<MuhasebeFis, bool>> predicate, Func<IQueryable<MuhasebeFis>, IQueryable<MuhasebeFis>>? include = null) => throw new NotSupportedException();
        public Task<MuhasebeFisDto?> GetByIdWithSatirlarAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<MuhasebeFisDto>> GetByKaynakAsync(string kaynakModul, int kaynakId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MuhasebeFisDto> OnaylaAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MuhasebeFisDto> IptalEtAsync(int id, string? aciklama = null, CancellationToken cancellationToken = default) => throw new NotSupportedException("Bu senaryolarda fis olusmadigi icin cagrilmamali.");
        public Task<List<MuhasebeFisDto>> GetFilteredAsync(MuhasebeFisFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountFilteredAsync(MuhasebeFisFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<YevmiyeDefteriDto> GetYevmiyeDefteriAsync(MuhasebeFisFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<byte[]> ExportYevmiyeDefteriExcelAsync(MuhasebeFisFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MuavinDefterDto> GetMuavinDefterAsync(MuavinDefterFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<byte[]> ExportMuavinDefterExcelAsync(MuavinDefterFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MizanDto> GetMizanAsync(MizanFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MizanDto> GetMizanBakiyeAsync(MizanFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<byte[]> ExportMizanBakiyeExcelAsync(MizanFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MizanKarsilastirmaDto> KarsilastirMizanAsync(MizanFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TasinirMuhasebeFisiOlusturResultDto> TasinirMuhasebeFisiTaslagiOlusturAsync(TasinirMuhasebeFisiOlusturRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
