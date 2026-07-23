using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Iller.Entities;
using STYS.Kurumlar.Entities;
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
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using STYS.Muhasebe.MuhasebeFisleri.Mapping;
using STYS.Muhasebe.MuhasebeFisleri.Repositories;
using STYS.Muhasebe.MuhasebeFisleri.Services;
using STYS.Muhasebe.MuhasebeHesapBakiyeleri.Services;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.PosTahsilatValorleri.Dtos;
using STYS.Muhasebe.PosTahsilatValorleri.Entities;
using STYS.Muhasebe.PosTahsilatValorleri.Services;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Mapping;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Repositories;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Services;
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

        // CariHareketler (Senaryo 8'in gercek cari hareket/kapama zinciri) - once self-referencing
        // IliskiliCariHareketId'yi NULL'a cek (FK Restrict), sonra kartlari silmeden ONCE hareketleri
        // sil.
        var cariHareketIds = await dbContext.CariHareketler
            .Where(x => x.CariKart != null && (x.CariKart.TesisId == TesisAId || x.CariKart.TesisId == TesisBId))
            .Select(x => x.Id)
            .ToListAsync();
        if (cariHareketIds.Count > 0)
        {
            await dbContext.CariHareketler.Where(x => cariHareketIds.Contains(x.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IliskiliCariHareketId, (int?)null));
            await dbContext.CariHareketler.Where(x => cariHareketIds.Contains(x.Id)).ExecuteDeleteAsync();
        }

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

    private static StysAppDbContext CreateDbContext(params Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<StysAppDbContext>().UseSqlServer(ConnectionString);
        if (interceptors.Length > 0)
        {
            builder.AddInterceptors(interceptors);
        }
        var options = builder.Options;
        return new StysAppDbContext(options, new FakeCurrentUserAccessor(), new FakeCurrentTenantAccessor());
    }

    /// <summary>
    /// PosValorFisNoSayaclari uzerindeki kilitli SELECT (WITH UPDLOCK/ROWLOCK/HOLDLOCK) TAM
    /// tamamlandiginda iki eszamanli tarafi GERCEKTEN durdurup ayni anda serbest birakan bir
    /// DbCommandInterceptor. "Sayac henuz yokken ilk olusturma" yarisini test ederken, cagri
    /// oncesindeki bir gate/semaphore'un bunu garanti ETMEDIGI (cunku iki task'in HANGI noktada
    /// duracagi belirsizdir - HesabaAktarAsync'in Adim A'sina, aktif donem sorgusuna vb. de takilabilir)
    /// tespit edildi; bu interceptor, SQL komut metnini inceleyerek TAM olarak dogru SELECT
    /// tamamlandiktan SONRA (INSERT'ten HEMEN once) her iki tarafi da bariyerde bekletir.
    /// </summary>
    private sealed class SayacSelectBarrierInterceptor : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
    {
        private readonly SemaphoreSlim _gate;
        private readonly CountdownEvent _hazir;
        public readonly System.Collections.Concurrent.ConcurrentBag<string> GorulenKomutlar = [];

        public SayacSelectBarrierInterceptor(SemaphoreSlim gate, CountdownEvent hazir)
        {
            _gate = gate;
            _hazir = hazir;
        }

        public override async ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command,
            Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            GorulenKomutlar.Add(command.CommandText);
            if (command.CommandText.Contains("PosValorFisNoSayaclari", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("UPDLOCK", StringComparison.OrdinalIgnoreCase))
            {
                // ONEMLI: bariyer SELECT TAMAMLANDIKTAN SONRA (ReaderExecutedAsync) DEGIL,
                // komut SQL Server'a GONDERILMEDEN ONCE (ReaderExecutingAsync) uygulanir. UPDLOCK,
                // baska bir UPDLOCK ile UYUMSUZDUR (S/S kilitlerin aksine) - eger bariyer SELECT
                // TAMAMLANDIKTAN SONRA konulsaydi, ilk tamamlanan taraf HALA transaction'ini acik
                // tutarak (kilidi elinde tutarak) bariyerde beklerdi, ikinci taraf ise TAM O KILIDI
                // beklemek zorunda kalip SQL Server seviyesinde bloke olur ve interceptor'a hic
                // ULASAMAZDI - bu, iki tarafin da "hazir" sinyalini asla veremeyecegi bir KENDI
                // KENDINE deadlock'a yol acardi (gercekten yasandi, bkz. commit gecmisi). Komut
                // gonderilmeden ONCE senkronize etmek, her iki SELECT'in ayni anda SQL Server'a
                // ulasmasini saglar - kim once kilidi alirsa dogal olarak "kazanir", digeri SQL
                // Server'in KENDI kilit bekleme mekanizmasiyla (test kodunda degil) bloke olur.
                _hazir.Signal();
                await _gate.WaitAsync(cancellationToken);
            }

            return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    /// <summary>
    /// PosTahsilatValorleri uzerindeki kilitli SELECT (WITH UPDLOCK/ROWLOCK) SQL Server'a
    /// GONDERILMEDEN HEMEN once (yalnizca ILK eslesen komutta) iki tarafi bariyerde durdurup ayni
    /// anda serbest birakan bir DbCommandInterceptor. Senaryo 8'de, HesabaAktarAsync'in Adim A
    /// claim'i ile PosTahsilatValorSnapshotService.IptalEtAsync'in kendi kilitli SELECT'ini -
    /// production'daki TAM kritik kilit noktasinda - bulusturmak icin kullanilir; boylece yaris
    /// yalnizca Task.WhenAll'in rastgele zamanlamasina degil, GERCEK SQL Server satir kilidi
    /// rekabetine dayanir.
    /// </summary>
    private sealed class PosValorSelectBarrierInterceptor : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
    {
        private readonly SemaphoreSlim _gate;
        private readonly CountdownEvent _hazir;
        private bool _tetiklendi;

        public PosValorSelectBarrierInterceptor(SemaphoreSlim gate, CountdownEvent hazir)
        {
            _gate = gate;
            _hazir = hazir;
        }

        public override async ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command,
            Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (!_tetiklendi
                && command.CommandText.Contains("PosTahsilatValorleri", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("UPDLOCK", StringComparison.OrdinalIgnoreCase))
            {
                _tetiklendi = true;
                _hazir.Signal();
                await _gate.WaitAsync(cancellationToken);
            }

            return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<MuhasebeDonemProfile>();
            cfg.AddProfile<MuhasebeFisProfile>();
            cfg.AddProfile<TahsilatOdemeBelgesiProfile>();
            cfg.AddProfile<CariKartProfile>();
            cfg.AddProfile<CariHareketProfile>();
        }, NullLoggerFactory.Instance);
        return config.CreateMapper();
    }

    /// <summary>
    /// Gercek, production'da kullanilan TahsilatOdemeBelgesiService.IptalEtAsync akisini kurar -
    /// bu, tahsilat belgesi iptalinin AMBIENT transaction'ini yoneten GERCEK metottur (bkz.
    /// Senaryo 8). Snapshot servisi burada da GERCEK MuhasebeFisService'i kullanir (Fake DEGIL) -
    /// aksi halde ters kayit fisi/bakiye etkisi gercekci sekilde dogrulanamaz.
    /// </summary>
    private static ITahsilatOdemeBelgesiService CreateTahsilatOdemeBelgesiService(StysAppDbContext dbContext, IUserAccessScopeService userAccessScopeService)
    {
        var mapper = CreateMapper();
        var tahsilatRepo = new TahsilatOdemeBelgesiRepository(dbContext, mapper);
        var cariKartRepo = new CariKartRepository(dbContext, mapper);
        var cariHareketRepo = new CariHareketRepository(dbContext, mapper);
        var muhasebeDonemService = CreateMuhasebeDonemService(dbContext);
        var cariHareketKapamaService = new CariHareketKapamaService(
            dbContext, tahsilatRepo, cariHareketRepo, muhasebeDonemService, userAccessScopeService, mapper);

        var posTahsilatValorSnapshotService = new PosTahsilatValorSnapshotService(
            dbContext,
            new ValorTarihHesaplamaService(new NoOpResmiTatilGunuProvider()),
            CreateMuhasebeFisService(dbContext, userAccessScopeService));

        return new TahsilatOdemeBelgesiService(
            tahsilatRepo, cariKartRepo, cariHareketRepo, cariHareketKapamaService,
            dbContext, muhasebeDonemService, userAccessScopeService, posTahsilatValorSnapshotService, mapper);
    }

    private static IMuhasebeDonemService CreateMuhasebeDonemService(StysAppDbContext dbContext)
    {
        var mapper = CreateMapper();
        var repo = new MuhasebeDonemRepository(dbContext, mapper);
        return new MuhasebeDonemService(repo, mapper, dbContext, new FakeMuhasebeTesisScopeService());
    }

    /// <summary>Senaryo 8'de, tahsilat iptalinin GERCEK cari hareket kapama geri-alma yolunu
    /// (CariHareketKapamaService.GeriAlAsync) tetikleyebilmesi icin, tahsilat belgesi olusturulurken
    /// KULLANILAN gercek servis - dogrudan DbContext'e CariHareket eklemek yerine, production'daki
    /// AYNI "fatura hareketi + kapama hareketi" olusturma akisini calistirir.</summary>
    private static ICariHareketKapamaService CreateCariHareketKapamaService(StysAppDbContext dbContext, IUserAccessScopeService userAccessScopeService)
    {
        var mapper = CreateMapper();
        var tahsilatRepo = new TahsilatOdemeBelgesiRepository(dbContext, mapper);
        var cariHareketRepo = new CariHareketRepository(dbContext, mapper);
        var muhasebeDonemService = CreateMuhasebeDonemService(dbContext);
        return new CariHareketKapamaService(dbContext, tahsilatRepo, cariHareketRepo, muhasebeDonemService, userAccessScopeService, mapper);
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

    /// <summary>`dbContextFactory` verilmezse, PosTahsilatValorAktarimService'in kendi ic
    /// duzeltme/cleanup islemleri (bkz. CorrectFisNoSayaciAsync, KosulluGuncelleAsync) icin
    /// interceptor'suz, sade context'ler ureten bir fabrika kullanilir - bu, hata enjeksiyonu
    /// GEREKTIRMEYEN normal senaryo testlerinde (Senaryo 1-9) mevcut davranisi degistirmez.</summary>
    private static IPosTahsilatValorAktarimService CreateAktarimService(
        StysAppDbContext dbContext, IUserAccessScopeService userAccessScopeService, IDbContextFactory<StysAppDbContext>? dbContextFactory = null)
    {
        return new PosTahsilatValorAktarimService(
            dbContext,
            dbContextFactory ?? new TestDbContextFactory(),
            CreateMuhasebeDonemService(dbContext),
            CreateMuhasebeFisService(dbContext, userAccessScopeService),
            userAccessScopeService,
            NullLogger<PosTahsilatValorAktarimService>.Instance);
    }

    /// <summary>Testlerde IDbContextFactory&lt;StysAppDbContext&gt; gerektiren servisler icin -
    /// PosTahsilatValorAktarimService, sayac duzeltmesi (CorrectFisNoSayaciAsync) ve claim
    /// temizleme (KosulluGuncelleAsync) islemlerini AYRI, kisa omurlu context'ler uzerinden yapar.
    /// Verilen interceptor'lar (varsa) URETILEN HER context'e uygulanir - bu, "sayac duzeltmesi
    /// sirasinda ikinci bir hata" gibi senaryolari deterministik olarak enjekte etmeyi saglar.</summary>
    private sealed class TestDbContextFactory : IDbContextFactory<StysAppDbContext>
    {
        private readonly IInterceptor[] _interceptors;
        private readonly string _connectionString;
        private readonly string? _gecikenIlkNCagriIcinBozukConnectionString;
        private readonly int _bozukCagriButcesi;
        private int _cagriSayaci;

        public TestDbContextFactory(string? connectionString = null, params IInterceptor[] interceptors)
        {
            _connectionString = connectionString ?? ConnectionString!;
            _interceptors = interceptors;
        }

        /// <summary>Yalnizca ILK `bozukCagriSayisi` CreateDbContext cagrisinda `bozukConnectionString`
        /// dondurur (gecici baglanti sorunu SIMULASYONU); sonraki cagrilar `saglikliConnectionString`
        /// ile normal calisir. Bu, gercekci bir senaryoyu modeller: CorrectFisNoSayaciAsync'in
        /// KENDI cagrisi baglanti hatasiyla basarisiz olur, ama HEMEN ARDINDAN gelen
        /// KosulluGuncelleAsync cleanup cagrisi (ayni factory uzerinden) BASARILI olur - "sayac
        /// duzeltmesi + cleanup'in TAMAMEN ayni, KALICI olarak cokmus DB'ye bagli oldugu" gercekci
        /// OLMAYAN bir varsayimdan kaçınılır.</summary>
        public TestDbContextFactory(string saglikliConnectionString, string bozukConnectionString, int bozukCagriSayisi)
        {
            _connectionString = saglikliConnectionString;
            _gecikenIlkNCagriIcinBozukConnectionString = bozukConnectionString;
            _bozukCagriButcesi = bozukCagriSayisi;
            _interceptors = [];
        }

        public StysAppDbContext CreateDbContext()
        {
            var connectionString = _connectionString;
            if (_gecikenIlkNCagriIcinBozukConnectionString is not null
                && System.Threading.Interlocked.Increment(ref _cagriSayaci) <= _bozukCagriButcesi)
            {
                connectionString = _gecikenIlkNCagriIcinBozukConnectionString;
            }

            var builder = new DbContextOptionsBuilder<StysAppDbContext>().UseSqlServer(connectionString);
            if (_interceptors.Length > 0)
            {
                builder.AddInterceptors(_interceptors);
            }
            return new StysAppDbContext(builder.Options, new FakeCurrentUserAccessor(), new FakeCurrentTenantAccessor());
        }

        public Task<StysAppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
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
    // Senaryo 2 — Sayac kaydi HENUZ YOKKEN, iki "instance" ayni anda ilk kez
    // olusturmaya calisiyor (deterministik barrier ile eszamanlanir).
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task Senaryo2_SayacYokkenIkiEsZamanliIlkOlusturma_TekSayacSatiriVeBenzersizFisNo()
    {
        await using var seedContext = CreateDbContext();
        var valorId1 = await SeedValorKaydiAsync(seedContext, TesisAId, CariKartAId, KasaBankaPosAId, KasaBankaBankaAId, 500m, 0m, HesapPlaniKomisyonId, "S2-A");
        var valorId2 = await SeedValorKaydiAsync(seedContext, TesisAId, CariKartAId, KasaBankaPosAId, KasaBankaBankaAId, 700m, 0m, HesapPlaniKomisyonId, "S2-B");

        // On-kosul: bu tesis/mali yil icin sayac tablosunda HENUZ hicbir satir yok - "ilk olusturma"
        // yarisini test ediyoruz (Max(FisNo)+1 yaklasiminin en kirilgan oldugu senaryo).
        var sayacVarMi = await seedContext.PosValorFisNoSayaclari.AnyAsync(x => x.TesisId == TesisAId);
        Assert.False(sayacVarMi, "On-kosul ihlali: sayac zaten mevcut, test senaryosu gecersiz.");

        // Deterministik barrier: bir onceki turda kullanilan "cagri ONCESINDEKI gate" yaklasimi
        // GERCEKTEN garanti ETMIYORDU - iki task'in HesabaAktarAsync icinde HANGI noktada
        // (Adim A'nin kendi kilidi, aktif donem sorgusu, vb.) oldugu belirsizdi; gate serbest
        // birakildiginda taraflardan biri sayac SELECT'ine cok once/sonra ulasabilirdi. Bunun
        // yerine GERCEK bir DbCommandInterceptor kullanilir: SQL komut metni incelenip, TAM olarak
        // sayac uzerindeki kilitli SELECT (WITH UPDLOCK/ROWLOCK/HOLDLOCK) TAMAMLANDIKTAN SONRA (ama
        // INSERT'ten ONCE) her iki taraf da bariyerde durdurulur, ikisi de hazir olunca AYNI ANDA
        // serbest birakilir - "sayac henuz yokken" fantom-satir yarisi boylece gercekten garanti
        // edilir.
        using var gate = new SemaphoreSlim(0, 2);
        using var hazirSayaci = new CountdownEvent(2);
        var interceptor1 = new SayacSelectBarrierInterceptor(gate, hazirSayaci);
        var interceptor2 = new SayacSelectBarrierInterceptor(gate, hazirSayaci);

        await using var ctx1 = CreateDbContext(interceptor1);
        await using var ctx2 = CreateDbContext(interceptor2);
        var scope = new FakeUserAccessScopeService(DomainAccessScope.Unscoped());
        var svc1 = CreateAktarimService(ctx1, scope);
        var svc2 = CreateAktarimService(ctx2, scope);

        var task1 = Task.Run(() => svc1.HesabaAktarAsync(valorId1, null, CancellationToken.None));
        var task2 = Task.Run(() => svc2.HesabaAktarAsync(valorId2, null, CancellationToken.None));

        // Iki taraf da sayac SELECT'ini tamamlayip bariyerde durana kadar bekle (10 sn icinde
        // gerceklesmezse test zaman asimina ugrar - "her iki taraf da sayac SELECT'ine ulasti"
        // varsayimi GERCEKTEN dogrulanmis olur, varsayilmaz).
        var ikisiDeHazir = hazirSayaci.Wait(TimeSpan.FromSeconds(10));
        if (!ikisiDeHazir)
        {
            gate.Release(2);
            var tanilama = string.Join(" | ", interceptor1.GorulenKomutlar.Concat(interceptor2.GorulenKomutlar)
                .Select(x => x.Replace('\n', ' ').Replace('\r', ' ')).Take(20));
            Assert.Fail($"On-kosul ihlali: iki taraf da beklenen surede sayac SELECT'ine ulasmadi. Gorulen komutlar: {tanilama}");
        }
        gate.Release(2);

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

        // Sayac tablosunda bu tesis/mali yil icin TEK bir (soft-delete edilmemis) satir olmali -
        // eszamanli iki "ilk olusturma" denemesi ikinci bir sayac satiri URETMEMIS olmali.
        var sayacSatirSayisi = await verifyContext.PosValorFisNoSayaclari
            .CountAsync(x => x.TesisId == TesisAId && !x.IsDeleted);
        Assert.Equal(1, sayacSatirSayisi);

        var sonNumara = await verifyContext.PosValorFisNoSayaclari
            .Where(x => x.TesisId == TesisAId && !x.IsDeleted)
            .Select(x => x.SonNumara)
            .SingleAsync();
        Assert.Equal(2, sonNumara); // iki fis uretildi -> sayac 2'de olmali
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

    /// <summary>Yalnizca YARIS KAYBININ beklenen sonucu olan 409 ("islem baska bir surec tarafindan
    /// devralinmis" / "duzeltme/ters kayit isleminin surdugu") yutulur. Baska bir BaseException
    /// (500 veri tutarsizligi, 422 girdi hatasi, 403 yetki vb.) KOŞULSUZ YUTULMAZ - test hatasi
    /// olarak yukari firlatilir; bu, testin gercek bir hatayi "beklenen yaris kaybi" sanip
    /// gizlemesini engeller.</summary>
    private static async Task SafeDuzeltmeAsync(IPosTahsilatValorAktarimService svc, int valorId, string aciklama)
    {
        try
        {
            await svc.DuzeltmeTersKayitAsync(valorId, aciklama, CancellationToken.None);
        }
        catch (BaseException ex) when (ex.ErrorCode == 409)
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

        // GERCEK cari hareket + cari kapama zinciri kurulur: once acik bir "fatura" hareketi
        // (musteri borcu, KalanTutar=250) eklenir, sonra tahsilat belgesi bu harekete baglanip
        // CariHareketKapamaService ile GERCEKTEN kapatilir (yeni bir kapama CariHareket'i
        // olusturulur, fatura hareketinin KalanTutar/KapandiMi alanlari guncellenir) - boylece
        // iptal yarisinin cari hareket/kapama GERI ALMA davranisi GERCEK verilerle dogrulanabilir.
        var scope = new FakeUserAccessScopeService(DomainAccessScope.Unscoped());
        int faturaHareketId;
        await using (var setupCtx = CreateDbContext())
        {
            var faturaHareket = new CariHareket
            {
                CariKartId = CariKartAId,
                HareketTarihi = DateTime.UtcNow.Date,
                BelgeTuru = "Fatura",
                BelgeNo = $"{_uniqueSuffix}-S8-FATURA",
                BorcTutari = 250m,
                AlacakTutari = 0m,
                KapananTutar = 0m,
                KalanTutar = 250m,
                ParaBirimi = "TRY",
                Durum = CariHareketDurumlari.Aktif,
                KapandiMi = false
            };
            setupCtx.CariHareketler.Add(faturaHareket);
            await setupCtx.SaveChangesAsync();
            faturaHareketId = faturaHareket.Id;

            var belgeEntity = await setupCtx.TahsilatOdemeBelgeleri.SingleAsync(x => x.Id == belgeId);
            belgeEntity.KapatilacakCariHareketId = faturaHareketId;
            await setupCtx.SaveChangesAsync();

            var kapamaSvc = CreateCariHareketKapamaService(setupCtx, scope);
            await kapamaSvc.TahsilatOdemeIcinCariHareketOlusturVeKapatAsync(belgeId, CancellationToken.None);
        }

        // Deterministik bariyer: HesabaAktarAsync'in Adim A'si ve PosTahsilatValorSnapshotService.
        // IptalEtAsync'in KENDI kilitli SELECT'i, AYNI PosTahsilatValorleri satiri uzerinde GERCEK
        // UPDLOCK cakismasina girer - production'daki tam kritik kilit noktasi budur. Bariyer, her
        // iki tarafin da bu SELECT'i SQL Server'a gondermeden HEMEN once senkronize olmasini
        // saglar; boylece "hangi tarafin once basladigi" rastgeleligine BAGLI KALINMAZ - yalnizca
        // GERCEK satir kilidi rekabeti sonucu belirler (Task.WhenAll TEK BASINA bunu garanti etmez).
        using var gate = new SemaphoreSlim(0, 2);
        using var hazirSayaci = new CountdownEvent(2);
        var aktarimInterceptor = new PosValorSelectBarrierInterceptor(gate, hazirSayaci);
        var iptalInterceptor = new PosValorSelectBarrierInterceptor(gate, hazirSayaci);

        await using var ctx1 = CreateDbContext(aktarimInterceptor);
        await using var ctx2 = CreateDbContext(iptalInterceptor);
        var aktarimSvc = CreateAktarimService(ctx1, scope);
        // ONEMLI: dogrudan PosTahsilatValorSnapshotService.IptalEtAsync DEGIL, production'da
        // tahsilat iptalinin AMBIENT transaction'ini yoneten GERCEK giris noktasi
        // (TahsilatOdemeBelgesiService.IptalEtAsync) cagrilir - bu, cari hareket kapamasinin geri
        // alinmasi + POS valor snapshot iptalinin AYNI transaction icinde, gercek uretim akisiyla
        // birebir calistigini dogrular.
        var tahsilatSvc = CreateTahsilatOdemeBelgesiService(ctx2, scope);

        // Yalnizca YARISTA KAYBETMENIN beklenen sonucu olan hata kodlari yutulur (404: kayit henuz
        // gorulemedi/race; 409: "aktarim/iptal surdugu icin simdi yapilamaz"; 422: valor tarihi/
        // girdi kontrolu). Baska HERHANGI bir BaseException (ornegin 500 veri tutarsizligi, 403
        // yetki) test hatasi olarak YUKARI FIRLATILIR - koşulsuz yutma yapilmaz.
        var aktarimTask = Task.Run(() => SafeCallAsync(() => aktarimSvc.HesabaAktarAsync(valorId, null, CancellationToken.None), 404, 409, 422));
        var iptalTask = Task.Run(() => SafeCallAsync(() => tahsilatSvc.IptalEtAsync(belgeId, CancellationToken.None), 404, 409, 422));

        var ikisiDeHazir = hazirSayaci.Wait(TimeSpan.FromSeconds(10));
        gate.Release(2);
        Assert.True(ikisiDeHazir, "On-kosul ihlali: iki taraf da beklenen surede kilitli SELECT'e ulasmadi.");

        await Task.WhenAll(aktarimTask, iptalTask);

        await using var verifyContext = CreateDbContext();
        var valor = await verifyContext.PosTahsilatValorleri.SingleAsync(x => x.Id == valorId);

        // Tahsilat belgesinin durumu, valor'un nihai durumuyla IKI YONLU tutarli olmali:
        // Aktarildi ise belge HALA Aktif olmali (iptal yarisi kaybetti/hic tetiklenmedi); Iptal ise
        // belge de KESINLIKLE Iptal olmali. Onceki tek yonlu kontrol (yalnizca Iptal dalinda
        // dogrulama) belge Aktarildi dalinda YANLISLIKLA Iptal kalmis olsa bile fark etmezdi.
        var belge = await verifyContext.TahsilatOdemeBelgeleri.SingleAsync(x => x.Id == belgeId);
        Assert.Equal(
            valor.Durum == PosTahsilatValorDurumlari.Iptal ? TahsilatOdemeBelgeDurumlari.Iptal : TahsilatOdemeBelgeDurumlari.Aktif,
            belge.Durum);

        // Cari hareket/kapama zinciri de valor'un nihai durumuyla tutarli olmali: iptal kazandiysa
        // kapama hareketi GERI ALINMIS (Durum=Iptal, iliski koparilmis) ve fatura hareketinin
        // KalanTutar'i ORIJINAL degerine (250) DONMUS olmali; aktarim kazandiysa (iptal hic
        // etkilemedi) kapama hareketi hala Aktif ve fatura hareketi hala KAPALI (KalanTutar=0)
        // kalmis olmali - hicbiri "yarim" durumda kalmamali.
        var faturaHareketSonHali = await verifyContext.CariHareketler.SingleAsync(x => x.Id == faturaHareketId);
        var kapamaHareketSonHali = await verifyContext.CariHareketler.SingleAsync(x =>
            x.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi && x.KaynakId == belgeId);

        if (valor.Durum == PosTahsilatValorDurumlari.Iptal)
        {
            Assert.Equal(CariHareketDurumlari.Iptal, kapamaHareketSonHali.Durum);
            Assert.Null(kapamaHareketSonHali.IliskiliCariHareketId);
            Assert.Equal(250m, faturaHareketSonHali.KalanTutar);
            Assert.False(faturaHareketSonHali.KapandiMi);
        }
        else
        {
            Assert.Equal(CariHareketDurumlari.Aktif, kapamaHareketSonHali.Durum);
            Assert.Equal(0m, faturaHareketSonHali.KalanTutar);
            Assert.True(faturaHareketSonHali.KapandiMi);
        }

        // Tutarlilik: kayit ya basariyla aktarilmis (Aktarildi) ya da iptal edilmis (Iptal) olmali;
        // hicbir zaman "Aktariliyor"da takili kalmamali ve ClaimToken bos olmali.
        Assert.Contains(valor.Durum, new[] { PosTahsilatValorDurumlari.Aktarildi, PosTahsilatValorDurumlari.Iptal });
        Assert.Null(valor.ClaimToken);

        if (valor.Durum == PosTahsilatValorDurumlari.Aktarildi)
        {
            // Aktarim yarisi kazandi: tam olarak bir Onayli transfer fisi olmali (Taslak degil -
            // bkz. duzeltme #1), tahsilat iptali bu fisi henuz gormemis/etkilememis olmali.
            Assert.NotNull(valor.MuhasebeFisId);
            var fis = await verifyContext.MuhasebeFisler.SingleAsync(x => x.Id == valor.MuhasebeFisId);
            Assert.Equal(MuhasebeFisDurumlari.Onayli, fis.Durum);

            var fisSayisi = await verifyContext.MuhasebeFisler.CountAsync(x =>
                x.KaynakModul == MuhasebeKaynakModulleri.PosTahsilatValorTransferi && x.KaynakId == valorId);
            Assert.Equal(1, fisSayisi);
        }
        else // valor.Durum == Iptal
        {
            // Iptal yarisi kazandi (ya da aktarimdan sonra iptal tetiklendi). Iki alt-durum var:
            if (valor.MuhasebeFisId is null)
            {
                // (a) Iptal, transfer fisi hic olusmadan once devreye girdi - aktif/Onayli hicbir
                // transfer fisi KESINLIKLE olusmamis olmali.
                var olusmusFisSayisi = await verifyContext.MuhasebeFisler.CountAsync(x =>
                    x.KaynakModul == MuhasebeKaynakModulleri.PosTahsilatValorTransferi && x.KaynakId == valorId);
                Assert.Equal(0, olusmusFisSayisi);
            }
            else
            {
                // (b) Aktarim ONCE tamamlandi (fis Onayli oldu), SONRA iptal onu ters kayitla geri
                // aldi (TahsilatOdemeBelgesiService -> PosTahsilatValorSnapshotService.IptalEtAsync
                // -> MuhasebeFisService.PosValorTransferFisiniIptalEtAsync). Orijinal fis artik
                // AKTIF/Onayli KALMAMALI; tam olarak bir karsit (TersKayit) fis olusmus olmali ve
                // iliski dogru kurulmus olmali.
                var orijinalFis = await verifyContext.MuhasebeFisler.SingleAsync(x => x.Id == valor.MuhasebeFisId);
                Assert.Equal(MuhasebeFisDurumlari.Iptal, orijinalFis.Durum); // aktif Onayli birakilmadi

                Assert.NotNull(valor.TersKayitMuhasebeFisId);
                var tersFis = await verifyContext.MuhasebeFisler.SingleAsync(x => x.Id == valor.TersKayitMuhasebeFisId);
                Assert.Equal(MuhasebeFisDurumlari.TersKayit, tersFis.Durum);
                Assert.Equal(orijinalFis.Id, tersFis.IptalEdilenFisId);
                Assert.Equal(tersFis.Id, orijinalFis.TersKayitFisId);

                var tersKayitSayisi = await verifyContext.MuhasebeFisler.CountAsync(x => x.IptalEdilenFisId == orijinalFis.Id);
                Assert.Equal(1, tersKayitSayisi); // tam olarak BIR ters kayit

                // Bakiye etkisi geri alinmis olmali: orijinal fis + ters kayit fisinin POS (109) ve
                // Banka (102) hesaplarina toplam net etkisi sifir olmalidir (bu izole test tesisinde
                // baska hicbir fis bu hesaplara dokunmuyor).
                var posNetBakiye = await verifyContext.MuhasebeHesapBakiyeleri
                    .Where(x => x.TesisId == TesisAId && x.MuhasebeHesapPlaniId == HesapPlaniPosId)
                    .SumAsync(x => x.NetBakiye);
                var bankaNetBakiye = await verifyContext.MuhasebeHesapBakiyeleri
                    .Where(x => x.TesisId == TesisAId && x.MuhasebeHesapPlaniId == HesapPlaniBankaId)
                    .SumAsync(x => x.NetBakiye);
                Assert.Equal(0m, posNetBakiye);
                Assert.Equal(0m, bankaNetBakiye);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 9 — Legacy fis (2026-VLR-000001) varken sayac tablosu bos: migration/gecis senaryosu.
    // Yeni bir aktarim, sayac HENUZ yokken bile GERCEK MuhasebeFisler verisine bakarak benzersiz,
    // >=000002 bir numarayla basariyla tamamlanmali (bkz. GenerateFisNoAsync'in "sayac is null" dali).
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task Senaryo9_LegacyFisVarkenSayacBos_YeniAktarimBenzersizNumarayiUretir()
    {
        await using var seedContext = CreateDbContext();

        var legacyFis = new MuhasebeFis
        {
            TesisId = TesisAId,
            MaliYil = 2026,
            Donem = 1,
            FisNo = "2026-VLR-000001",
            FisTarihi = DateTime.UtcNow.Date,
            FisTipi = MuhasebeFisTipleri.Mahsup,
            KaynakModul = MuhasebeKaynakModulleri.PosTahsilatValorTransferi,
            KaynakId = -1, // gercek bir valor kaydina bagli degil - yalnizca sayac hesaplamasini test eder
            Durum = MuhasebeFisDurumlari.Onayli,
            ToplamBorc = 500m,
            ToplamAlacak = 500m
        };
        seedContext.MuhasebeFisler.Add(legacyFis);
        await seedContext.SaveChangesAsync();

        // On-kosul: sayac tablosunda bu tesis/mali yil icin HENUZ kayit yok.
        var sayacVarMi = await seedContext.PosValorFisNoSayaclari.AnyAsync(x => x.TesisId == TesisAId && x.MaliYil == 2026);
        Assert.False(sayacVarMi, "On-kosul ihlali: sayac zaten mevcut, test senaryosu gecersiz.");

        var valorId = await SeedValorKaydiAsync(seedContext, TesisAId, CariKartAId, KasaBankaPosAId, KasaBankaBankaAId, 600m, 0m, HesapPlaniKomisyonId, "S9");

        await using var ctx = CreateDbContext();
        var scope = new FakeUserAccessScopeService(DomainAccessScope.Unscoped());
        var svc = CreateAktarimService(ctx, scope);

        var sonuc = await svc.HesabaAktarAsync(valorId, null, CancellationToken.None);
        Assert.True(sonuc.Basarili, sonuc.HataMesaji);

        await using var verifyContext = CreateDbContext();
        var yeniFis = await verifyContext.MuhasebeFisler.SingleAsync(x => x.Id == sonuc.MuhasebeFisId);
        Assert.NotEqual("2026-VLR-000001", yeniFis.FisNo);
        Assert.Matches(@"^2026-VLR-\d{6}$", yeniFis.FisNo);

        var yeniSira = int.Parse(yeniFis.FisNo[^6..]);
        Assert.True(yeniSira >= 2, $"Beklenen: >=2 (legacy 000001'i takip etmeli), Gerçek: {yeniSira} (FisNo={yeniFis.FisNo})");

        var sayacSonNumara = await verifyContext.PosValorFisNoSayaclari
            .Where(x => x.TesisId == TesisAId && x.MaliYil == 2026 && !x.IsDeleted)
            .Select(x => x.SonNumara)
            .SingleAsync();
        Assert.Equal(yeniSira, sayacSonNumara);
    }

    /// <summary>HesabaAktarAsync/IptalEtAsync gibi eszamanlilik testlerinde cagrilan islemler,
    /// yarisi kaybettiklerinde BEKLENEN hata kodlariyla (ornegin 409 "su an yapilamaz") basarisiz
    /// olabilir - bunlar yutulur. Ancak listede OLMAYAN bir ErrorCode (ornegin 500 veri tutarsizligi,
    /// 403 yetkisiz erisim) KOŞULSUZ YUTULMAZ, oldugu gibi yeniden firlatilir - bu, testin gercek
    /// bir hatayi "beklenen yaris kaybi" sanip gizlemesini engeller.</summary>
    private static async Task SafeCallAsync(Func<Task> action, params int[] beklenenHataKodlari)
    {
        try
        {
            await action();
        }
        catch (BaseException ex) when (beklenenHataKodlari.Contains(ex.ErrorCode))
        {
            // Beklenen yaris-kaybi hatasi - yutulur.
        }
    }

    /// <summary>HesabaAktarAsync, claim asamasinda kaybeden taraf icin bir sonuc DTO'su DEGIL,
    /// dogrudan BaseException firlatir (Adim A'da "uygun" degilse hemen throw). Bu yardimci,
    /// eszamanlilik testlerinde her iki tarafi da tekdüze bir sonuca (Basarili/HataMesaji)
    /// indirger. Yalnizca 409 (claim/durum yarisi) beklenen hatadir - baska bir kod gelirse test
    /// hatasi olarak yukari firlatilir.</summary>
    private static async Task<PosTahsilatValorAktarimSonucDto> TryAktarAsync(IPosTahsilatValorAktarimService svc, int valorId)
    {
        try
        {
            return await svc.HesabaAktarAsync(valorId, null, CancellationToken.None);
        }
        catch (BaseException ex) when (ex.ErrorCode == 409)
        {
            return new PosTahsilatValorAktarimSonucDto { Id = valorId, Basarili = false, HataMesaji = ex.Message };
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 10 — Sayac duzeltmesi SIRASINDA ikinci bir GERCEK unique catisma enjekte edilir.
    // Kayit Aktariliyor/ClaimToken dolu KALMAMALI (bkz. round-5 bulgu #1/#2).
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task Senaryo10_SayacDuzeltmesiSirasindaIkinciUniqueCakisma_KayitTakiliKalmaz()
    {
        const int maliYil = 2026;
        await using var seedContext = CreateDbContext();
        var valorId = await SeedValorKaydiAsync(seedContext, TesisAId, CariKartAId, KasaBankaPosAId, KasaBankaBankaAId, 300m, 0m, HesapPlaniKomisyonId, "S10");

        var sayacVarMi = await seedContext.PosValorFisNoSayaclari.AnyAsync(x => x.TesisId == TesisAId && x.MaliYil == maliYil);
        Assert.False(sayacVarMi, "On-kosul ihlali: sayac zaten mevcut, test senaryosu gecersiz.");

        // 1) Adim B'nin AMBIENT context'inde: GenerateFisNoAsync sayacI ILK KEZ olusturmaya
        //    calisirken (Added PosValorFisNoSayac tespit edildiginde), RAW/ayri bir baglantiyla
        //    ayni anahtarli (TesisId, MaliYil) satiri once BIZ ekleyip commit ediyoruz - bu, EF'in
        //    kendi INSERT'inin GERCEK bir unique index catismasiyla basarisiz olmasini saglar
        //    (PosValorAdimBYenidenDenemeException firlar).
        var ambientInterceptor = new SayacInsertOncesiRawSideEffectInterceptor(() => RawSayacInsertSil(TesisAId, maliYil, insert: true));

        // 2) HesabaAktarAsync bunu yakalayip CorrectFisNoSayaciAsync'i AYRI bir factory-context'te
        //    cagirir. O context'e iki interceptor takiliyor: (a) kendi SELECT'inden HEMEN once,
        //    adim 1'de eklenen satiri RAW olarak SILEN bir interceptor - boylece duzeltme de
        //    "sayac yok" durumuyla karsilasip INSERT dalina girer; (b) duzeltmenin KENDI INSERT'i
        //    calismadan hemen once, AYNI satiri TEKRAR RAW olarak ekleyen bir interceptor - boylece
        //    duzeltmenin KENDI INSERT'i de GERCEK bir ikinci unique catismasina ugrar.
        var selectOncesiSilInterceptor = new SayacSelectOncesiRawSideEffectInterceptor(() => RawSayacInsertSil(TesisAId, maliYil, insert: false));
        var insertOncesiTekrarEkleInterceptor = new SayacInsertOncesiRawSideEffectInterceptor(() => RawSayacInsertSil(TesisAId, maliYil, insert: true));
        var correctionFactory = new TestDbContextFactory(null, selectOncesiSilInterceptor, insertOncesiTekrarEkleInterceptor);

        await using var ctx = CreateDbContext(ambientInterceptor);
        var scope = new FakeUserAccessScopeService(DomainAccessScope.Unscoped());
        var svc = CreateAktarimService(ctx, scope, correctionFactory);

        var sonuc = await svc.HesabaAktarAsync(valorId, null, CancellationToken.None);

        Assert.False(sonuc.Basarili);

        await using var verifyContext = CreateDbContext();
        var valor = await verifyContext.PosTahsilatValorleri.SingleAsync(x => x.Id == valorId);
        Assert.Equal(PosTahsilatValorDurumlari.Hata, valor.Durum);
        Assert.Null(valor.ClaimToken);
        Assert.Null(valor.AktarimBaslamaTarihi);

        // Test tarafinca RAW olarak eklenen satiri temizle (test verisi).
        RawSayacInsertSil(TesisAId, maliYil, insert: false);
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 11 — Sayac duzeltmesi SIRASINDA iptal (cancellation) enjekte edilir. Kayit
    // Aktariliyor/ClaimToken dolu KALMAMALI.
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task Senaryo11_SayacDuzeltmesiSirasindaIptal_KayitTakiliKalmaz()
    {
        const int maliYil = 2026;
        await using var seedContext = CreateDbContext();
        var valorId = await SeedValorKaydiAsync(seedContext, TesisAId, CariKartAId, KasaBankaPosAId, KasaBankaBankaAId, 300m, 0m, HesapPlaniKomisyonId, "S11");

        // FisNo cakismasini deterministik olarak tetiklemek icin: sayaci "SonNumara=1" ile
        // onceden olustur, ayni tesis/yil icin FisNo="2026-VLR-000002" olan bir "yabanci" fis ekle -
        // GenerateFisNoAsync bir sonraki numarayi (2) uretecek ve bu fisle CAKISACAK.
        await using (var setupCtx = CreateDbContext())
        {
            setupCtx.PosValorFisNoSayaclari.Add(new PosValorFisNoSayac { TesisId = TesisAId, MaliYil = maliYil, SonNumara = 1 });
            setupCtx.MuhasebeFisler.Add(new MuhasebeFis
            {
                TesisId = TesisAId, MaliYil = maliYil, Donem = 1, FisNo = $"{maliYil}-VLR-000002",
                FisTarihi = DateTime.UtcNow.Date, FisTipi = MuhasebeFisTipleri.Mahsup,
                KaynakModul = MuhasebeKaynakModulleri.PosTahsilatValorTransferi, KaynakId = -911,
                Durum = MuhasebeFisDurumlari.Onayli, ToplamBorc = 10m, ToplamAlacak = 10m
            });
            await setupCtx.SaveChangesAsync();
        }

        using var iptalCts = new CancellationTokenSource();
        // CorrectFisNoSayaciAsync'in KENDI SELECT'i tam calismadan hemen once token iptal edilir -
        // boylece CorrectFisNoSayaciAsync icinde GERCEK bir OperationCanceledException olusur.
        var iptalInterceptor = new SayacSelectOncesiRawSideEffectInterceptor(() => iptalCts.Cancel());
        var correctionFactory = new TestDbContextFactory(null, iptalInterceptor);

        await using var ctx = CreateDbContext();
        var scope = new FakeUserAccessScopeService(DomainAccessScope.Unscoped());
        var svc = CreateAktarimService(ctx, scope, correctionFactory);

        var sonuc = await svc.HesabaAktarAsync(valorId, null, iptalCts.Token);

        Assert.False(sonuc.Basarili);

        await using var verifyContext = CreateDbContext();
        var valor = await verifyContext.PosTahsilatValorleri.SingleAsync(x => x.Id == valorId);
        Assert.Equal(PosTahsilatValorDurumlari.Hata, valor.Durum);
        Assert.Null(valor.ClaimToken);
        Assert.Null(valor.AktarimBaslamaTarihi);
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 12 — Sayac duzeltmesi SIRASINDA baglanti hatasi enjekte edilir (gecersiz sunucuya
    // isaret eden bir factory). Kayit Aktariliyor/ClaimToken dolu KALMAMALI.
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task Senaryo12_SayacDuzeltmesiSirasindaBaglantiHatasi_KayitTakiliKalmaz()
    {
        const int maliYil = 2026;
        await using var seedContext = CreateDbContext();
        var valorId = await SeedValorKaydiAsync(seedContext, TesisAId, CariKartAId, KasaBankaPosAId, KasaBankaBankaAId, 300m, 0m, HesapPlaniKomisyonId, "S12");

        await using (var setupCtx = CreateDbContext())
        {
            setupCtx.PosValorFisNoSayaclari.Add(new PosValorFisNoSayac { TesisId = TesisAId, MaliYil = maliYil, SonNumara = 1 });
            setupCtx.MuhasebeFisler.Add(new MuhasebeFis
            {
                TesisId = TesisAId, MaliYil = maliYil, Donem = 1, FisNo = $"{maliYil}-VLR-000002",
                FisTarihi = DateTime.UtcNow.Date, FisTipi = MuhasebeFisTipleri.Mahsup,
                KaynakModul = MuhasebeKaynakModulleri.PosTahsilatValorTransferi, KaynakId = -912,
                Durum = MuhasebeFisDurumlari.Onayli, ToplamBorc = 10m, ToplamAlacak = 10m
            });
            await setupCtx.SaveChangesAsync();
        }

        // CorrectFisNoSayaciAsync'in AYRI context'i icin ERISILEMEZ bir sunucuya isaret eden,
        // yalnizca ILK cagrida (sayac duzeltmesinin kendisinde) baglanti hatasi ureten bir factory -
        // hemen ardindan gelen KosulluGuncelleAsync cleanup cagrisi (AYNI factory'nin 2. cagrisi)
        // saglikli baglantiya doner ve basariyla temizler. Bu, gercek uretimde tek bir factory'nin
        // GECICI bir baglanti sorunu yasadigi (kalici cokme DEGIL) gercekci senaryoyu modeller.
        var hataliConnectionString = System.Text.RegularExpressions.Regex.Replace(ConnectionString!, @"Server=[^;]+", "Server=127.0.0.1,1") + ";Connect Timeout=3";
        var correctionFactory = new TestDbContextFactory(ConnectionString!, hataliConnectionString, bozukCagriSayisi: 1);

        await using var ctx = CreateDbContext();
        var scope = new FakeUserAccessScopeService(DomainAccessScope.Unscoped());
        var svc = CreateAktarimService(ctx, scope, correctionFactory);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var sonuc = await svc.HesabaAktarAsync(valorId, null, timeoutCts.Token);

        Assert.False(sonuc.Basarili);

        await using var verifyContext = CreateDbContext();
        var valor = await verifyContext.PosTahsilatValorleri.SingleAsync(x => x.Id == valorId);
        Assert.Equal(PosTahsilatValorDurumlari.Hata, valor.Durum);
        Assert.Null(valor.ClaimToken);
        Assert.Null(valor.AktarimBaslamaTarihi);
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 13 — Sayac duzeltmesi SIRASINDA "deadlock benzeri" beklenmeyen bir istisna enjekte
    // edilir. IsDeadlock'un tam SqlException(1205) tipini eslestirmesi gercek bir SQL Server
    // deadlock'u (veya SqlClient'a reflection ile mudahaleyi) gerektirdigi icin, bu test yerine
    // CorrectFisNoSayaciAsync'in genel GUVENLIK AGINI (herhangi bir istisna turunde kaydin
    // takili KALMAMASI) dogrular - HesabaAktarAsync'teki nested catch (Exception correctEx) ozel
    // olarak exception TURUNE bakmadan calisir, dolayisiyla bu test o genel yolu dogru sekilde
    // kapsar.
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task Senaryo13_SayacDuzeltmesiSirasindaBeklenmeyenIstisna_KayitTakiliKalmaz()
    {
        const int maliYil = 2026;
        await using var seedContext = CreateDbContext();
        var valorId = await SeedValorKaydiAsync(seedContext, TesisAId, CariKartAId, KasaBankaPosAId, KasaBankaBankaAId, 300m, 0m, HesapPlaniKomisyonId, "S13");

        await using (var setupCtx = CreateDbContext())
        {
            setupCtx.PosValorFisNoSayaclari.Add(new PosValorFisNoSayac { TesisId = TesisAId, MaliYil = maliYil, SonNumara = 1 });
            setupCtx.MuhasebeFisler.Add(new MuhasebeFis
            {
                TesisId = TesisAId, MaliYil = maliYil, Donem = 1, FisNo = $"{maliYil}-VLR-000002",
                FisTarihi = DateTime.UtcNow.Date, FisTipi = MuhasebeFisTipleri.Mahsup,
                KaynakModul = MuhasebeKaynakModulleri.PosTahsilatValorTransferi, KaynakId = -913,
                Durum = MuhasebeFisDurumlari.Onayli, ToplamBorc = 10m, ToplamAlacak = 10m
            });
            await setupCtx.SaveChangesAsync();
        }

        var istisnaInterceptor = new SayacSelectOncesiRawSideEffectInterceptor(() =>
            throw new InvalidOperationException("Test: sayaç düzeltmesi sırasında deadlock benzeri beklenmeyen bir hata simüle edildi."));
        var correctionFactory = new TestDbContextFactory(null, istisnaInterceptor);

        await using var ctx = CreateDbContext();
        var scope = new FakeUserAccessScopeService(DomainAccessScope.Unscoped());
        var svc = CreateAktarimService(ctx, scope, correctionFactory);

        var sonuc = await svc.HesabaAktarAsync(valorId, null, CancellationToken.None);

        Assert.False(sonuc.Basarili);

        await using var verifyContext = CreateDbContext();
        var valor = await verifyContext.PosTahsilatValorleri.SingleAsync(x => x.Id == valorId);
        Assert.Equal(PosTahsilatValorDurumlari.Hata, valor.Durum);
        Assert.Null(valor.ClaimToken);
        Assert.Null(valor.AktarimBaslamaTarihi);
    }

    /// <summary>SavingChanges anında, verilen context'in "eklenmekte olan" (Added) bir
    /// PosValorFisNoSayac icerip icermedigini kontrol eder; iceriyorsa (yani INSERT'e hazirlaniyorsa)
    /// verilen side-effect'i CALISTIRIR. Bu, sayac satirinin ilk kez olusturulmasi sirasinda GERCEK
    /// bir SQL Server unique-index catismasini deterministik olarak enjekte etmek icin kullanilir.</summary>
    private sealed class SayacInsertOncesiRawSideEffectInterceptor(Action sideEffect) : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            if (SayacEklemesiVarMi(eventData)) { sideEffect(); }
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (SayacEklemesiVarMi(eventData)) { sideEffect(); }
            return new ValueTask<InterceptionResult<int>>(result);
        }

        private static bool SayacEklemesiVarMi(DbContextEventData eventData) =>
            eventData.Context is not null && eventData.Context.ChangeTracker.Entries<PosValorFisNoSayac>().Any(e => e.State == EntityState.Added);
    }

    /// <summary>Sayac uzerindeki kilitli SELECT (WITH UPDLOCK) SQL Server'a gonderilmeden HEMEN
    /// once (yalnizca ILK cagrida) verilen side-effect'i calistirir. "Duzeltme kendi SELECT'ini
    /// yapmadan once dis bir etken (raw silme/ekleme, token iptali, beklenmeyen istisna) araya
    /// giriyor" senaryolarini deterministik olarak enjekte etmek icin kullanilir.</summary>
    private sealed class SayacSelectOncesiRawSideEffectInterceptor(Action sideEffect) : DbCommandInterceptor
    {
        private bool _tetiklendi;

        public override async ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command,
            CommandEventData eventData,
            InterceptionResult<System.Data.Common.DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (!_tetiklendi
                && command.CommandText.Contains("PosValorFisNoSayaclari", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("UPDLOCK", StringComparison.OrdinalIgnoreCase))
            {
                _tetiklendi = true;
                sideEffect();
            }

            return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    /// <summary>RAW (EF disi) bir SqlConnection ile PosValorFisNoSayaclari tablosuna dogrudan
    /// INSERT/DELETE yapar - fault-injection testlerinde, EF'in ic degisiklik izlemesine dahil
    /// OLMADAN gercek bir eszamanli/rakip satir olusturmak/kaldirmak icin kullanilir.</summary>
    private static void RawSayacInsertSil(int tesisId, int maliYil, bool insert)
    {
        using var conn = new Microsoft.Data.SqlClient.SqlConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        if (insert)
        {
            cmd.CommandText = """
                INSERT INTO [muhasebe].[PosValorFisNoSayaclari] ([TesisId],[MaliYil],[SonNumara],[IsDeleted],[CreatedAt],[UpdatedAt],[CreatedBy],[UpdatedBy])
                VALUES (@tesisId, @maliYil, 99, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), N'test_raw_side_effect', N'test_raw_side_effect')
                """;
        }
        else
        {
            cmd.CommandText = "DELETE FROM [muhasebe].[PosValorFisNoSayaclari] WHERE [TesisId] = @tesisId AND [MaliYil] = @maliYil";
        }
        cmd.Parameters.AddWithValue("@tesisId", tesisId);
        cmd.Parameters.AddWithValue("@maliYil", maliYil);
        cmd.ExecuteNonQuery();
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
