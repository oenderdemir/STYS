using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using STYS.Infrastructure.EntityFramework;
using STYS.Infrastructure.EntityFramework.Migrations;
using STYS.Muhasebe.SatisBelgeleri;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.MuhasebeDonemleri.Entities;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Services;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// "Expand ve compatibility projection" aşamasının (SatisBelgesiDurumProjection + AddSeparatedSatisBelgesiStatuses
/// migration'ı + SatisBelgesiService'teki dual-write) GERÇEK SQL Server üzerinde doğrulandığı
/// entegrasyon testleri. Bu turda SatisBelgesiDurumu HÂLÂ otoriter karar kaynağıdır - testler bunu
/// (mevcut kabul/ret davranışının değişmediğini) de doğrular; üç yeni alan yalnızca BİRLİKTE
/// yazılan, henüz okunmayan projeksiyon alanlarıdır.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class SatisBelgesiDurumAyrimiIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "DURUMAYR-882";

    private string _uniqueSuffix = TestMarker;
    private int _kurumId;
    private int _ilId;
    private int _tesisId;
    private int _musteriKartId;
    private int _tedarikciKartId;

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
        var kdvAlisHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.KDVIndirilecek, "KDVA", _tesisId);
        var giderHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.GiderHizmetMaliyet, "GIDER", _tesisId);
        var musteriHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "MUS", _tesisId);
        var tedarikciHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "TED", _tesisId);
        dbContext.MuhasebeHesapPlanlari.AddRange(gelirHesap, kdvSatisHesap, kdvAlisHesap, giderHesap, musteriHesap, tedarikciHesap);
        await dbContext.SaveChangesAsync();

        var musteriKart = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "MUS", CariKartTipleri.Musteri, _tesisId, musteriHesap.Id);
        var tedarikciKart = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "TED", CariKartTipleri.Tedarikci, _tesisId, tedarikciHesap.Id);
        tedarikciKart.VergiNoTckn = "1111111111";
        dbContext.CariKartlar.AddRange(musteriKart, tedarikciKart);
        await dbContext.SaveChangesAsync();
        _musteriKartId = musteriKart.Id;
        _tedarikciKartId = tedarikciKart.Id;

        dbContext.MuhasebeDonemler.Add(new MuhasebeDonem
        {
            TesisId = _tesisId, MaliYil = 2026, DonemNo = 1,
            BaslangicTarihi = new DateTime(2026, 1, 1), BitisTarihi = new DateTime(2026, 12, 31), KapaliMi = false
        });
        await dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString) || _kurumId <= 0)
        {
            return;
        }

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await CleanupKurumAsync(dbContext, _kurumId, _tesisId, _ilId, _uniqueSuffix);
    }

    // ─────────────────────────────────────────────────────────────
    // Yardımcılar
    // ─────────────────────────────────────────────────────────────

    private static async Task CleanupKurumAsync(StysAppDbContext dbContext, int kurumId, int tesisId, int ilId, string uniqueSuffix)
    {
        var belgeIds = await dbContext.SatisBelgeleri.IgnoreQueryFilters()
            .Where(x => x.KurumId == kurumId).Select(x => x.Id).ToListAsync();
        var fisIds = new List<int>();
        if (belgeIds.Count > 0)
        {
            fisIds = await dbContext.MuhasebeFisler.IgnoreQueryFilters()
                .Where(x => x.KaynakModul == MuhasebeKaynakModulleri.SatisBelgesi && x.KaynakId != null && belgeIds.Contains(x.KaynakId.Value))
                .Select(x => x.Id).ToListAsync();
            await dbContext.CariHareketler
                .Where(x => x.KaynakModul == MuhasebeKaynakModulleri.SatisBelgesi && x.KaynakId != null && belgeIds.Contains(x.KaynakId.Value))
                .ExecuteDeleteAsync();
            await dbContext.SatisBelgeleri.IgnoreQueryFilters().Where(x => belgeIds.Contains(x.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IadeEdilenBelgeId, (int?)null));
            await dbContext.SatisBelgeleri.IgnoreQueryFilters().Where(x => belgeIds.Contains(x.Id)).ExecuteDeleteAsync();
        }
        if (fisIds.Count > 0)
        {
            await dbContext.MuhasebeFisSatirlari.IgnoreQueryFilters().Where(x => fisIds.Contains(x.MuhasebeFisId)).ExecuteDeleteAsync();
            await dbContext.MuhasebeFisler.IgnoreQueryFilters().Where(x => fisIds.Contains(x.Id)).ExecuteDeleteAsync();
        }

        await dbContext.KurumFaturaNumaraSayaclari.Where(x => x.KurumId == kurumId).ExecuteDeleteAsync();
        await dbContext.MuhasebeDonemler.Where(x => x.TesisId == tesisId).ExecuteDeleteAsync();
        await dbContext.CariKartlar.Where(x => x.TesisId == tesisId).ExecuteDeleteAsync();
        await dbContext.MuhasebeHesapPlanlari.Where(x => x.Kod != null && x.Kod.Contains(uniqueSuffix)).ExecuteDeleteAsync();
        await dbContext.Tesisler.Where(x => x.Id == tesisId).ExecuteDeleteAsync();
        await dbContext.Iller.Where(x => x.Id == ilId).ExecuteDeleteAsync();
        await dbContext.Kurumlar.Where(x => x.Id == kurumId).ExecuteDeleteAsync();
    }

    private static async Task<SatisBelgesi> ReadNoTrackingAsync(StysAppDbContext dbContext, int id)
        => await dbContext.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.Id == id);

    /// <summary>
    /// Ortak assertion yardımcısı: verilen (AsNoTracking okunmuş) belgenin saklanan üç projeksiyon
    /// alanının, SatisBelgesiDurumProjection.Proje(belge.BelgeTipi, belge.Durum) ile TAZE olarak
    /// yeniden hesaplanan sonuçla BİREBİR eşleştiğini doğrular - belgenin NİHAİ BelgeTipi/Durum
    /// değerleriyle tutarlılığı kanıtlar (bkz. B/C bölümü - BelgeTipi değişiminde projection'ın
    /// bayatlamadığının kanıtı).
    /// </summary>
    private static void AssertProjectionTutarli(SatisBelgesi belge)
    {
        var (beklenenTicari, beklenenMuhasebe, beklenenFaturalama) =
            SatisBelgesiDurumProjection.Proje(belge.BelgeTipi, belge.Durum);

        Assert.Equal(beklenenTicari, belge.TicariDurum);
        Assert.Equal(beklenenMuhasebe, belge.MuhasebeDurumu);
        Assert.Equal(beklenenFaturalama, belge.FaturalamaDurumu);
    }

    private CreateSatisBelgesiRequest BuildProformaRequest() => new()
    {
        BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
        BelgeTipi = SatisBelgesiTipi.Proforma,
        TesisId = _tesisId,
        CariKartId = _musteriKartId,
        BelgeTarihi = new DateTime(2026, 3, 1),
        MusteriAdSoyad = "Test Musteri " + _uniqueSuffix,
        Satirlar =
        [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Test satir", Miktar = 1, BirimFiyat = 1000m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
            }
        ]
    };

    private CreateSatisBelgesiRequest BuildSatisFaturasiRequest() => new()
    {
        BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
        BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
        TesisId = _tesisId,
        CariKartId = _musteriKartId,
        BelgeTarihi = new DateTime(2026, 3, 1),
        MusteriAdSoyad = "Test Musteri " + _uniqueSuffix,
        Satirlar =
        [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Test satir", Miktar = 1, BirimFiyat = 1000m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
            }
        ]
    };

    private CreateSatisBelgesiRequest BuildAlisFaturasiRequest() => new()
    {
        BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
        BelgeTipi = SatisBelgesiTipi.AlisFaturasi,
        TesisId = _tesisId,
        CariKartId = _tedarikciKartId,
        BelgeTarihi = new DateTime(2026, 3, 1),
        MusteriAdSoyad = "Test Tedarikci " + _uniqueSuffix,
        KarsiTarafFaturaNo = $"TED-{Guid.NewGuid():N}"[..20],
        Satirlar =
        [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Test satir", Miktar = 1, BirimFiyat = 500m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
            }
        ]
    };

    /// <summary>
    /// Backfill matrisini İKİNCİ KEZ (elle kopyalanarak) TANIMLAMAZ - bunun yerine gerçek
    /// AddSeparatedSatisBelgesiStatuses migration'ının Up() metodunu GERÇEKTEN çalıştırıp ürettiği
    /// MigrationOperation listesinden backfill SqlOperation'ının [Sql] metnini ÇIKARIR ve bu METNİ
    /// OLDUĞU GİBİ (üretim migration'ıyla BİREBİR AYNI, gerçek nesneden okunmuş) çalıştırır. Bu
    /// sayede test, üretim migration dosyasından BAĞIMSIZ ikinci bir eşleme tanımıyla değil,
    /// migration'ın GERÇEK içeriğiyle doğrulanmış olur. Üretim SQL'i WHERE koşulu İÇERMEZ (tüm
    /// tabloyu kapsar) - test izolasyonu için ayrıca bir kapsam daraltması YAPILMAZ; idempotent
    /// olduğundan paylaşılan test DB'sindeki diğer satırları da (varsa) doğru şekilde günceller.
    /// </summary>
    private static async Task BackfillAsync(StysAppDbContext dbContext)
    {
        var migration = new AddSeparatedSatisBelgesiStatuses();
        var migrationBuilder = new MigrationBuilder(activeProvider: null);
        // Migration.Up(MigrationBuilder) `protected override`dır - üretim erişim düzeyi
        // BİLİNÇLİ OLARAK değiştirilmez (migration dosyasına dokunulmaz); bunun yerine test
        // burada reflection ile GERÇEK metodu çağırır.
        var upMethod = typeof(AddSeparatedSatisBelgesiStatuses).GetMethod(
            "Up", BindingFlags.NonPublic | BindingFlags.Instance, [typeof(MigrationBuilder)])
            ?? throw new InvalidOperationException("AddSeparatedSatisBelgesiStatuses.Up metodu reflection ile bulunamadı.");
        upMethod.Invoke(migration, [migrationBuilder]);

        var sqlOperation = migrationBuilder.Operations
            .OfType<SqlOperation>()
            .Single(op => op.Sql.Contains("[SatisBelgeleri]", StringComparison.Ordinal) && op.Sql.Contains("UPDATE", StringComparison.Ordinal));

        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sqlOperation.Sql;
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Servisi/dual-write'ı BAYPAS ederek doğrudan SQL ile bir SatisBelgesi satırı ekler - "pre-migration" (yeni alanlar NULL) legacy veriyi simüle eder.</summary>
    private async Task<int> InsertLegacyRowAsync(
        StysAppDbContext dbContext, SatisBelgesiTipi belgeTipi, SatisBelgesiDurumu durum, bool isDeleted, int? cariKartId = null)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO [muhasebe].[SatisBelgeleri]
                (KurumId, BelgeNo, BelgeTipi, Durum, KaynakModul, TesisId, CariKartId, BelgeTarihi, KurumsalMi, ToplamMatrah, ToplamKdv, GenelToplam, IsDeleted)
            OUTPUT INSERTED.Id
            VALUES (@kurumId, @belgeNo, @belgeTipi, @durum, 1, @tesisId, @cariKartId, '2026-01-01', 0, 100, 20, 120, @isDeleted)
            """;
        cmd.Parameters.Add(new SqlParameter("@kurumId", _kurumId));
        cmd.Parameters.Add(new SqlParameter("@belgeNo", $"LGC-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40]));
        cmd.Parameters.Add(new SqlParameter("@belgeTipi", (int)belgeTipi));
        cmd.Parameters.Add(new SqlParameter("@durum", (int)durum));
        cmd.Parameters.Add(new SqlParameter("@tesisId", _tesisId));
        cmd.Parameters.Add(new SqlParameter("@cariKartId", (object?)cariKartId ?? DBNull.Value));
        cmd.Parameters.Add(new SqlParameter("@isDeleted", isDeleted));

        var id = (int)(await cmd.ExecuteScalarAsync())!;
        return id;
    }

    // ─────────────────────────────────────────────────────────────
    // Migration testleri — kolon nullable'lığı, check constraint'ler
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task Migration_YeniUcKolonNullableOlarakTanimlanmis()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT COLUMN_NAME, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = 'muhasebe' AND TABLE_NAME = 'SatisBelgeleri'
              AND COLUMN_NAME IN ('TicariDurum', 'MuhasebeDurumu', 'FaturalamaDurumu')
            """;
        var nullableByColumn = new Dictionary<string, string>();
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                nullableByColumn[reader.GetString(0)] = reader.GetString(1);
            }
        }

        Assert.Equal(3, nullableByColumn.Count);
        Assert.Equal("YES", nullableByColumn["TicariDurum"]);
        Assert.Equal("YES", nullableByColumn["MuhasebeDurumu"]);
        Assert.Equal("YES", nullableByColumn["FaturalamaDurumu"]);
    }

    [IntegrationTheory]
    [InlineData("TicariDurum", "CK_SatisBelgeleri_TicariDurum")]
    [InlineData("MuhasebeDurumu", "CK_SatisBelgeleri_MuhasebeDurumu")]
    [InlineData("FaturalamaDurumu", "CK_SatisBelgeleri_FaturalamaDurumu")]
    public async Task Migration_GecersizEnumDegeriCheckConstraintTarafindanReddedilir(string kolonAdi, string constraintAdi)
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO [muhasebe].[SatisBelgeleri]
                (KurumId, BelgeNo, BelgeTipi, Durum, KaynakModul, TesisId, BelgeTarihi, KurumsalMi, ToplamMatrah, ToplamKdv, GenelToplam, IsDeleted, [{kolonAdi}])
            VALUES (@kurumId, @belgeNo, 2, 1, 1, @tesisId, '2026-01-01', 0, 100, 20, 120, 0, 99)
            """;
        cmd.Parameters.Add(new SqlParameter("@kurumId", _kurumId));
        cmd.Parameters.Add(new SqlParameter("@belgeNo", $"CKX-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40]));
        cmd.Parameters.Add(new SqlParameter("@tesisId", _tesisId));

        var ex = await Assert.ThrowsAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
        Assert.Contains(constraintAdi, ex.Message);
    }

    [IntegrationFact]
    public async Task Migration_GecerliEnumDegerleriKabulEdilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }
        var belgeNo = $"CKV-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40];
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO [muhasebe].[SatisBelgeleri]
                    (KurumId, BelgeNo, BelgeTipi, Durum, KaynakModul, TesisId, BelgeTarihi, KurumsalMi, ToplamMatrah, ToplamKdv, GenelToplam, IsDeleted, TicariDurum, MuhasebeDurumu, FaturalamaDurumu)
                VALUES (@kurumId, @belgeNo, 2, 1, 1, @tesisId, '2026-01-01', 0, 100, 20, 120, 0, 1, 1, 2)
                """;
            cmd.Parameters.Add(new SqlParameter("@kurumId", _kurumId));
            cmd.Parameters.Add(new SqlParameter("@belgeNo", belgeNo));
            cmd.Parameters.Add(new SqlParameter("@tesisId", _tesisId));
            await cmd.ExecuteNonQueryAsync();
        }

        var eklenen = await dbContext.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.BelgeNo == belgeNo);
        Assert.Equal(TicariBelgeDurumu.Taslak, eklenen.TicariDurum);
        Assert.Equal(TicariBelgeMuhasebeDurumu.Bekliyor, eklenen.MuhasebeDurumu);
        Assert.Equal(TicariBelgeFaturalamaDurumu.Baslatilmadi, eklenen.FaturalamaDurumu);
    }

    // ─────────────────────────────────────────────────────────────
    // Migration testleri — backfill (aktif + soft-delete + legacy tip + öncelik)
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task Migration_Backfill_AktifKayitDogruDolduruluyor()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var id = await InsertLegacyRowAsync(dbContext, SatisBelgesiTipi.SatisFaturasi, SatisBelgesiDurumu.MuhasebeOnaylandi, isDeleted: false, _musteriKartId);

        var oncesi = await ReadNoTrackingAsync(dbContext, id);
        Assert.Null(oncesi.TicariDurum);
        Assert.Null(oncesi.MuhasebeDurumu);
        Assert.Null(oncesi.FaturalamaDurumu);

        await BackfillAsync(dbContext);

        var sonrasi = await ReadNoTrackingAsync(dbContext, id);
        Assert.Equal(TicariBelgeDurumu.Hazir, sonrasi.TicariDurum);
        Assert.Equal(TicariBelgeMuhasebeDurumu.Onaylandi, sonrasi.MuhasebeDurumu);
        Assert.Equal(TicariBelgeFaturalamaDurumu.KesimBekliyor, sonrasi.FaturalamaDurumu);
        Assert.Equal(SatisBelgesiTipi.SatisFaturasi, sonrasi.BelgeTipi);
        Assert.Equal(SatisBelgesiDurumu.MuhasebeOnaylandi, sonrasi.Durum);
    }

    [IntegrationFact]
    public async Task Migration_Backfill_SoftDeleteEdilmisKayitDaBackfillEdilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var id = await InsertLegacyRowAsync(dbContext, SatisBelgesiTipi.SatisFaturasi, SatisBelgesiDurumu.Taslak, isDeleted: true, _musteriKartId);

        await BackfillAsync(dbContext);

        // IgnoreQueryFilters ZORUNLUDUR - satır IsDeleted=true olduğundan normal sorgu onu görmez.
        var sonrasi = await dbContext.SatisBelgeleri.IgnoreQueryFilters().AsNoTracking().FirstAsync(x => x.Id == id);
        Assert.True(sonrasi.IsDeleted);
        Assert.Equal(TicariBelgeDurumu.Taslak, sonrasi.TicariDurum);
        Assert.Equal(TicariBelgeMuhasebeDurumu.Bekliyor, sonrasi.MuhasebeDurumu);
        Assert.Equal(TicariBelgeFaturalamaDurumu.Baslatilmadi, sonrasi.FaturalamaDurumu);
    }

    [IntegrationFact]
    public async Task Migration_Backfill_LegacyIadeFaturasiTipiDegismezVeUygulanamazOlur()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var id = await InsertLegacyRowAsync(dbContext, SatisBelgesiTipi.IadeFaturasi, SatisBelgesiDurumu.Taslak, isDeleted: false);

        await BackfillAsync(dbContext);

        var sonrasi = await ReadNoTrackingAsync(dbContext, id);
        // BelgeTipi ASLA değiştirilmemeli - SatisIadeFaturasi veya AlisIadeFaturasi'ye TAHMİN EDİLEREK dönüştürülmez.
        Assert.Equal(SatisBelgesiTipi.IadeFaturasi, sonrasi.BelgeTipi);
        Assert.Equal(SatisBelgesiDurumu.Taslak, sonrasi.Durum);
        Assert.Equal(TicariBelgeFaturalamaDurumu.Uygulanamaz, sonrasi.FaturalamaDurumu);
        Assert.Equal(TicariBelgeDurumu.Taslak, sonrasi.TicariDurum);
        Assert.Equal(TicariBelgeMuhasebeDurumu.Bekliyor, sonrasi.MuhasebeDurumu);
    }

    [IntegrationFact]
    public async Task Migration_Backfill_FaturaKesildiVeMusteriyeGonderildiGecmisiBelgeTipindenBagimsizKorunur()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();

        // AlisFaturasi normal uygulama akışında ASLA FaturaKesildi'ye ulaşamaz (bkz.
        // OtomatikResmiNumaraUretilebilirMi), ama VERİ olarak (anomalik/tarihsel) böyle bir satır
        // bulunsa dahi backfill'in FaturalamaDurumu geçmişini (belge tipinden bağımsız) KORUMASI
        // gerekir - bu bilinçli olarak "olması beklenmeyen ama korunması gereken" bir senaryodur.
        var alisFaturaKesildiId = await InsertLegacyRowAsync(dbContext, SatisBelgesiTipi.AlisFaturasi, SatisBelgesiDurumu.FaturaKesildi, isDeleted: false, _tedarikciKartId);
        var satisIadeMusteriyeGonderildiId = await InsertLegacyRowAsync(dbContext, SatisBelgesiTipi.SatisIadeFaturasi, SatisBelgesiDurumu.MusteriyeGonderildi, isDeleted: false, _musteriKartId);

        await BackfillAsync(dbContext);

        var alisSonrasi = await ReadNoTrackingAsync(dbContext, alisFaturaKesildiId);
        Assert.Equal(SatisBelgesiTipi.AlisFaturasi, alisSonrasi.BelgeTipi);
        Assert.Equal(SatisBelgesiDurumu.FaturaKesildi, alisSonrasi.Durum);
        Assert.Equal(TicariBelgeFaturalamaDurumu.Kesildi, alisSonrasi.FaturalamaDurumu);

        var satisIadeSonrasi = await ReadNoTrackingAsync(dbContext, satisIadeMusteriyeGonderildiId);
        Assert.Equal(SatisBelgesiTipi.SatisIadeFaturasi, satisIadeSonrasi.BelgeTipi);
        Assert.Equal(SatisBelgesiDurumu.MusteriyeGonderildi, satisIadeSonrasi.Durum);
        Assert.Equal(TicariBelgeFaturalamaDurumu.MusteriyeGonderildi, satisIadeSonrasi.FaturalamaDurumu);
    }

    // ─────────────────────────────────────────────────────────────
    // Servis dual-write testleri — SatisBelgesiDurumu HÂLÂ otoriter/karar-belirleyicidir
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task CreateAsync_DualWriteYazarVeKararDegismez()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var created = await service.CreateAsync(BuildSatisFaturasiRequest());

        Assert.Equal(SatisBelgesiDurumu.Taslak, created.Durum); // mevcut davranış DEĞİŞMEDİ

        await using var verifyContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var sonHal = await ReadNoTrackingAsync(verifyContext, created.Id!.Value);
        Assert.Equal(SatisBelgesiDurumu.Taslak, sonHal.Durum);
        Assert.Equal(TicariBelgeDurumu.Taslak, sonHal.TicariDurum);
        Assert.Equal(TicariBelgeMuhasebeDurumu.Bekliyor, sonHal.MuhasebeDurumu);
        Assert.Equal(TicariBelgeFaturalamaDurumu.Baslatilmadi, sonHal.FaturalamaDurumu);
    }

    [IntegrationFact]
    public async Task SatisFaturasiYasamDongusu_MuhasebeOnayinaGonderOnaylaVeFaturaKes_DualWriteHerAsamadaDogruYazar()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var donemService = SatisBelgesiMuhasebeTestSupport.CreateRealMuhasebeDonemService(dbContext);
        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext, donemService);

        var created = await service.CreateAsync(BuildSatisFaturasiRequest());

        // 1) MuhasebeOnayinaGonderAsync
        await service.MuhasebeOnayinaGonderAsync(created.Id!.Value);
        await using (var ctx1 = SatisBelgesiMuhasebeTestSupport.CreateDbContext())
        {
            var hal1 = await ReadNoTrackingAsync(ctx1, created.Id.Value);
            Assert.Equal(SatisBelgesiDurumu.MuhasebeOnayinda, hal1.Durum); // mevcut davranış DEĞİŞMEDİ
            Assert.Equal(TicariBelgeDurumu.Hazir, hal1.TicariDurum);
            Assert.Equal(TicariBelgeMuhasebeDurumu.Onayda, hal1.MuhasebeDurumu);
            Assert.Equal(TicariBelgeFaturalamaDurumu.Baslatilmadi, hal1.FaturalamaDurumu);
        }

        // 2) MuhasebeOnaylaAsync
        await service.MuhasebeOnaylaAsync(created.Id.Value);
        await using (var ctx2 = SatisBelgesiMuhasebeTestSupport.CreateDbContext())
        {
            var hal2 = await ReadNoTrackingAsync(ctx2, created.Id.Value);
            Assert.Equal(SatisBelgesiDurumu.MuhasebeOnaylandi, hal2.Durum);
            Assert.Equal(TicariBelgeDurumu.Hazir, hal2.TicariDurum);
            Assert.Equal(TicariBelgeMuhasebeDurumu.Onaylandi, hal2.MuhasebeDurumu);
            Assert.Equal(TicariBelgeFaturalamaDurumu.KesimBekliyor, hal2.FaturalamaDurumu);
        }

        await fisService.MuhasebeFisiOlusturAsync(created.Id.Value);
        dbContext.KurumFaturaNumaraSayaclari.Add(new KurumFaturaNumaraSayaci
        {
            KurumId = _kurumId, MaliYil = 2026, SeriKodu = "DAY", SonNumara = 0, AktifMi = true
        });
        await dbContext.SaveChangesAsync();

        // 3) FaturaKesAsync
        await service.FaturaKesAsync(created.Id.Value, new FaturaKesRequest { SeriKodu = "DAY" });
        await using (var ctx3 = SatisBelgesiMuhasebeTestSupport.CreateDbContext())
        {
            var hal3 = await ReadNoTrackingAsync(ctx3, created.Id.Value);
            Assert.Equal(SatisBelgesiDurumu.FaturaKesildi, hal3.Durum);
            Assert.Equal(TicariBelgeDurumu.Hazir, hal3.TicariDurum);
            Assert.Equal(TicariBelgeMuhasebeDurumu.Onaylandi, hal3.MuhasebeDurumu);
            Assert.Equal(TicariBelgeFaturalamaDurumu.Kesildi, hal3.FaturalamaDurumu);
        }
    }

    [IntegrationTheory]
    [InlineData(true)] // SatisFaturasi -> StysTarafindanDuzenlenirMi -> Baslatilmadi
    [InlineData(false)] // AlisFaturasi -> Uygulanamaz
    public async Task ReddetAsync_DualWriteYazarVeKararDegismez(bool satisFaturasiMi)
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var request = satisFaturasiMi ? BuildSatisFaturasiRequest() : BuildAlisFaturasiRequest();
        var created = await service.CreateAsync(request);
        await service.MuhasebeOnayinaGonderAsync(created.Id!.Value);

        await service.ReddetAsync(created.Id.Value, "Test ret nedeni");

        await using var verifyContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var sonHal = await ReadNoTrackingAsync(verifyContext, created.Id.Value);

        Assert.Equal(SatisBelgesiDurumu.Reddedildi, sonHal.Durum); // mevcut davranış DEĞİŞMEDİ

        // Muhasebe reddi TİCARİ BELGE REDDİ SAYILMAZ (bkz. TicariBelgeDurumu doc'u) - TicariDurum
        // Hazir kalır, ret yalnızca MuhasebeDurumu'nda ifade edilir.
        Assert.Equal(TicariBelgeDurumu.Hazir, sonHal.TicariDurum);
        Assert.Equal(TicariBelgeMuhasebeDurumu.Reddedildi, sonHal.MuhasebeDurumu);
        Assert.Equal(
            satisFaturasiMi ? TicariBelgeFaturalamaDurumu.Baslatilmadi : TicariBelgeFaturalamaDurumu.Uygulanamaz,
            sonHal.FaturalamaDurumu);
    }

    [IntegrationFact]
    public async Task UpdateAsync_ReddedilenBelgeTaslagaDonerken_DualWriteYazarVeKararDegismez()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var created = await service.CreateAsync(BuildSatisFaturasiRequest());
        await service.MuhasebeOnayinaGonderAsync(created.Id!.Value);
        await service.ReddetAsync(created.Id.Value, "Test ret nedeni");

        var guncellenen = await service.UpdateAsync(created.Id.Value, new UpdateSatisBelgesiRequest { Aciklama = "Guncellendi" });

        Assert.Equal(SatisBelgesiDurumu.Taslak, guncellenen.Durum); // mevcut davranış DEĞİŞMEDİ

        await using var verifyContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var sonHal = await ReadNoTrackingAsync(verifyContext, created.Id.Value);
        Assert.Equal(SatisBelgesiDurumu.Taslak, sonHal.Durum);
        Assert.Equal(TicariBelgeDurumu.Taslak, sonHal.TicariDurum);
        Assert.Equal(TicariBelgeMuhasebeDurumu.Bekliyor, sonHal.MuhasebeDurumu);
        Assert.Equal(TicariBelgeFaturalamaDurumu.Baslatilmadi, sonHal.FaturalamaDurumu);
    }

    [IntegrationFact]
    public async Task IptalEtAsync_DualWriteYazarVeKararDegismez()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var created = await service.CreateAsync(BuildAlisFaturasiRequest());
        await service.MuhasebeOnayinaGonderAsync(created.Id!.Value);
        await service.MuhasebeOnaylaAsync(created.Id.Value);

        // Bağlı muhasebe fişi (henüz) YOK - IptalEtAsync'in ValidateVeIptalEtMuhasebeFisiAsync
        // dalı hiç tetiklenmez, bu yüzden test servis fabrikasının null IMuhasebeFisService'i
        // güvenle kullanılabilir (bkz. SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService).
        await service.IptalEtAsync(created.Id.Value);

        await using var verifyContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var sonHal = await ReadNoTrackingAsync(verifyContext, created.Id.Value);

        Assert.Equal(SatisBelgesiDurumu.IptalEdildi, sonHal.Durum); // mevcut davranış DEĞİŞMEDİ
        Assert.Equal(TicariBelgeDurumu.IptalEdildi, sonHal.TicariDurum);
        Assert.Equal(TicariBelgeMuhasebeDurumu.IptalEdildi, sonHal.MuhasebeDurumu);
        Assert.Equal(TicariBelgeFaturalamaDurumu.IptalEdildi, sonHal.FaturalamaDurumu);
    }

    [IntegrationFact]
    public async Task MuhasebeOnaylaAsync_TaslakBelgeUzerindeCagrilirsaReddedilir_MevcutDavranisKorunur()
    {
        // Dual-write eklenmesi, mevcut karar kontrollerinin (belge.Durum'a dayalı) davranışını
        // DEĞİŞTİRMEMELİDİR - bu test, kabul/ret davranışının aynen korunduğunu kanıtlar.
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var created = await service.CreateAsync(BuildSatisFaturasiRequest());

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.MuhasebeOnaylaAsync(created.Id!.Value));
        Assert.Contains("Sadece Muhasebe Onayında durumundaki belgeler onaylanabilir", ex.Message);

        await using var verifyContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var sonHal = await ReadNoTrackingAsync(verifyContext, created.Id!.Value);
        Assert.Equal(SatisBelgesiDurumu.Taslak, sonHal.Durum);
        Assert.Equal(TicariBelgeDurumu.Taslak, sonHal.TicariDurum);
    }

    // ─────────────────────────────────────────────────────────────
    // C. UpdateAsync sırasında BelgeTipi değişimi — projeksiyon NİHAİ tip/durumla yeniden hesaplanmalı
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task UpdateAsync_TaslakSatisFaturasiProformayaCevrilirse_ProjeksiyonNihaiTipleYenidenHesaplanir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var created = await service.CreateAsync(BuildSatisFaturasiRequest());

        await service.UpdateAsync(created.Id!.Value, new UpdateSatisBelgesiRequest { BelgeTipi = SatisBelgesiTipi.Proforma });

        await using var verifyContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var sonHal = await ReadNoTrackingAsync(verifyContext, created.Id.Value);

        Assert.Equal(SatisBelgesiTipi.Proforma, sonHal.BelgeTipi);
        Assert.Equal(SatisBelgesiDurumu.Taslak, sonHal.Durum);
        Assert.Equal(TicariBelgeDurumu.Taslak, sonHal.TicariDurum);
        Assert.Equal(TicariBelgeMuhasebeDurumu.Bekliyor, sonHal.MuhasebeDurumu);
        Assert.Equal(TicariBelgeFaturalamaDurumu.Uygulanamaz, sonHal.FaturalamaDurumu); // Proforma StysTarafindanDuzenlenirMi=false
        AssertProjectionTutarli(sonHal);
    }

    [IntegrationFact]
    public async Task UpdateAsync_TaslakProformaSatisFaturasinaCevrilirse_ProjeksiyonNihaiTipleYenidenHesaplanir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var created = await service.CreateAsync(BuildProformaRequest());

        await service.UpdateAsync(created.Id!.Value, new UpdateSatisBelgesiRequest { BelgeTipi = SatisBelgesiTipi.SatisFaturasi });

        await using var verifyContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var sonHal = await ReadNoTrackingAsync(verifyContext, created.Id.Value);

        Assert.Equal(SatisBelgesiTipi.SatisFaturasi, sonHal.BelgeTipi);
        Assert.Equal(SatisBelgesiDurumu.Taslak, sonHal.Durum);
        Assert.Equal(TicariBelgeDurumu.Taslak, sonHal.TicariDurum);
        Assert.Equal(TicariBelgeMuhasebeDurumu.Bekliyor, sonHal.MuhasebeDurumu);
        Assert.Equal(TicariBelgeFaturalamaDurumu.Baslatilmadi, sonHal.FaturalamaDurumu); // SatisFaturasi StysTarafindanDuzenlenirMi=true
        AssertProjectionTutarli(sonHal);
    }

    [IntegrationFact]
    public async Task UpdateAsync_ReddedilmisSatisFaturasiAyniUpdateIcindeProformayaCevrilirse_ProjeksiyonNihaiTipleHesaplanir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var created = await service.CreateAsync(BuildSatisFaturasiRequest());
        await service.MuhasebeOnayinaGonderAsync(created.Id!.Value);
        await service.ReddetAsync(created.Id.Value, "Test ret nedeni");

        // Reddedildi->Taslak dönüşü VE BelgeTipi değişimi AYNI update isteğinde birlikte gönderilir -
        // düzeltmeden ÖNCE, Reddedildi->Taslak dalındaki erken projeksiyon ESKİ BelgeTipi
        // (SatisFaturasi) ile hesaplanıp KALIRDI (FaturalamaDurumu yanlışlıkla Baslatilmadi olurdu);
        // artık UpdateAsync sonunda NİHAİ BelgeTipi (Proforma) ile YENİDEN hesaplanır.
        var guncellenen = await service.UpdateAsync(created.Id.Value, new UpdateSatisBelgesiRequest { BelgeTipi = SatisBelgesiTipi.Proforma });

        Assert.Equal(SatisBelgesiDurumu.Taslak, guncellenen.Durum);
        Assert.Null(guncellenen.RedNedeni);

        await using var verifyContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var sonHal = await ReadNoTrackingAsync(verifyContext, created.Id.Value);

        Assert.Equal(SatisBelgesiTipi.Proforma, sonHal.BelgeTipi);
        Assert.Equal(SatisBelgesiDurumu.Taslak, sonHal.Durum);
        Assert.Null(sonHal.RedNedeni);
        Assert.Equal(TicariBelgeFaturalamaDurumu.Uygulanamaz, sonHal.FaturalamaDurumu);
        AssertProjectionTutarli(sonHal);
    }

    [IntegrationFact]
    public async Task UpdateAsync_YalnizcaAciklamaGuncellenirse_BelgeTipiVeProjeksiyonDegismezKalir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var created = await service.CreateAsync(BuildSatisFaturasiRequest());

        await service.UpdateAsync(created.Id!.Value, new UpdateSatisBelgesiRequest { Aciklama = "Guncellendi" });

        await using var verifyContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var sonHal = await ReadNoTrackingAsync(verifyContext, created.Id.Value);

        Assert.Equal(SatisBelgesiTipi.SatisFaturasi, sonHal.BelgeTipi);
        Assert.Equal(SatisBelgesiDurumu.Taslak, sonHal.Durum);
        Assert.Equal(TicariBelgeDurumu.Taslak, sonHal.TicariDurum);
        Assert.Equal(TicariBelgeMuhasebeDurumu.Bekliyor, sonHal.MuhasebeDurumu);
        Assert.Equal(TicariBelgeFaturalamaDurumu.Baslatilmadi, sonHal.FaturalamaDurumu);
        AssertProjectionTutarli(sonHal);
    }
}
