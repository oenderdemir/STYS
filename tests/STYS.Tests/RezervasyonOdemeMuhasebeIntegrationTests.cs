using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.CariHareketler.Mapping;
using STYS.Muhasebe.CariHareketler.Repositories;
using STYS.Muhasebe.CariHareketler.Services;
using STYS.Muhasebe.CariKartlar.Dtos;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.CariKartlar.Mapping;
using STYS.Muhasebe.CariKartlar.Repositories;
using STYS.Muhasebe.CariKartlar.Services;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Muhasebe.MuhasebeDonemleri.Entities;
using STYS.Muhasebe.MuhasebeDonemleri.Mapping;
using STYS.Muhasebe.MuhasebeDonemleri.Repositories;
using STYS.Muhasebe.MuhasebeDonemleri.Services;
using STYS.Muhasebe.MuhasebeHesapBakiyeleri.Services;
using STYS.Muhasebe.MuhasebeFisleri.Dtos;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using STYS.Muhasebe.MuhasebeFisleri.Mapping;
using STYS.Muhasebe.MuhasebeFisleri.Services;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.MuhasebeFisleri.Repositories;
using STYS.Muhasebe.SatisBelgeleri.Mapping;
using STYS.Muhasebe.SatisBelgeleri.Repositories;
using STYS.Muhasebe.SatisBelgeleri.Services;
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

    // Paylasilan TestMarker ile degil, bu ornege ozel uniqueSuffix ile filtrelenir — ayni sinifin
    // testleri xUnit'te paralel calisabildigi icin (her IntegrationFact kendi InitializeAsync/
    // DisposeAsync'ini alir), global TestMarker ile filtrelemek baska bir es zamanli testin
    // MuhasebeHesapPlanlari kaydini silip onun canli MuhasebeFisSatirlari'nin FK Restrict
    // hatasina dusmesine yol acabilirdi (bkz. CleanupAsync).
    private string _uniqueSuffix = TestMarker;

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
        _uniqueSuffix = uniqueSuffix;

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

        // MuhasebeHesapBakiyeGuncellemeService (OnaylaAsync/IptalEtAsync sirasinda cagrilir), her
        // detay hesabin TamKod'undaki "." ile ayrilan ust segmentlerinin de bir hesap plani kaydi
        // olarak var olmasini bekler (GetUstHesapKodlari) — aksi halde "Ust muhasebe hesabi
        // bulunamadi" hatasi verir. Bu yuzden detay hesaplardan once bir ana hesap seed edilir.
        var hesapAna = new MuhasebeHesapPlani { Kod = uniqueSuffix, TamKod = uniqueSuffix, Ad = "Test Ana Hesap", AktifMi = true, DetayHesapMi = false, HareketGorebilirMi = false, HesapTipi = HesapTipi.AnaHesap };
        dbContext.MuhasebeHesapPlanlari.Add(hesapAna);
        await dbContext.SaveChangesAsync();

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
        await CleanupAsync(dbContext, TesisCariId, TesisAvansId, KurumId, _uniqueSuffix);
    }

    private static async Task CleanupAsync(StysAppDbContext dbContext, int tesisCariId, int tesisAvansId, int kurumId, string uniqueSuffix)
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
        // TahsilatOdemeBelgeleri.KapatilacakCariHareketId CariHareketler'e FK Restrict ile bagli
        // oldugundan (retroaktif kapama senaryolarinda doldurulur), CariHareketler'den ONCE silinmeli.
        await dbContext.TahsilatOdemeBelgeleri
            .Where(x => x.CariKart != null && (x.CariKart.TesisId == tesisCariId || x.CariKart.TesisId == tesisAvansId))
            .ExecuteDeleteAsync();
        // MuhasebeFisSatirlari -> MuhasebeHesapPlanlari/CariKartlar/KasaBankaHesaplari Restrict FK
        // ile bagli oldugundan, fis satirlari (ve fisler) o tablolardan ONCE silinmeli.
        var fisIds = await dbContext.MuhasebeFisler
            .Where(x => x.TesisId == tesisCariId || x.TesisId == tesisAvansId)
            .Select(x => x.Id)
            .ToListAsync();
        if (fisIds.Count > 0)
        {
            await dbContext.MuhasebeFisSatirlari.Where(x => fisIds.Contains(x.MuhasebeFisId)).ExecuteDeleteAsync();
            await dbContext.MuhasebeFisler.Where(x => fisIds.Contains(x.Id)).ExecuteDeleteAsync();
        }
        await dbContext.CariHareketler
            .Where(x => x.CariKart != null && x.CariKart.TesisId == tesisCariId)
            .ExecuteDeleteAsync();
        await dbContext.SatisBelgeleri
            .Where(x => x.TesisId == tesisCariId || x.TesisId == tesisAvansId)
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
        // MuhasebeHesapBakiyeGuncellemeService (OnaylaAsync/IptalEtAsync) MuhasebeHesapBakiyeleri
        // satirlari uretir; bunlar da MuhasebeHesapPlanlari'na Restrict FK ile bagli.
        await dbContext.MuhasebeHesapBakiyeleri
            .Where(x => x.TesisId == tesisCariId || x.TesisId == tesisAvansId)
            .ExecuteDeleteAsync();
        // Global TestMarker DEGIL: es zamanli calisan diger IntegrationFact ornekleri de ayni
        // TestMarker on ekini kullaniyor; bu ornegin kendi uniqueSuffix'iyle filtrelenmezse
        // baska bir calisan testin hala canli MuhasebeFisSatirlari referanslayan hesap plani
        // silinmeye calisilir ve FK Restrict hatasi olusur.
        await dbContext.MuhasebeHesapPlanlari
            .Where(x => x.Kod != null && x.Kod.StartsWith(uniqueSuffix))
            .ExecuteDeleteAsync();
        await dbContext.Tesisler
            .Where(x => x.Id == tesisCariId || x.Id == tesisAvansId)
            .ExecuteDeleteAsync();
        // Burada da global TestMarker degil, ornege ozel uniqueSuffix kullanilir (bkz. yukaridaki
        // MuhasebeHesapPlanlari notu) — aksi halde es zamanli baska bir testin Tesisler kaydinin
        // hala referansladigi bir Il silinmeye calisilip FK Restrict hatasi olusabilir.
        await dbContext.Iller
            .Where(x => x.Ad != null && x.Ad.Contains(uniqueSuffix))
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
        // Not: TahsilatOdemeBelgesi.KaynakId her zaman bu rezervasyonun bir RezervasyonOdeme.Id'sine
        // esittir; yukaridaki satir hicbir odeme olusmadigini zaten kanitliyor, bu yuzden
        // KaynakModul=Rezervasyon icin GLOBAL bir kontrol (once burada vardi) gereksiz VE es zamanli
        // calisan diger IntegrationFact testleriyle (kendi rezervasyonlari icin gecerli belge
        // olusturanlar) yanlis pozitif cakismaya yol aciyordu — kaldirildi.
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
    // Senaryo 9 (Gelir Tahakkuku) — Eszamanli iki "Gelir Belgesi Olustur" cagrisi
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task Senaryo9_EszamanliGelirBelgesiOlusturma_YalnizcaBirBelgeKalir()
    {
        await using var seedContext = CreateDbContext();
        var rezervasyon = await SeedRezervasyonCheckOutTamamlandiAsync(seedContext, TesisCariId, "Senaryo9 Misafir", "555-0009");
        var rezervasyonId = rezervasyon.Id;
        // Cari kart cozumlemesinin (422) yarisi engellememesi icin dogrudan atanir — burada test
        // edilen SatisBelgesi/Rezervasyon.SatisBelgesiId uzerindeki yaris durumudur.
        rezervasyon.CariKartId = CariKartWithHesapAId;
        await seedContext.SaveChangesAsync();

        // Iki bagimsiz DbContext/servis grafi ile gercek eszamanlilik simule edilir.
        await using var dbContext1 = CreateDbContext();
        await using var dbContext2 = CreateDbContext();
        var service1 = CreateRezervasyonGelirTahakkukService(dbContext1);
        var service2 = CreateRezervasyonGelirTahakkukService(dbContext2);

        var sonuclar = await Task.WhenAll(
            SafeOlusturTaslakAsync(service1, rezervasyonId),
            SafeOlusturTaslakAsync(service2, rezervasyonId));

        // Ikisinden en az biri basarili olmali; SatisBelgesi(KaynakModul,KaynakTipi,KaynakId) uzerindeki
        // mevcut unique index (StysAppDbContext.cs) veya Rezervasyon.SatisBelgesiId uzerindeki yeni
        // filtrelenmis unique index sayesinde nihai durumda tam olarak BIR belge kalir.
        Assert.Contains(sonuclar, x => x);

        await using var verifyContext = CreateDbContext();
        var belgeSayisi = await verifyContext.SatisBelgeleri.CountAsync(x =>
            !x.IsDeleted && x.KaynakId == rezervasyonId.ToString());
        Assert.Equal(1, belgeSayisi);
    }

    private static async Task<bool> SafeOlusturTaslakAsync(IRezervasyonGelirTahakkukService service, int rezervasyonId)
    {
        try
        {
            await service.OlusturTaslakAsync(rezervasyonId);
            return true;
        }
        catch (BaseException)
        {
            return false;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 10 (Gelir Tahakkuku) — Kapatilmis bir tahsilat iptal edilir, geri alma calisir
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task Senaryo10_KapatilmisTahsilatIptalEdilir_GeriAlmaFaturaKalanTutariniArtirir()
    {
        await using var dbContext = CreateDbContext();
        var rezervasyon = await SeedRezervasyonCheckOutTamamlandiAsync(dbContext, TesisCariId, "Senaryo10 Misafir", "555-0010");
        var rezervasyonId = rezervasyon.Id;
        rezervasyon.CariKartId = CariKartWithHesapAId;
        await dbContext.SaveChangesAsync();

        var (odeme, belge) = await SeedTahsilatOdemeBelgesiAsync(dbContext, rezervasyonId, CariKartWithHesapAId, 400m, "S10-TAH-1");
        var faturaHareket = await SeedFaturaCariHareketiAsync(dbContext, rezervasyon, CariKartWithHesapAId, 1000m);

        var gelirService = CreateRezervasyonGelirTahakkukService(dbContext);
        var kapamaSonucu = await gelirService.KapatOncekiTahsilatlariAsync(rezervasyonId);
        Assert.Equal(1, kapamaSonucu.BasariliSayisi);

        var kapamaService = CreateCariHareketKapamaService(dbContext);
        await kapamaService.GeriAlAsync(belge.Id);

        await using var verifyContext = CreateDbContext();
        var faturaGuncel = await verifyContext.CariHareketler.SingleAsync(x => x.Id == faturaHareket.Id);
        Assert.Equal(1000m, faturaGuncel.KalanTutar);
        Assert.False(faturaGuncel.KapandiMi);

        var belgeGuncel = await verifyContext.TahsilatOdemeBelgeleri.SingleAsync(x => x.Id == belge.Id);
        Assert.Equal(TahsilatOdemeBelgeDurumlari.Iptal, belgeGuncel.Durum);
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 11 (Gelir Tahakkuku) — Donem kapaliyken kapama geri alma engellenir
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task Senaryo11_DonemKapaliyken_KapamaGeriAlmaEngellenir()
    {
        await using var dbContext = CreateDbContext();
        var rezervasyon = await SeedRezervasyonCheckOutTamamlandiAsync(dbContext, TesisCariId, "Senaryo11 Misafir", "555-0011");
        var rezervasyonId = rezervasyon.Id;
        rezervasyon.CariKartId = CariKartWithHesapAId;
        await dbContext.SaveChangesAsync();

        var (_, belge) = await SeedTahsilatOdemeBelgesiAsync(dbContext, rezervasyonId, CariKartWithHesapAId, 400m, "S11-TAH-1");
        await SeedFaturaCariHareketiAsync(dbContext, rezervasyon, CariKartWithHesapAId, 1000m);

        var gelirService = CreateRezervasyonGelirTahakkukService(dbContext);
        await gelirService.KapatOncekiTahsilatlariAsync(rezervasyonId);

        // Tahsilatin BelgeTarihi'ni acik donemin disina (kapali bir doneme) tasi.
        belge.BelgeTarihi = new DateTime(2019, 1, 1);
        await dbContext.SaveChangesAsync();

        var kapamaService = CreateCariHareketKapamaService(dbContext);
        var ex = await Assert.ThrowsAsync<BaseException>(() => kapamaService.GeriAlAsync(belge.Id));
        Assert.Equal(400, ex.ErrorCode);

        await using var verifyContext = CreateDbContext();
        var belgeGuncel = await verifyContext.TahsilatOdemeBelgeleri.SingleAsync(x => x.Id == belge.Id);
        Assert.Equal(TahsilatOdemeBelgeDurumlari.Aktif, belgeGuncel.Durum);
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 12 — Taslak durumda muhasebe fisi olan odeme iptal edilemez
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task Senaryo12_TaslakFisliOdeme_IptalEngellenir()
    {
        await using var dbContext = CreateDbContext();
        var rezervasyon = await SeedRezervasyonAsync(dbContext, TesisCariId, "Senaryo12 Misafir", "555-0012");
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
        var belgeId = odeme.TahsilatOdemeBelgesiId!.Value;

        var fisService = CreateTahsilatOdemeBelgesiMuhasebeFisService(dbContext);
        await fisService.FisOlusturAsync(belgeId);

        var fis = await dbContext.MuhasebeFisler.SingleAsync(x =>
            x.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi && x.KaynakId == belgeId);
        Assert.Equal(MuhasebeFisDurumlari.Taslak, fis.Durum);

        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            service.IptalOdemeAsync(rezervasyonId, odeme.Id, new RezervasyonOdemeIptalRequestDto { Aciklama = "Test" }));
        Assert.Equal(409, ex.ErrorCode);

        await using var verifyContext = CreateDbContext();
        Assert.Equal(RezervasyonOdemeDurumlari.Aktif, (await verifyContext.RezervasyonOdemeler.SingleAsync(x => x.Id == odeme.Id)).Durum);
        Assert.Equal(TahsilatOdemeBelgeDurumlari.Aktif, (await verifyContext.TahsilatOdemeBelgeleri.SingleAsync(x => x.Id == belgeId)).Durum);
        Assert.Equal(MuhasebeFisDurumlari.Taslak, (await verifyContext.MuhasebeFisler.SingleAsync(x => x.Id == fis.Id)).Durum);
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 13 — Onayli muhasebe fisi olan odeme iptal edilemez;
    // otomatik ters kayit uretilmez
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task Senaryo13_OnayliFisliOdeme_IptalEngellenirVeOtomatikTersKayitOlusmaz()
    {
        await using var dbContext = CreateDbContext();
        var rezervasyon = await SeedRezervasyonAsync(dbContext, TesisCariId, "Senaryo13 Misafir", "555-0013");
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
        var belgeId = odeme.TahsilatOdemeBelgesiId!.Value;

        var fisService = CreateTahsilatOdemeBelgesiMuhasebeFisService(dbContext);
        await fisService.FisOlusturAsync(belgeId);
        var fis = await dbContext.MuhasebeFisler.SingleAsync(x =>
            x.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi && x.KaynakId == belgeId);

        var muhasebeFisService = CreateMuhasebeFisService(dbContext);
        await muhasebeFisService.OnaylaAsync(fis.Id);

        // Bu cagri, sadece RezervasyonYonetimi.Manage yetkisiyle korunan
        // RezervasyonController.IptalOdeme uc noktasinin arkasindaki servis cagrisiyla ayni —
        // MuhasebeFisYonetimi.Manage yetkisi hic devreye girmemeli.
        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            service.IptalOdemeAsync(rezervasyonId, odeme.Id, new RezervasyonOdemeIptalRequestDto { Aciklama = "Test" }));
        Assert.Equal(409, ex.ErrorCode);

        await using var verifyContext = CreateDbContext();
        Assert.Equal(RezervasyonOdemeDurumlari.Aktif, (await verifyContext.RezervasyonOdemeler.SingleAsync(x => x.Id == odeme.Id)).Durum);
        Assert.Equal(TahsilatOdemeBelgeDurumlari.Aktif, (await verifyContext.TahsilatOdemeBelgeleri.SingleAsync(x => x.Id == belgeId)).Durum);
        Assert.Equal(MuhasebeFisDurumlari.Onayli, (await verifyContext.MuhasebeFisler.SingleAsync(x => x.Id == fis.Id)).Durum);

        // Otomatik ters kayit uretilmedi: belgeye bagli fis sayisi hala 1.
        var fisSayisi = await verifyContext.MuhasebeFisler.CountAsync(x =>
            x.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi && x.KaynakId == belgeId);
        Assert.Equal(1, fisSayisi);
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 14 — Iptal durumundaki muhasebe fisi olan odeme iptal edilebilir;
    // yeniden ters kayit uretilmez
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task Senaryo14_IptalFisliOdeme_IptalEdilebilirVeYenidenTersKayitUretilmez()
    {
        await using var dbContext = CreateDbContext();
        var rezervasyon = await SeedRezervasyonAsync(dbContext, TesisCariId, "Senaryo14 Misafir", "555-0014");
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
        var belgeId = odeme.TahsilatOdemeBelgesiId!.Value;

        var fisService = CreateTahsilatOdemeBelgesiMuhasebeFisService(dbContext);
        await fisService.FisOlusturAsync(belgeId);
        var fis = await dbContext.MuhasebeFisler.SingleAsync(x =>
            x.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi && x.KaynakId == belgeId);

        var muhasebeFisService = CreateMuhasebeFisService(dbContext);
        await muhasebeFisService.OnaylaAsync(fis.Id);
        // Muhasebe ekranindan (MuhasebeFisYonetimi.Manage) bilincli olarak iptal edilmis —
        // bu, orijinal fisi Durum=Iptal yapar ve ayrica bir "TERS-" ters kayit fisi uretir.
        await muhasebeFisService.IptalEtAsync(fis.Id, "Muhasebe iptali");

        var ozetSonra = await service.IptalOdemeAsync(rezervasyonId, odeme.Id, new RezervasyonOdemeIptalRequestDto { Aciklama = "Test" });
        Assert.Equal(0m, ozetSonra.OdenenTutar);

        await using var verifyContext = CreateDbContext();
        Assert.Equal(RezervasyonOdemeDurumlari.Iptal, (await verifyContext.RezervasyonOdemeler.SingleAsync(x => x.Id == odeme.Id)).Durum);
        Assert.Equal(TahsilatOdemeBelgeDurumlari.Iptal, (await verifyContext.TahsilatOdemeBelgeleri.SingleAsync(x => x.Id == belgeId)).Durum);
        Assert.Equal(MuhasebeFisDurumlari.Iptal, (await verifyContext.MuhasebeFisler.SingleAsync(x => x.Id == fis.Id)).Durum);

        // Muhasebenin kendi iptalinden gelen 1 ters kayit disinda YENI bir ters kayit uretilmedi:
        // orijinal fis + tek ters kayit = toplam 2.
        var fisSayisi = await verifyContext.MuhasebeFisler.CountAsync(x =>
            x.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi && x.KaynakId == belgeId);
        Assert.Equal(2, fisSayisi);
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 15 — Bagli fis TersKayit durumundaysa odeme iptalini engellemez
    // (belge.MuhasebeFisId normal akista hep orijinal fise isaret eder ve onun
    // durumu Iptal olur; bu senaryo savunma amacli olarak TersKayit durumunun
    // da "serbest" sayildigini dogrudan test eder).
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task Senaryo15_TersKayitDurumundakiFis_OdemeIptaliniEngellemez()
    {
        await using var dbContext = CreateDbContext();
        var rezervasyon = await SeedRezervasyonAsync(dbContext, TesisCariId, "Senaryo15 Misafir", "555-0015");
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
        var belgeId = odeme.TahsilatOdemeBelgesiId!.Value;
        var belge = await dbContext.TahsilatOdemeBelgeleri.SingleAsync(x => x.Id == belgeId);

        var tersKayitFis = new MuhasebeFis
        {
            TesisId = TesisCariId,
            MaliYil = 2026,
            Donem = 1,
            FisNo = $"{TestMarker}-TERS-{Guid.NewGuid():N}"[..24],
            FisTarihi = DateTime.UtcNow,
            FisTipi = MuhasebeFisTipleri.Duzeltme,
            KaynakModul = MuhasebeKaynakModulleri.TahsilatOdemeBelgesi,
            KaynakId = belgeId,
            Durum = MuhasebeFisDurumlari.TersKayit,
            Aciklama = "Senaryo15 sentetik ters kayit"
        };
        dbContext.MuhasebeFisler.Add(tersKayitFis);
        await dbContext.SaveChangesAsync();
        belge.MuhasebeFisId = tersKayitFis.Id;
        await dbContext.SaveChangesAsync();

        var ozetSonra = await service.IptalOdemeAsync(rezervasyonId, odeme.Id, new RezervasyonOdemeIptalRequestDto { Aciklama = "Test" });
        Assert.Equal(0m, ozetSonra.OdenenTutar);

        await using var verifyContext = CreateDbContext();
        Assert.Equal(RezervasyonOdemeDurumlari.Iptal, (await verifyContext.RezervasyonOdemeler.SingleAsync(x => x.Id == odeme.Id)).Durum);
        Assert.Equal(TahsilatOdemeBelgeDurumlari.Iptal, (await verifyContext.TahsilatOdemeBelgeleri.SingleAsync(x => x.Id == belgeId)).Durum);
        // MuhasebeFisService.IptalEtAsync hic cagrilmadi: TersKayit fis degismeden kaldi.
        Assert.Equal(MuhasebeFisDurumlari.TersKayit, (await verifyContext.MuhasebeFisler.SingleAsync(x => x.Id == tersKayitFis.Id)).Durum);
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 16 — Yetki felsefesi: RezervasyonYonetimi.Manage, dolayli olarak
    // MuhasebeFisYonetimi.Manage gerektiren bir islem tetikleyemez.
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task Senaryo16_RezervasyonYonetimiYetkisi_MuhasebeFisIptaliniTetikleyemez()
    {
        // Uc noktalarin yetki kapsami degismedi: RezervasyonController.IptalOdeme hala sadece
        // RezervasyonYonetimi.Manage istiyor; MuhasebeFisController.IptalEt hala ayrica
        // MuhasebeFisYonetimi.Manage istiyor. Duzeltme bu iki yetkiyi birbirine baglamiyor,
        // servis katmaninda otomatik cagriyi tamamen kaldiriyor.
        var rezervasyonEndpoint = typeof(STYS.Rezervasyonlar.Controllers.RezervasyonController)
            .GetMethod(nameof(STYS.Rezervasyonlar.Controllers.RezervasyonController.IptalOdeme));
        var rezervasyonPermissionCodes = GetPermissionCodes(rezervasyonEndpoint!);
        Assert.Contains(StructurePermissions.RezervasyonYonetimi.Manage, rezervasyonPermissionCodes);
        Assert.DoesNotContain(StructurePermissions.MuhasebeFisYonetimi.Manage, rezervasyonPermissionCodes);

        var muhasebeFisEndpoint = typeof(STYS.Muhasebe.MuhasebeFisleri.Controllers.MuhasebeFisController)
            .GetMethod(nameof(STYS.Muhasebe.MuhasebeFisleri.Controllers.MuhasebeFisController.IptalEt));
        var muhasebeFisPermissionCodes = GetPermissionCodes(muhasebeFisEndpoint!);
        Assert.Contains(StructurePermissions.MuhasebeFisYonetimi.Manage, muhasebeFisPermissionCodes);

        // Fonksiyonel dogrulama: RezervasyonController.IptalOdeme'nin cagirdigi tam servis zinciri
        // (CreateRezervasyonService) — yalnizca RezervasyonYonetimi.Manage'in koruyabildigi yol —
        // onayli fisli bir odemeyi iptal edemiyor ve muhasebe fisine dokunmuyor.
        await using var dbContext = CreateDbContext();
        var rezervasyon = await SeedRezervasyonAsync(dbContext, TesisCariId, "Senaryo16 Misafir", "555-0016");
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
        var belgeId = odeme.TahsilatOdemeBelgesiId!.Value;

        var fisService = CreateTahsilatOdemeBelgesiMuhasebeFisService(dbContext);
        await fisService.FisOlusturAsync(belgeId);
        var fis = await dbContext.MuhasebeFisler.SingleAsync(x =>
            x.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi && x.KaynakId == belgeId);
        await CreateMuhasebeFisService(dbContext).OnaylaAsync(fis.Id);

        await Assert.ThrowsAsync<BaseException>(() =>
            service.IptalOdemeAsync(rezervasyonId, odeme.Id, new RezervasyonOdemeIptalRequestDto { Aciklama = "Test" }));

        await using var verifyContext = CreateDbContext();
        Assert.Equal(MuhasebeFisDurumlari.Onayli, (await verifyContext.MuhasebeFisler.SingleAsync(x => x.Id == fis.Id)).Durum);
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 17 — Hizli cari kart olusturma uc noktasinin yetki kapsami:
    // CariKartYonetimi.Manage sahipleri de kullanabilir, ama genel CariKart
    // View/Manage uc noktalariyla karistirilmamali.
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public void Senaryo17_CariKartHizliOlustur_YetkiKapsami_ManageVeQuickCreateKabulEder()
    {
        var endpoint = typeof(STYS.Rezervasyonlar.Controllers.RezervasyonController)
            .GetMethod(nameof(STYS.Rezervasyonlar.Controllers.RezervasyonController.CariKartHizliOlustur));
        var permissionCodes = GetPermissionCodes(endpoint!);

        Assert.Contains(StructurePermissions.CariKartYonetimi.QuickCreate, permissionCodes);
        Assert.Contains(StructurePermissions.CariKartYonetimi.Manage, permissionCodes);

        // Genel cari kart listeleme/olusturma uc noktalari QuickCreate'i kabul etmemeli —
        // Resepsiyonist'e (yalnizca QuickCreate sahibi) genel CariKart ekranina erisim acilmamali.
        var listEndpoint = typeof(STYS.Muhasebe.CariKartlar.Controllers.CariKartlarController)
            .GetMethod(nameof(STYS.Muhasebe.CariKartlar.Controllers.CariKartlarController.GetList));
        var listPermissionCodes = GetPermissionCodes(listEndpoint!);
        Assert.DoesNotContain(StructurePermissions.CariKartYonetimi.QuickCreate, listPermissionCodes);

        var createEndpoint = typeof(STYS.Muhasebe.CariKartlar.Controllers.CariKartlarController)
            .GetMethod(nameof(STYS.Muhasebe.CariKartlar.Controllers.CariKartlarController.Create));
        var createPermissionCodes = GetPermissionCodes(createEndpoint!);
        Assert.DoesNotContain(StructurePermissions.CariKartYonetimi.QuickCreate, createPermissionCodes);
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 18 — Hizli cari kart olusturma, minimum alanlarla gecerli bir
    // Musteri tipinde cari kart uretir (RezervasyonController.CariKartHizliOlustur
    // ile ayni DTO sekli).
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task Senaryo18_CariKartHizliOlustur_MinimumAlanlarlaMusteriCariKartUretir()
    {
        await using var dbContext = CreateDbContext();
        var cariKartService = CreateCariKartService(dbContext);

        var dto = new CariKartDto
        {
            TesisId = TesisCariId,
            CariTipi = CariKartTipleri.Musteri,
            UnvanAdSoyad = "Senaryo18 Hizli Misafir",
            VergiNoTckn = "11111111111",
            Telefon = "555-0018",
            AktifMi = true,
            EFaturaMukellefiMi = false,
            EArsivKapsamindaMi = false
        };

        var result = await cariKartService.AddAsync(dto);

        Assert.True(result.Id > 0);
        Assert.Equal(CariKartTipleri.Musteri, result.CariTipi);
        Assert.Equal("Senaryo18 Hizli Misafir", result.UnvanAdSoyad);
        Assert.Equal(TesisCariId, result.TesisId);
        Assert.True(result.AktifMi);
        Assert.NotNull(result.MuhasebeHesapPlaniId);
        Assert.False(result.EFaturaMukellefiMi);
        Assert.False(result.EArsivKapsamindaMi);
        Assert.Empty(result.BankaHesaplari);
        Assert.Empty(result.YetkiliKisiler);

        await using var verifyContext = CreateDbContext();
        var kayitli = await verifyContext.CariKartlar.SingleAsync(x => x.Id == result.Id);
        Assert.Equal("Senaryo18 Hizli Misafir", kayitli.UnvanAdSoyad);

        // CleanupAsync, TesisId'ye gore CariKartlar'i temizler ama bu test AddAsync'in ana hesap
        // altinda urettigi GERCEK (uniqueSuffix ile ilgisiz, ana hesap plani "1.12.120" hiyerarsisi
        // altindaki) detay hesabi kapsamaz — burada elle temizlenir.
        var detayHesapId = result.MuhasebeHesapPlaniId;
        await verifyContext.CariKartlar.Where(x => x.Id == result.Id).ExecuteDeleteAsync();
        if (detayHesapId.HasValue)
        {
            await verifyContext.MuhasebeHesapPlanlari.Where(x => x.Id == detayHesapId.Value).ExecuteDeleteAsync();
            await verifyContext.Set<MuhasebeHesapKoduSayac>()
                .Where(x => x.TesisId == TesisCariId && x.AnaHesapKodu == MuhasebeAnaHesapKodlari.CariMusteri)
                .ExecuteDeleteAsync();
        }
    }

    private static List<string> GetPermissionCodes(System.Reflection.MethodInfo endpoint)
    {
        var attributeData = endpoint.GetCustomAttributesData()
            .Single(x => x.AttributeType == typeof(TOD.Platform.AspNetCore.Authorization.PermissionAttribute));
        var permissionsArgument = Assert.Single(attributeData.ConstructorArguments);
        var permissions = (System.Collections.ObjectModel.ReadOnlyCollection<System.Reflection.CustomAttributeTypedArgument>)permissionsArgument.Value!;
        return permissions.Select(x => (string)x.Value!).ToList();
    }

    // ─────────────────────────────────────────────────────────────
    // Yardimcilar
    // ─────────────────────────────────────────────────────────────

    private static async Task<Rezervasyon> SeedRezervasyonCheckOutTamamlandiAsync(
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
            RezervasyonDurumu = RezervasyonDurumlari.CheckOutTamamlandi,
            AktifMi = true
        };
        dbContext.Rezervasyonlar.Add(rezervasyon);
        await dbContext.SaveChangesAsync();
        return rezervasyon;
    }

    private static async Task<(RezervasyonOdeme Odeme, TahsilatOdemeBelgesi Belge)> SeedTahsilatOdemeBelgesiAsync(
        StysAppDbContext dbContext, int rezervasyonId, int cariKartId, decimal tutar, string belgeNo)
    {
        var odeme = new RezervasyonOdeme
        {
            RezervasyonId = rezervasyonId,
            OdemeTarihi = DateTime.UtcNow,
            OdemeTutari = tutar,
            ParaBirimi = "TRY",
            OdemeTipi = OdemeTipleri.Nakit
        };
        dbContext.RezervasyonOdemeler.Add(odeme);
        await dbContext.SaveChangesAsync();

        var belge = new TahsilatOdemeBelgesi
        {
            BelgeNo = belgeNo,
            BelgeTarihi = DateTime.UtcNow,
            BelgeTipi = TahsilatOdemeBelgeTipleri.Tahsilat,
            CariKartId = cariKartId,
            Tutar = tutar,
            ParaBirimi = "TRY",
            OdemeYontemi = OdemeTipleri.Nakit,
            KaynakModul = MuhasebeKaynakModulleri.Rezervasyon,
            KaynakId = odeme.Id,
            Durum = TahsilatOdemeBelgeDurumlari.Aktif
        };
        dbContext.TahsilatOdemeBelgeleri.Add(belge);
        await dbContext.SaveChangesAsync();

        return (odeme, belge);
    }

    /// <summary>
    /// SatisBelgesiMuhasebeFisService.CreateCariHareketAsync'in urettigi fatura CariHareket'iyle
    /// ayni semada, dogrudan seed edilir — bu testlerin odagi retroaktif kapama/geri-alma mantigi
    /// oldugu icin (KapatOncekiTahsilatlariAsync ve CariHareketKapamaService), fatura onay/fis
    /// zincirinin tamamini yeniden kurmak gerekmez.
    /// </summary>
    private static async Task<CariHareket> SeedFaturaCariHareketiAsync(
        StysAppDbContext dbContext, Rezervasyon rezervasyon, int cariKartId, decimal genelToplam)
    {
        var satisBelgesi = new STYS.Muhasebe.SatisBelgeleri.Entities.SatisBelgesi
        {
            BelgeNo = $"{TestMarker}-FAT-{Guid.NewGuid():N}"[..24],
            BelgeTipi = STYS.Muhasebe.SatisBelgeleri.Enums.SatisBelgesiTipi.SatisFaturasi,
            Durum = STYS.Muhasebe.SatisBelgeleri.Enums.SatisBelgesiDurumu.MuhasebeOnaylandi,
            KaynakModul = STYS.Muhasebe.SatisBelgeleri.Enums.SatisKaynakModulu.Otel,
            KaynakTipi = "RezervasyonCheckout",
            KaynakId = rezervasyon.Id.ToString(),
            TesisId = rezervasyon.TesisId,
            CariKartId = cariKartId,
            BelgeTarihi = DateTime.UtcNow,
            GenelToplam = genelToplam,
            MuhasebeFisId = null
        };
        dbContext.SatisBelgeleri.Add(satisBelgesi);
        await dbContext.SaveChangesAsync();

        rezervasyon.SatisBelgesiId = satisBelgesi.Id;
        await dbContext.SaveChangesAsync();

        var faturaHareket = new CariHareket
        {
            CariKartId = cariKartId,
            HareketTarihi = DateTime.UtcNow,
            BelgeTuru = "SatisFaturasi",
            BelgeNo = satisBelgesi.BelgeNo,
            BorcTutari = genelToplam,
            AlacakTutari = 0m,
            KapananTutar = 0m,
            KalanTutar = genelToplam,
            ParaBirimi = "TRY",
            Durum = CariHareketDurumlari.Aktif,
            KaynakModul = MuhasebeKaynakModulleri.SatisBelgesi,
            KaynakId = satisBelgesi.Id
        };
        dbContext.CariHareketler.Add(faturaHareket);
        await dbContext.SaveChangesAsync();

        return faturaHareket;
    }

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
            cfg.AddProfile<SatisBelgesiProfile>();
            cfg.AddProfile<MuhasebeFisProfile>();
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

        var posTahsilatValorSnapshotService = new STYS.Muhasebe.PosTahsilatValorleri.Services.PosTahsilatValorSnapshotService(
            dbContext,
            new STYS.Muhasebe.Common.Services.ValorTarihHesaplamaService(new STYS.Muhasebe.Common.Services.NoOpResmiTatilGunuProvider()),
            new FakeMuhasebeFisService());

        return new TahsilatOdemeBelgesiService(
            tahsilatRepo, cariKartRepo, cariHareketRepo, cariHareketKapamaService,
            dbContext, muhasebeDonemService, userAccessScope, posTahsilatValorSnapshotService, mapper);
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
            new FakeMuhasebeFisService(),
            new RezervasyonCariKartResolver(dbContext));
    }

    private static IMuhasebeFisService CreateMuhasebeFisService(StysAppDbContext dbContext)
    {
        var mapper = CreateMapper();
        var repository = new MuhasebeFisRepository(dbContext, mapper);
        return new MuhasebeFisService(
            repository,
            mapper,
            dbContext,
            CreateMuhasebeDonemService(dbContext),
            new MuhasebeHesapBakiyeGuncellemeService(dbContext),
            new FakeUserAccessScopeService(),
            new FakeDomainOperationLogger());
    }

    private static ITahsilatOdemeBelgesiMuhasebeFisService CreateTahsilatOdemeBelgesiMuhasebeFisService(StysAppDbContext dbContext)
    {
        return new TahsilatOdemeBelgesiMuhasebeFisService(dbContext, CreateMapper(), CreateMuhasebeDonemService(dbContext));
    }

    private static ICariKartService CreateCariKartService(StysAppDbContext dbContext)
    {
        var mapper = CreateMapper();
        var repository = new CariKartRepository(dbContext, mapper);
        return new CariKartService(
            repository,
            dbContext,
            new FakeUserAccessScopeService(),
            new MuhasebeDetayHesapService(dbContext),
            mapper);
    }

    private static ICariHareketKapamaService CreateCariHareketKapamaService(StysAppDbContext dbContext)
    {
        var mapper = CreateMapper();
        var tahsilatRepo = new TahsilatOdemeBelgesiRepository(dbContext, mapper);
        var cariHareketRepo = new CariHareketRepository(dbContext, mapper);
        var muhasebeDonemService = CreateMuhasebeDonemService(dbContext);
        var userAccessScope = new FakeUserAccessScopeService();
        return new CariHareketKapamaService(
            dbContext, tahsilatRepo, cariHareketRepo, muhasebeDonemService, userAccessScope, mapper);
    }

    private static ISatisBelgesiService CreateSatisBelgesiService(StysAppDbContext dbContext)
    {
        var mapper = CreateMapper();
        var satisBelgesiRepo = new SatisBelgesiRepository(dbContext, mapper);
        var muhasebeFisRepo = new MuhasebeFisRepository(dbContext, mapper);
        return new SatisBelgesiService(
            satisBelgesiRepo,
            dbContext,
            mapper,
            muhasebeFisRepo,
            new FakeMuhasebeFisService(),
            new FakeUserAccessScopeService(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SatisBelgesiService>.Instance,
            new FakeDomainOperationLogger());
    }

    private static ISatisBelgesiTaslakOlusturmaService CreateSatisBelgesiTaslakOlusturmaService(StysAppDbContext dbContext)
    {
        var mapper = CreateMapper();
        var satisBelgesiRepo = new SatisBelgesiRepository(dbContext, mapper);
        return new SatisBelgesiTaslakOlusturmaService(
            CreateSatisBelgesiService(dbContext),
            satisBelgesiRepo,
            new FakeUserAccessScopeService(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SatisBelgesiTaslakOlusturmaService>.Instance);
    }

    private static IRezervasyonSatisBelgesiService CreateRezervasyonSatisBelgesiService(StysAppDbContext dbContext)
    {
        return new RezervasyonSatisBelgesiService(
            dbContext,
            new FakeUserAccessScopeService(),
            CreateSatisBelgesiTaslakOlusturmaService(dbContext),
            new RezervasyonCariKartResolver(dbContext),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<RezervasyonSatisBelgesiService>.Instance);
    }

    private static IRezervasyonGelirTahakkukService CreateRezervasyonGelirTahakkukService(StysAppDbContext dbContext)
    {
        return new RezervasyonGelirTahakkukService(
            dbContext,
            new FakeUserAccessScopeService(),
            CreateRezervasyonSatisBelgesiService(dbContext),
            CreateSatisBelgesiService(dbContext),
            CreateCariHareketKapamaService(dbContext));
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
            CreateRezervasyonOdemeMuhasebeService(dbContext),
            CreateRezervasyonGelirTahakkukService(dbContext));
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
        public Task<STYS.Muhasebe.MuhasebeFisleri.Dtos.MuhasebeFisIptalSonucDto> PosValorTransferFisiniIptalEtAsync(int muhasebeFisId, int beklenenKaynakId, int beklenenTesisId, string aciklama, CancellationToken cancellationToken = default) => throw new NotSupportedException();
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
