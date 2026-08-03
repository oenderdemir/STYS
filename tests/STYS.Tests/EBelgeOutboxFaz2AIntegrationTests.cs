using System.Data.Common;
using System.Runtime.CompilerServices;
using AutoMapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Kurumlar.Entities;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.CariKartlar.Mapping;
using STYS.Muhasebe.CariKartlar.Repositories;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.MuhasebeDonemleri.Mapping;
using STYS.Muhasebe.MuhasebeDonemleri.Repositories;
using STYS.Muhasebe.MuhasebeDonemleri.Services;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Mapping;
using STYS.Muhasebe.SatisBelgeleri.Repositories;
using STYS.Muhasebe.SatisBelgeleri.Services;
using STYS.Muhasebe.SatisBelgeleri.Services.MuhasebeFisStratejileri;
using STYS.Tesisler.Entities;
using TOD.Platform.AspNetCore.Logging;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class EBelgeOutboxFaz2AIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "EBO-2A";

    private string _uniqueSuffix = TestMarker;
    private DateTime _classStartUtc;
    private int _kurumId;
    private int _ilId;
    private int _tesisId;
    private int _musteriKartId;
    private int _tedarikciKartId;

    public async Task InitializeAsync()
    {
        _classStartUtc = DateTime.UtcNow;
        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (kurum, il, tesis) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(dbContext, _uniqueSuffix);
        _kurumId = kurum.Id;
        _ilId = il.Id;
        _tesisId = tesis.Id;

        var musteriHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "MUS", _tesisId);
        var tedarikciHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "TED", _tesisId);
        var gelirHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.GelirSatis, "GELIR", _tesisId);
        var kdvHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.KDVHesaplanan, "KDV", _tesisId);
        dbContext.MuhasebeHesapPlanlari.AddRange(musteriHesap, tedarikciHesap, gelirHesap, kdvHesap);
        await dbContext.SaveChangesAsync();

        var musteriKart = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "MUS", CariKartTipleri.Musteri, _tesisId, musteriHesap.Id);
        musteriKart.EArsivKapsamindaMi = true;
        var tedarikciKart = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "TED", CariKartTipleri.Tedarikci, _tesisId, tedarikciHesap.Id);
        tedarikciKart.VergiNoTckn = "1111111111";
        tedarikciKart.EFaturaMukellefiMi = true;
        dbContext.CariKartlar.AddRange(musteriKart, tedarikciKart);
        await dbContext.SaveChangesAsync();
        _musteriKartId = musteriKart.Id;
        _tedarikciKartId = tedarikciKart.Id;

        dbContext.MuhasebeDonemler.Add(new STYS.Muhasebe.MuhasebeDonemleri.Entities.MuhasebeDonem
        {
            TesisId = _tesisId,
            MaliYil = 2026,
            DonemNo = 1,
            BaslangicTarihi = new DateTime(2026, 1, 1),
            BitisTarihi = new DateTime(2026, 12, 31),
            KapaliMi = false
        });

        dbContext.KurumFaturaNumaraSayaclari.Add(new KurumFaturaNumaraSayaci
        {
            KurumId = _kurumId,
            MaliYil = 2026,
            SeriKodu = "EBF",
            SonNumara = 0,
            AktifMi = true
        });

        await dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await dbContext.KurumFaturaNumaraSayaclari.Where(x => x.KurumId == _kurumId).ExecuteDeleteAsync();
        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, _uniqueSuffix, _tesisId, _kurumId, _ilId);
    }

    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<SatisBelgesiProfile>();
            cfg.AddProfile<MuhasebeDonemProfile>();
            cfg.AddProfile<CariKartProfile>();
        }, NullLoggerFactory.Instance);

        return config.CreateMapper();
    }

    private static ISatisBelgesiService CreateService(StysAppDbContext dbContext)
    {
        var mapper = CreateMapper();
        var satisBelgesiRepository = new SatisBelgesiRepository(dbContext, mapper);
        var muhasebeFisRepository = new STYS.Muhasebe.MuhasebeFisleri.Repositories.MuhasebeFisRepository(dbContext, mapper);
        return new SatisBelgesiService(
            satisBelgesiRepository,
            dbContext,
            mapper,
            muhasebeFisRepository,
            null!,
            new SatisBelgesiMuhasebeTestSupport.FakeUserAccessScopeService(),
            NullLogger<SatisBelgesiService>.Instance,
            new SatisBelgesiMuhasebeTestSupport.NoOpDomainOperationLogger());
    }

    private static StysAppDbContext CreateDbContext(
        IInterceptor? interceptor = null,
        int? currentKurumId = null,
        bool isSuperAdmin = true)
    {
        var optionsBuilder = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseSqlServer(SatisBelgesiMuhasebeTestSupport.ConnectionString);

        if (interceptor is not null)
        {
            optionsBuilder.AddInterceptors(interceptor);
        }

        return new StysAppDbContext(
            optionsBuilder.Options,
            new SatisBelgesiMuhasebeTestSupport.FakeCurrentUserAccessor(),
            new TestTenantAccessor(currentKurumId, isSuperAdmin));
    }

    private CreateSatisBelgesiRequest BuildSatisBelgesiRequest(SatisBelgesiTipi belgeTipi = SatisBelgesiTipi.SatisFaturasi)
        => new()
        {
            BelgeNo = TruncateToMax($"{_uniqueSuffix}-EBF-{Guid.NewGuid():N}", 40),
            BelgeTipi = belgeTipi,
            TesisId = _tesisId,
            CariKartId = belgeTipi == SatisBelgesiTipi.AlisFaturasi ? _tedarikciKartId : _musteriKartId,
            KarsiTarafFaturaNo = belgeTipi == SatisBelgesiTipi.AlisFaturasi ? TruncateToMax($"KTF-{_uniqueSuffix}", 40) : null,
            BelgeTarihi = new DateTime(2026, 3, 1),
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1,
                    Aciklama = "Test satiri",
                    SatirTipi = SatisBelgesiSatirTipi.EkHizmet,
                    Miktar = 1,
                    BirimFiyat = 1000m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli,
                    KdvOrani = 20m
                }
            ]
        };

    private static string TruncateToMax(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private async Task EnsureMuhasebeFisIdAsync(int satisBelgesiId)
    {
        await using var verifyCtx = CreateDbContext();
        var muhasebeFisId = await verifyCtx.SatisBelgeleri
            .AsNoTracking()
            .Where(x => x.Id == satisBelgesiId)
            .Select(x => x.MuhasebeFisId)
            .SingleAsync();

        Assert.True(muhasebeFisId.HasValue);
    }

    private static string GetMigrationFilePath([CallerFilePath] string testFilePath = "")
    {
        var testsDir = Path.GetDirectoryName(testFilePath)!;
        var repoRoot = Path.GetFullPath(Path.Combine(testsDir, "..", ".."));
        return Path.Combine(
            repoRoot,
            "backend",
            "Infrastructure",
            "EntityFramework",
            "Migrations",
            "20260802214530_AddEBelgeOutboxFaz2A.cs");
    }

    private static string GetBackfillSqlFromMigration()
    {
        var migrationSource = File.ReadAllText(GetMigrationFilePath());
        Assert.Contains("WHERE kayit.[IsDeleted] = 0", migrationSource);
        Assert.Contains("NOT EXISTS", migrationSource);
        Assert.Contains("SYSUTCDATETIME()", migrationSource);
        Assert.Contains("migration:20260802214530_AddEBelgeOutboxFaz2A", migrationSource);

        const string startMarker = "migrationBuilder.Sql(\"\"\"";
        const string endMarker = "\"\"\");";

        var start = migrationSource.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, "Migration SQL başlangıcı bulunamadı.");

        start += startMarker.Length;
        var end = migrationSource.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, "Migration SQL bitişi bulunamadı.");

        return migrationSource[start..end];
    }

    private static SqlException? FindSqlException(Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is SqlException sqlException)
            {
                return sqlException;
            }

            exception = exception.InnerException;
        }

        return null;
    }

    private static void AssertSqlException(DbUpdateException exception, int[] expectedNumbers, string expectedText)
    {
        var sqlException = FindSqlException(exception) ?? throw new Xunit.Sdk.XunitException("SqlException bulunamadi.");
        Assert.Contains(sqlException.Number, expectedNumbers);
        Assert.Contains(expectedText, sqlException.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task CreateTempBackfillSchemaAsync(SqlConnection connection)
    {
        await using (var schemaCommand = connection.CreateCommand())
        {
            schemaCommand.CommandText = """
IF SCHEMA_ID(N'muhasebe') IS NULL
    EXEC(N'CREATE SCHEMA muhasebe');
""";
            await schemaCommand.ExecuteNonQueryAsync();
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
CREATE TABLE muhasebe.EBelgeKayitlari (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EBelgeKayitlari PRIMARY KEY,
    KurumId INT NOT NULL,
    IsDeleted BIT NOT NULL,
    CreatedAt DATETIME2 NULL,
    CreatedBy NVARCHAR(MAX) NULL
);

CREATE TABLE muhasebe.EBelgeOutboxMesajlari (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EBelgeOutboxMesajlari PRIMARY KEY,
    KurumId INT NOT NULL,
    EBelgeKaydiId INT NOT NULL,
    IsTuru INT NOT NULL,
    Durum INT NOT NULL,
    DenemeSayisi INT NOT NULL,
    SonrakiDenemeZamaniUtc DATETIME2 NULL,
    KilitToken NVARCHAR(MAX) NULL,
    KilitBitisZamaniUtc DATETIME2 NULL,
    IslemBaslamaZamaniUtc DATETIME2 NULL,
    TamamlanmaZamaniUtc DATETIME2 NULL,
    SonHataKodu NVARCHAR(100) NULL,
    SonHataMesaji NVARCHAR(2000) NULL,
    IsDeleted BIT NOT NULL,
    CreatedAt DATETIME2 NULL,
    UpdatedAt DATETIME2 NULL,
    DeletedAt DATETIME2 NULL,
    CreatedBy NVARCHAR(MAX) NULL,
    UpdatedBy NVARCHAR(MAX) NULL,
    DeletedBy NVARCHAR(MAX) NULL
);

CREATE UNIQUE INDEX IX_EBelgeOutboxMesajlari_EBelgeKaydiId_IsTuru
    ON muhasebe.EBelgeOutboxMesajlari (EBelgeKaydiId, IsTuru);
""";
            await command.ExecuteNonQueryAsync();
        }
    }

    private async Task<SatisBelgesiDto> CreateAndCutOutgoingInvoiceAsync()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var created = await service.CreateAsync(BuildSatisBelgesiRequest());
        await service.MuhasebeOnayinaGonderAsync(created.Id!.Value, CancellationToken.None);
        await service.MuhasebeOnaylaAsync(created.Id.Value, CancellationToken.None);

        var donemService = SatisBelgesiMuhasebeTestSupport.CreateRealMuhasebeDonemService(dbContext);
        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext, donemService);
        await fisService.MuhasebeFisiOlusturAsync(created.Id.Value, CancellationToken.None);
        await EnsureMuhasebeFisIdAsync(created.Id.Value);

        await using var kesimCtx = CreateDbContext();
        var kesimService = CreateService(kesimCtx);
        return await kesimService.FaturaKesAsync(created.Id.Value, new FaturaKesRequest { SeriKodu = "EBF" }, CancellationToken.None);
    }

    private static async Task<(string DatabaseName, string ConnectionString)> CreateTempDatabaseAsync(string baseConnectionString)
    {
        var databaseName = $"STYSDB_EBelgeOutbox_{Guid.NewGuid():N}";
        var masterConnectionString = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = "master"
        }.ConnectionString;

        var tempConnectionString = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = databaseName
        }.ConnectionString;

        await using (var masterConnection = new SqlConnection(masterConnectionString))
        {
            await masterConnection.OpenAsync();
            await using var createCmd = masterConnection.CreateCommand();
            createCmd.CommandText = $"CREATE DATABASE [{databaseName}]";
            await createCmd.ExecuteNonQueryAsync();
        }

        return (databaseName, tempConnectionString);
    }

    private static async Task DropTempDatabaseAsync(string baseConnectionString, string databaseName)
    {
        var masterConnectionString = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = "master"
        }.ConnectionString;

        await using var masterConnection = new SqlConnection(masterConnectionString);
        await masterConnection.OpenAsync();

        await using var dropCmd = masterConnection.CreateCommand();
        dropCmd.CommandText = $@"
IF DB_ID(N'{databaseName}') IS NOT NULL
BEGIN
    ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [{databaseName}];
END";
        await dropCmd.ExecuteNonQueryAsync();
    }

    [IntegrationFact]
    public async Task BasariliKesimdeTekBekleyenArtefaktOutboxMesajiOlusur()
    {
        var cut = await CreateAndCutOutgoingInvoiceAsync();

        await using var verifyCtx = CreateDbContext();
        var belge = await verifyCtx.SatisBelgeleri
            .AsNoTracking()
            .Include(x => x.EBelgeKaydi)
            .SingleAsync(x => x.Id == cut.Id.Value);

        var outboxMesaji = await verifyCtx.EBelgeOutboxMesajlari
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(x => x.EBelgeKaydiId == belge.EBelgeKaydi!.Id);

        Assert.Equal(_kurumId, outboxMesaji.KurumId);
        Assert.Equal(belge.EBelgeKaydi.Id, outboxMesaji.EBelgeKaydiId);
        Assert.Equal(EBelgeOutboxIsTuru.ArtefaktOlustur, outboxMesaji.IsTuru);
        Assert.Equal(EBelgeOutboxDurumu.Bekliyor, outboxMesaji.Durum);
        Assert.Equal(0, outboxMesaji.DenemeSayisi);
        Assert.Null(outboxMesaji.SonrakiDenemeZamaniUtc);
        Assert.Null(outboxMesaji.KilitToken);
        Assert.Null(outboxMesaji.KilitBitisZamaniUtc);
        Assert.Null(outboxMesaji.IslemBaslamaZamaniUtc);
        Assert.Null(outboxMesaji.TamamlanmaZamaniUtc);
        Assert.Null(outboxMesaji.SonHataKodu);
        Assert.Null(outboxMesaji.SonHataMesaji);
    }

    [IntegrationFact]
    public async Task AyniBelgeTekrarKesildigindeOutboxSayisiBirKalir()
    {
        var cut = await CreateAndCutOutgoingInvoiceAsync();

        await using var kesimCtx = CreateDbContext();
        var kesimService = CreateService(kesimCtx);
        var ikinci = await kesimService.FaturaKesAsync(cut.Id!.Value, new FaturaKesRequest { SeriKodu = "EBF" }, CancellationToken.None);

        Assert.Equal(cut.ResmiFaturaNo, ikinci.ResmiFaturaNo);
        Assert.Equal(cut.EBelgeUuid, ikinci.EBelgeUuid);

        await using var verifyCtx = CreateDbContext();
        var outboxSayisi = await verifyCtx.EBelgeOutboxMesajlari
            .IgnoreQueryFilters()
            .CountAsync(x => x.EBelgeKaydi.SatisBelgesiId == cut.Id.Value);

        Assert.Equal(1, outboxSayisi);
    }

    [IntegrationFact]
    public async Task KanalBelirlenemezseRollbackOlurVeOutboxOlusmaz()
    {
        await using var seedCtx = CreateDbContext();
        var service = CreateService(seedCtx);
        var created = await service.CreateAsync(BuildSatisBelgesiRequest());
        await service.MuhasebeOnayinaGonderAsync(created.Id!.Value, CancellationToken.None);
        await service.MuhasebeOnaylaAsync(created.Id.Value, CancellationToken.None);

        var donemService = SatisBelgesiMuhasebeTestSupport.CreateRealMuhasebeDonemService(seedCtx);
        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(seedCtx, donemService);
        await fisService.MuhasebeFisiOlusturAsync(created.Id.Value, CancellationToken.None);
        await EnsureMuhasebeFisIdAsync(created.Id.Value);

        var sayacOnce = await seedCtx.KurumFaturaNumaraSayaclari
            .AsNoTracking()
            .SingleAsync(x => x.KurumId == _kurumId && x.MaliYil == 2026 && x.SeriKodu == "EBF");

        await using (var mutateCtx = CreateDbContext())
        {
            var cariKart = await mutateCtx.CariKartlar.SingleAsync(x => x.Id == _musteriKartId);
            cariKart.EFaturaMukellefiMi = false;
            cariKart.EArsivKapsamindaMi = false;
            await mutateCtx.SaveChangesAsync();
        }

        await using var kesimCtx = CreateDbContext();
        var kesimService = CreateService(kesimCtx);
        var ex = await Assert.ThrowsAsync<BaseException>(
            () => kesimService.FaturaKesAsync(created.Id.Value, new FaturaKesRequest { SeriKodu = "EBF" }, CancellationToken.None));

        Assert.Contains("e-Fatura ya da e-Arşiv", ex.Message, StringComparison.OrdinalIgnoreCase);

        await using var verifyCtx = CreateDbContext();
        var belge = await verifyCtx.SatisBelgeleri.AsNoTracking().SingleAsync(x => x.Id == created.Id.Value);
        Assert.Null(belge.ResmiFaturaNo);
        Assert.Null(belge.EBelgeKaydi);
        Assert.False(await verifyCtx.EBelgeKayitlari.IgnoreQueryFilters().AnyAsync(x => x.SatisBelgesiId == created.Id.Value));
        Assert.False(await verifyCtx.EBelgeSnapshots.IgnoreQueryFilters().AnyAsync(x => x.EBelgeKaydi.SatisBelgesiId == created.Id.Value));
        Assert.False(await verifyCtx.EBelgeOutboxMesajlari.IgnoreQueryFilters().AnyAsync(x => x.EBelgeKaydi.SatisBelgesiId == created.Id.Value));

        var sayacSonra = await verifyCtx.KurumFaturaNumaraSayaclari
            .AsNoTracking()
            .SingleAsync(x => x.KurumId == _kurumId && x.MaliYil == 2026 && x.SeriKodu == "EBF");

        Assert.Equal(sayacOnce.SonNumara, sayacSonra.SonNumara);
    }

    [IntegrationFact]
    public async Task OutboxSoftDeleteEdilseBileAyniBelgeVeIsTuruTekrarKullanilamaz()
    {
        var cut = await CreateAndCutOutgoingInvoiceAsync();
        var sayacOnce = await GetSayacSnapshotAsync();

        await using (var softDeleteCtx = CreateDbContext())
        {
            var mesaj = await softDeleteCtx.EBelgeOutboxMesajlari
                .IgnoreQueryFilters()
                .SingleAsync(x => x.EBelgeKaydi.SatisBelgesiId == cut.Id.Value);

            softDeleteCtx.Remove(mesaj);
            await softDeleteCtx.SaveChangesAsync();
            Assert.True(mesaj.IsDeleted);
        }

        await using (var insertCtx = CreateDbContext())
        {
            var eBelgeKaydiId = await insertCtx.SatisBelgeleri
                .AsNoTracking()
                .Where(x => x.Id == cut.Id.Value)
                .Select(x => x.EBelgeKaydi!.Id)
                .SingleAsync();

            insertCtx.EBelgeOutboxMesajlari.Add(new EBelgeOutboxMesaji
            {
                KurumId = _kurumId,
                EBelgeKaydiId = eBelgeKaydiId,
                IsTuru = EBelgeOutboxIsTuru.ArtefaktOlustur,
                Durum = EBelgeOutboxDurumu.Bekliyor,
                DenemeSayisi = 0
            });

            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => insertCtx.SaveChangesAsync());
            AssertSqlException(ex, new[] { 2601, 2627 }, "IX_EBelgeOutboxMesajlari_EBelgeKaydiId_IsTuru");
        }

        await using var verifyCtx = CreateDbContext();
        var sayacSonra = await GetSayacSnapshotAsync(verifyCtx);
        Assert.Equal(sayacOnce, sayacSonra);
    }

    [IntegrationFact]
    public async Task CrossoverTenantOutboxBaglantisiDbTarafindanReddedilir()
    {
        var cut = await CreateAndCutOutgoingInvoiceAsync();

        await using var verifyCtx = CreateDbContext();
        var eBelgeKaydi = await verifyCtx.EBelgeKayitlari
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(x => x.SatisBelgesiId == cut.Id.Value);
        var mevcutOutbox = await verifyCtx.EBelgeOutboxMesajlari
            .IgnoreQueryFilters()
            .SingleAsync(x => x.EBelgeKaydiId == eBelgeKaydi.Id);

        await verifyCtx.EBelgeOutboxMesajlari
            .IgnoreQueryFilters()
            .Where(x => x.Id == mevcutOutbox.Id)
            .ExecuteDeleteAsync();

        await using var invalidCtx = CreateDbContext();
        invalidCtx.EBelgeOutboxMesajlari.Add(new EBelgeOutboxMesaji
        {
            KurumId = _kurumId + 999,
            EBelgeKaydiId = eBelgeKaydi.Id,
            IsTuru = EBelgeOutboxIsTuru.ArtefaktOlustur,
            Durum = EBelgeOutboxDurumu.Bekliyor,
            DenemeSayisi = 0
        });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => invalidCtx.SaveChangesAsync());
        AssertSqlException(ex, new[] { 547 }, "FK_EBelgeOutboxMesajlari_EBelgeKayitlari_EBelgeKaydiId_KurumId");
    }

    [IntegrationFact]
    public async Task OutboxIndexUniqueVeFiltresizOlmali()
    {
        var connectionString = SatisBelgesiMuhasebeTestSupport.ConnectionString
            ?? throw new InvalidOperationException("Connection string bulunamadi.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
SELECT is_unique, filter_definition
FROM sys.indexes
WHERE name = N'IX_EBelgeOutboxMesajlari_EBelgeKaydiId_IsTuru'
  AND object_id = OBJECT_ID(N'[muhasebe].[EBelgeOutboxMesajlari]')
""";

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0));
        Assert.True(reader.IsDBNull(1));
    }

    [IntegrationFact]
    public async Task MigrationBackfillAktifKayitIcinBirMesajUretir()
    {
        var migrationSql = GetBackfillSqlFromMigration();
        var baseConnectionString = SatisBelgesiMuhasebeTestSupport.ConnectionString
            ?? throw new InvalidOperationException("Connection string bulunamadi.");

        var (databaseName, tempConnectionString) = await CreateTempDatabaseAsync(baseConnectionString);

        try
        {
            await using var connection = new SqlConnection(tempConnectionString);
            await connection.OpenAsync();

            await CreateTempBackfillSchemaAsync(connection);

            await using (var seedCmd = connection.CreateCommand())
            {
                seedCmd.CommandText = """
INSERT INTO muhasebe.EBelgeKayitlari (KurumId, IsDeleted, CreatedAt, CreatedBy) VALUES
    (101, 0, SYSUTCDATETIME(), N'seed:active-1'),
    (102, 1, SYSUTCDATETIME(), N'seed:soft-delete'),
    (103, 0, SYSUTCDATETIME(), N'seed:active-2');

INSERT INTO muhasebe.EBelgeOutboxMesajlari
    (KurumId, EBelgeKaydiId, IsTuru, Durum, DenemeSayisi, SonrakiDenemeZamaniUtc, KilitToken, KilitBitisZamaniUtc,
     IslemBaslamaZamaniUtc, TamamlanmaZamaniUtc, SonHataKodu, SonHataMesaji, IsDeleted, CreatedAt, UpdatedAt, DeletedAt, CreatedBy, UpdatedBy, DeletedBy)
VALUES
    (103, 3, 1, 1, 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, SYSUTCDATETIME(), NULL, NULL, N'seed:existing-outbox', NULL, NULL);
""";
                await seedCmd.ExecuteNonQueryAsync();
            }

            var createdAtBefore = DateTime.UtcNow;
            await using (var backfillCmd = connection.CreateCommand())
            {
                backfillCmd.CommandText = migrationSql;
                await backfillCmd.ExecuteNonQueryAsync();
            }
            var createdAtAfter = DateTime.UtcNow;

            await using (var verifyCmd = connection.CreateCommand())
            {
                verifyCmd.CommandText = """
SELECT COUNT(*) FROM muhasebe.EBelgeOutboxMesajlari;
""";
                var totalCount = (int)await verifyCmd.ExecuteScalarAsync();
                Assert.Equal(2, totalCount);
            }

            await using (var verifyCmd = connection.CreateCommand())
            {
                verifyCmd.CommandText = """
SELECT COUNT(*) FROM muhasebe.EBelgeOutboxMesajlari WHERE EBelgeKaydiId = 1;
""";
                Assert.Equal(1, (int)await verifyCmd.ExecuteScalarAsync());
            }

            await using (var verifyCmd = connection.CreateCommand())
            {
                verifyCmd.CommandText = """
SELECT COUNT(*) FROM muhasebe.EBelgeOutboxMesajlari WHERE EBelgeKaydiId = 2;
""";
                Assert.Equal(0, (int)await verifyCmd.ExecuteScalarAsync());
            }

            await using (var verifyCmd = connection.CreateCommand())
            {
                verifyCmd.CommandText = """
SELECT COUNT(*) FROM muhasebe.EBelgeOutboxMesajlari WHERE EBelgeKaydiId = 3;
""";
                Assert.Equal(1, (int)await verifyCmd.ExecuteScalarAsync());
            }

            await using (var verifyCmd = connection.CreateCommand())
            {
                verifyCmd.CommandText = """
SELECT KurumId, EBelgeKaydiId, IsTuru, Durum, DenemeSayisi, IsDeleted, CreatedAt, CreatedBy
FROM muhasebe.EBelgeOutboxMesajlari
WHERE EBelgeKaydiId = 1;
""";
                await using var reader = await verifyCmd.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.Equal(101, reader.GetInt32(0));
                Assert.Equal(1, reader.GetInt32(1));
                Assert.Equal(1, reader.GetInt32(2));
                Assert.Equal(1, reader.GetInt32(3));
                Assert.Equal(0, reader.GetInt32(4));
                Assert.False(reader.GetBoolean(5));
                var createdAt = reader.GetDateTime(6);
                Assert.InRange(createdAt, createdAtBefore.AddMinutes(-1), createdAtAfter.AddMinutes(1));
                Assert.Equal("migration:20260802214530_AddEBelgeOutboxFaz2A", reader.GetString(7));
                Assert.False(await reader.ReadAsync());
            }

            await using (var secondRunCmd = connection.CreateCommand())
            {
                secondRunCmd.CommandText = migrationSql;
                await secondRunCmd.ExecuteNonQueryAsync();
            }

            await using (var verifyCmd = connection.CreateCommand())
            {
                verifyCmd.CommandText = """
SELECT COUNT(*) FROM muhasebe.EBelgeOutboxMesajlari;
""";
                var totalCount = (int)await verifyCmd.ExecuteScalarAsync();
                Assert.Equal(2, totalCount);
            }

            await using (var verifyCmd = connection.CreateCommand())
            {
                verifyCmd.CommandText = """
SELECT COUNT(*) FROM muhasebe.EBelgeOutboxMesajlari WHERE EBelgeKaydiId = 1;
""";
                Assert.Equal(1, (int)await verifyCmd.ExecuteScalarAsync());
            }

            await using (var verifyCmd = connection.CreateCommand())
            {
                verifyCmd.CommandText = """
SELECT COUNT(*) FROM muhasebe.EBelgeOutboxMesajlari WHERE EBelgeKaydiId = 2;
""";
                Assert.Equal(0, (int)await verifyCmd.ExecuteScalarAsync());
            }

            await using (var verifyCmd = connection.CreateCommand())
            {
                verifyCmd.CommandText = """
SELECT COUNT(*) FROM muhasebe.EBelgeOutboxMesajlari WHERE EBelgeKaydiId = 3;
""";
                Assert.Equal(1, (int)await verifyCmd.ExecuteScalarAsync());
            }
        }
        finally
        {
            await DropTempDatabaseAsync(baseConnectionString, databaseName);
        }
    }

    [IntegrationFact]
    public async Task KesilmisBelgeninOutboxMesajiSilinirseIkinciKesimAcik500VeriTutarsizligiVerir()
    {
        var cut = await CreateAndCutOutgoingInvoiceAsync();
        var ilkDurum = await GetKesimKalintiDurumuAsync(cut.Id!.Value);

        Assert.False(string.IsNullOrWhiteSpace(ilkDurum.ResmiFaturaNo));
        Assert.False(string.IsNullOrWhiteSpace(ilkDurum.EBelgeUuid));
        Assert.Equal(1, ilkDurum.EBelgeKaydiSayisi);
        Assert.Equal(1, ilkDurum.EBelgeSnapshotSayisi);
        Assert.Equal(1, ilkDurum.ToplamOutboxSayisi);
        Assert.Equal(1, ilkDurum.AktifOutboxSayisi);
        Assert.Equal(0, ilkDurum.SoftDeletedOutboxSayisi);

        await using (var deleteCtx = CreateDbContext())
        {
            await deleteCtx.EBelgeOutboxMesajlari
                .IgnoreQueryFilters()
                .Where(x => x.EBelgeKaydi.SatisBelgesiId == cut.Id.Value)
                .ExecuteDeleteAsync();
        }

        await using var kesimCtx = CreateDbContext();
        var kesimService = CreateService(kesimCtx);
        var ex = await Assert.ThrowsAsync<BaseException>(
            () => kesimService.FaturaKesAsync(cut.Id!.Value, new FaturaKesRequest { SeriKodu = "EBF" }, CancellationToken.None));

        Assert.Equal(500, ex.ErrorCode);
        Assert.Contains("EBelgeOutboxMesaji bulunamadı", ex.Message, StringComparison.OrdinalIgnoreCase);

        var sonDurum = await GetKesimKalintiDurumuAsync(cut.Id!.Value);
        Assert.Equal(ilkDurum.ResmiFaturaNo, sonDurum.ResmiFaturaNo);
        Assert.Equal(ilkDurum.EBelgeUuid, sonDurum.EBelgeUuid);
        Assert.Equal(ilkDurum.SonNumara, sonDurum.SonNumara);
        Assert.Equal(1, sonDurum.EBelgeKaydiSayisi);
        Assert.Equal(1, sonDurum.EBelgeSnapshotSayisi);
        Assert.Equal(0, sonDurum.ToplamOutboxSayisi);
        Assert.Equal(0, sonDurum.AktifOutboxSayisi);
        Assert.Equal(0, sonDurum.SoftDeletedOutboxSayisi);
    }

    [IntegrationFact]
    public async Task KesilmisBelgeninOutboxMesajiSoftDeleteEdilirseIkinciKesimAcik500VeriTutarsizligiVerir()
    {
        var cut = await CreateAndCutOutgoingInvoiceAsync();
        var ilkDurum = await GetKesimKalintiDurumuAsync(cut.Id!.Value);

        Assert.False(string.IsNullOrWhiteSpace(ilkDurum.ResmiFaturaNo));
        Assert.False(string.IsNullOrWhiteSpace(ilkDurum.EBelgeUuid));
        Assert.Equal(1, ilkDurum.EBelgeKaydiSayisi);
        Assert.Equal(1, ilkDurum.EBelgeSnapshotSayisi);
        Assert.Equal(1, ilkDurum.ToplamOutboxSayisi);
        Assert.Equal(1, ilkDurum.AktifOutboxSayisi);
        Assert.Equal(0, ilkDurum.SoftDeletedOutboxSayisi);

        await using (var deleteCtx = CreateDbContext())
        {
            var mesaj = await deleteCtx.EBelgeOutboxMesajlari
                .IgnoreQueryFilters()
                .SingleAsync(x => x.EBelgeKaydi.SatisBelgesiId == cut.Id.Value);

            deleteCtx.Remove(mesaj);
            await deleteCtx.SaveChangesAsync();
        }

        await using var kesimCtx = CreateDbContext();
        var kesimService = CreateService(kesimCtx);
        var ex = await Assert.ThrowsAsync<BaseException>(
            () => kesimService.FaturaKesAsync(cut.Id!.Value, new FaturaKesRequest { SeriKodu = "EBF" }, CancellationToken.None));

        Assert.Equal(500, ex.ErrorCode);
        Assert.Contains("soft-delete edilmiş", ex.Message, StringComparison.OrdinalIgnoreCase);
        var sonDurum = await GetKesimKalintiDurumuAsync(cut.Id!.Value);
        Assert.Equal(ilkDurum.ResmiFaturaNo, sonDurum.ResmiFaturaNo);
        Assert.Equal(ilkDurum.EBelgeUuid, sonDurum.EBelgeUuid);
        Assert.Equal(ilkDurum.SonNumara, sonDurum.SonNumara);
        Assert.Equal(1, sonDurum.EBelgeKaydiSayisi);
        Assert.Equal(1, sonDurum.EBelgeSnapshotSayisi);
        Assert.Equal(1, sonDurum.ToplamOutboxSayisi);
        Assert.Equal(0, sonDurum.AktifOutboxSayisi);
        Assert.Equal(1, sonDurum.SoftDeletedOutboxSayisi);
    }

    private async Task<KesimKalintiDurumu> GetKesimKalintiDurumuAsync(int satisBelgesiId)
    {
        await using var verifyCtx = CreateDbContext();

        var belge = await verifyCtx.SatisBelgeleri
            .AsNoTracking()
            .Where(x => x.Id == satisBelgesiId)
            .Select(x => new
            {
                x.ResmiFaturaNo,
                EBelgeUuid = x.EBelgeKaydi!.EBelgeUuid
            })
            .SingleAsync();

        var eBelgeKaydiId = await verifyCtx.EBelgeKayitlari
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.SatisBelgesiId == satisBelgesiId)
            .Select(x => x.Id)
            .SingleAsync();

        var sayac = await verifyCtx.KurumFaturaNumaraSayaclari
            .AsNoTracking()
            .Where(x => x.KurumId == _kurumId && x.MaliYil == 2026 && x.SeriKodu == "EBF")
            .Select(x => x.SonNumara)
            .SingleAsync();

        var toplamOutboxSayisi = await verifyCtx.EBelgeOutboxMesajlari
            .IgnoreQueryFilters()
            .CountAsync(x => x.EBelgeKaydiId == eBelgeKaydiId);

        var aktifOutboxSayisi = await verifyCtx.EBelgeOutboxMesajlari
            .IgnoreQueryFilters()
            .CountAsync(x => x.EBelgeKaydiId == eBelgeKaydiId && !x.IsDeleted);

        var snapshotSayisi = await verifyCtx.EBelgeSnapshots
            .IgnoreQueryFilters()
            .CountAsync(x => x.EBelgeKaydiId == eBelgeKaydiId);

        var eBelgeKaydiSayisi = await verifyCtx.EBelgeKayitlari
            .IgnoreQueryFilters()
            .CountAsync(x => x.SatisBelgesiId == satisBelgesiId);

        return new KesimKalintiDurumu(
            belge.ResmiFaturaNo!,
            belge.EBelgeUuid,
            eBelgeKaydiSayisi,
            snapshotSayisi,
            toplamOutboxSayisi,
            aktifOutboxSayisi,
            toplamOutboxSayisi - aktifOutboxSayisi,
            sayac);
    }

    private sealed record KesimKalintiDurumu(
        string ResmiFaturaNo,
        string EBelgeUuid,
        int EBelgeKaydiSayisi,
        int EBelgeSnapshotSayisi,
        int ToplamOutboxSayisi,
        int AktifOutboxSayisi,
        int SoftDeletedOutboxSayisi,
        int SonNumara);

    private async Task<int> GetSayacSnapshotAsync(StysAppDbContext? dbContext = null)
    {
        if (dbContext is null)
        {
            await using var ctx = CreateDbContext();
            return await ctx.KurumFaturaNumaraSayaclari
                .AsNoTracking()
                .Where(x => x.KurumId == _kurumId && x.MaliYil == 2026 && x.SeriKodu == "EBF")
                .Select(x => x.SonNumara)
                .SingleAsync();
        }

        return await dbContext.KurumFaturaNumaraSayaclari
            .AsNoTracking()
            .Where(x => x.KurumId == _kurumId && x.MaliYil == 2026 && x.SeriKodu == "EBF")
            .Select(x => x.SonNumara)
            .SingleAsync();
    }

    private sealed class TestTenantAccessor : ICurrentTenantAccessor
    {
        private readonly int? _currentKurumId;
        private readonly bool _isSuperAdmin;

        public TestTenantAccessor(int? currentKurumId, bool isSuperAdmin)
        {
            _currentKurumId = currentKurumId;
            _isSuperAdmin = isSuperAdmin;
        }

        public int? GetCurrentKurumId() => _currentKurumId;
        public IReadOnlyList<int> GetAccessibleKurumIds() => _currentKurumId.HasValue ? [_currentKurumId.Value] : [];
        public bool IsSuperAdmin() => _isSuperAdmin;
        public bool IsKurumAdmin() => false;
    }
}
