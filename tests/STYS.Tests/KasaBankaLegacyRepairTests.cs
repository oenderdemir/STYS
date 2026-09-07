using Microsoft.Data.SqlClient;

namespace STYS.Tests;

[Collection(SqlServerIntegrationCollection.Name)]
public class KasaBankaLegacyRepairTests
{
    [IntegrationFact]
    public async Task LegacyFinansalHesap_RepairYalnizFissizGuvenliBelgeleriDegistirir()
    {
        // Always create a dedicated disposable database; never seed or repair the supplied database.
        var builder = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable(IntegrationFactAttribute.ConnectionStringEnvVar));
        builder.InitialCatalog = "master";
        await using var master = new SqlConnection(builder.ConnectionString);
        await master.OpenAsync();
        var database = "STYS_LegacyRepairTest_" + Guid.NewGuid().ToString("N");
        await ExecuteAsync(master, $"CREATE DATABASE [{database}]");
        try
        {
            builder.InitialCatalog = database;
            builder.Pooling = false;
            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            await ExecuteAsync(connection, "CREATE SCHEMA muhasebe");
            await ExecuteAsync(connection, "CREATE SCHEMA kantin");
            await ExecuteAsync(connection, "CREATE SCHEMA entegrasyon");
            await ExecuteAsync(connection, Fixture);
            var root = FindRepositoryRoot();
            var repair = await File.ReadAllTextAsync(Path.Combine(root, "scripts/kasa-banka-legacy-repair.sql"));
            var diagnostic = await File.ReadAllTextAsync(Path.Combine(root, "scripts/kasa-banka-legacy-diagnostic.sql"));
            await using (var command = new SqlCommand(diagnostic, connection))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                Assert.True(await reader.ReadAsync());
                Assert.False(reader.GetBoolean(reader.GetOrdinal("YeniIslemIcinGecerliMi")));
                Assert.Equal(10L, reader.GetInt64(reader.GetOrdinal("TahsilatOdemeBelgesiSayisi")));
                Assert.True(await reader.NextResultAsync());
                var protectedIds = new List<int>();
                while (await reader.ReadAsync())
                    if (reader.GetBoolean(reader.GetOrdinal("MuhasebeGecmisiVarMi")))
                        protectedIds.Add(reader.GetInt32(reader.GetOrdinal("TahsilatOdemeBelgesiId")));
                Assert.Equal(new[] { 2, 3, 4 }, protectedIds.Order().ToArray());
                Assert.True(await reader.NextResultAsync());
                Assert.True(await reader.ReadAsync());
                Assert.Equal(10L, reader.GetInt64(reader.GetOrdinal("KullanimSayisi")));
            }
            // Preview must be a no-op.
            await ExecuteAsync(connection, repair);
            Assert.Equal(10, await ScalarAsync(connection, "SELECT COUNT(*) FROM muhasebe.TahsilatOdemeBelgeleri WHERE KasaBankaHesapId = 1"));
            await ExecuteAsync(connection, repair.Replace("DECLARE @ExecuteRepair bit = 0;", "DECLARE @ExecuteRepair bit = 1;"));
            Assert.Equal(2, await ScalarAsync(connection, "SELECT COUNT(*) FROM muhasebe.TahsilatOdemeBelgeleri WHERE KasaBankaHesapId = 2 AND Id IN (1, 10)"));
            Assert.Equal(8, await ScalarAsync(connection, "SELECT COUNT(*) FROM muhasebe.TahsilatOdemeBelgeleri WHERE KasaBankaHesapId = 1"));
            Assert.Equal(2, await ScalarAsync(connection, "SELECT KasaBankaHesapId FROM dbo.RezervasyonOdemeler WHERE Id = 100"));
            Assert.Equal(2, await ScalarAsync(connection, "SELECT COUNT(*) FROM muhasebe.MuhasebeFisler"));
            Assert.Equal(0, await ScalarAsync(connection, "SELECT COUNT(*) FROM muhasebe.MuhasebeHesapPlanlari WHERE Id = 1 AND (DetayHesapMi = 1 OR HareketGorebilirMi = 1)"));
            // Rerunning must not change already repaired or protected records.
            await ExecuteAsync(connection, repair.Replace("DECLARE @ExecuteRepair bit = 0;", "DECLARE @ExecuteRepair bit = 1;"));
            Assert.Equal(8, await ScalarAsync(connection, "SELECT COUNT(*) FROM muhasebe.TahsilatOdemeBelgeleri WHERE KasaBankaHesapId = 1"));
        }
        finally
        {
            await ExecuteAsync(master, $"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]");
        }
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 60 };
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> ScalarAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "STYS.sln"))) return directory.FullName;
        throw new InvalidOperationException("Repository root not found.");
    }

    // Minimal relational fixture for the script contract. Real SQL Server executes both scripts.
    private const string Fixture = """
        CREATE TABLE muhasebe.MuhasebeHesapPlanlari (Id int PRIMARY KEY, TesisId int NULL, TamKod nvarchar(64),
            AktifMi bit, IsDeleted bit, HareketGorebilirMi bit, DetayHesapMi bit);
        CREATE TABLE muhasebe.KasaBankaHesaplari (Id int PRIMARY KEY, TesisId int NULL, Tip nvarchar(32), Kod nvarchar(64),
            Ad nvarchar(128), MuhasebeHesapPlaniId int NULL, AktifMi bit, IsDeleted bit, ParaBirimi nvarchar(3));
        CREATE TABLE muhasebe.CariKartlar (Id int PRIMARY KEY, TesisId int NULL, IsDeleted bit);
        CREATE TABLE muhasebe.TahsilatOdemeBelgeleri (Id int PRIMARY KEY, BelgeNo nvarchar(64), BelgeTipi nvarchar(16),
            BelgeTarihi datetime2, Durum nvarchar(16), IsDeleted bit, CariKartId int, KasaBankaHesapId int,
            MuhasebeFisId int NULL, MuhasebeFisOlusturmaTarihi datetime2 NULL, KaynakModul nvarchar(64) NULL,
            KaynakId int NULL, ParaBirimi nvarchar(3), Tutar decimal(18,2), OdemeYontemi nvarchar(32), KapatilacakCariHareketId int NULL,
            FOREIGN KEY (KasaBankaHesapId) REFERENCES muhasebe.KasaBankaHesaplari(Id));
        CREATE TABLE dbo.Rezervasyonlar (Id int PRIMARY KEY, TesisId int, IsDeleted bit);
        CREATE TABLE dbo.RezervasyonOdemeler (Id int PRIMARY KEY, RezervasyonId int, TahsilatOdemeBelgesiId int,
            KasaBankaHesapId int, PosOdemeIslemiId int NULL, IsDeleted bit, Durum nvarchar(16), ParaBirimi nvarchar(3),
            OdemeTutari decimal(18,2), OdemeTipi nvarchar(32));
        CREATE TABLE muhasebe.MuhasebeFisler (Id int PRIMARY KEY, KaynakModul nvarchar(64), KaynakId int, Durum nvarchar(16), IsDeleted bit);
        CREATE TABLE muhasebe.MuhasebeFisSatirlari (Id int, MuhasebeFisId int, KasaBankaHesapId int);
        CREATE TABLE muhasebe.KasaHareketleri (Id int, KasaBankaHesapId int, KaynakModul nvarchar(64), KaynakId int);
        CREATE TABLE muhasebe.BankaHareketleri (Id int, KasaBankaHesapId int, KaynakModul nvarchar(64), KaynakId int);
        CREATE TABLE muhasebe.CariHareketler (Id int, KaynakModul nvarchar(64), KaynakId int);
        CREATE TABLE kantin.KantinSatisOdemeleri (Id int, KantinSatisId int, TahsilatOdemeBelgesiId int);
        CREATE TABLE kantin.KantinSatislar (Id int, MuhasebeFisId int);
        CREATE TABLE muhasebe.PosTahsilatValorleri (Id int, TahsilatOdemeBelgesiId int, MuhasebeFisId int NULL);
        CREATE TABLE entegrasyon.PosOdemeIslemleri (Id int, KasaBankaHesapId int, RezervasyonOdemeId int);
        INSERT muhasebe.MuhasebeHesapPlanlari VALUES (1,NULL,'1.10.100',1,0,0,0),(2,1,'1.10.100.001',1,0,1,1),
            (3,3,'1.10.100.002',1,0,1,1),(4,3,'1.10.100.003',1,0,1,1);
        INSERT muhasebe.KasaBankaHesaplari VALUES (1,NULL,'NakitKasa','LEGACY','Legacy',1,1,0,'TRY'),
            (2,1,'NakitKasa','MODERN','Modern',2,1,0,'TRY'),(3,3,'NakitKasa','OTHER1','Other1',3,1,0,'TRY'),
            (4,3,'NakitKasa','OTHER2','Other2',4,1,0,'TRY');
        INSERT muhasebe.CariKartlar VALUES (1,1,0),(2,2,0),(3,3,0);
        INSERT muhasebe.TahsilatOdemeBelgeleri
            (Id,BelgeNo,BelgeTipi,BelgeTarihi,Durum,IsDeleted,CariKartId,KasaBankaHesapId,ParaBirimi,Tutar,OdemeYontemi)
        SELECT n,CONCAT('TEST-',n),'Tahsilat','20260901','Aktif',0,1,1,'TRY',100,'Nakit'
        FROM (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10)) v(n);
        UPDATE muhasebe.TahsilatOdemeBelgeleri SET MuhasebeFisId = 20 WHERE Id = 2;
        INSERT muhasebe.MuhasebeFisler VALUES (30,'TahsilatOdemeBelgesi',3,'Onayli',0),(40,'TahsilatOdemeBelgesi',4,'TersKayit',1);
        INSERT muhasebe.MuhasebeFisSatirlari VALUES (30,30,1),(40,40,1);
        UPDATE muhasebe.TahsilatOdemeBelgeleri SET CariKartId = 2 WHERE Id = 5;
        UPDATE muhasebe.TahsilatOdemeBelgeleri SET CariKartId = 3 WHERE Id = 6;
        INSERT muhasebe.PosTahsilatValorleri VALUES (7,7,NULL);
        INSERT muhasebe.KasaHareketleri VALUES (8,1,'TahsilatOdemeBelgesi',8);
        UPDATE muhasebe.TahsilatOdemeBelgeleri SET ParaBirimi = 'USD' WHERE Id = 9;
        UPDATE muhasebe.TahsilatOdemeBelgeleri SET KaynakModul = 'Rezervasyon', KaynakId = 100 WHERE Id = 10;
        INSERT dbo.Rezervasyonlar VALUES (1,1,0);
        INSERT dbo.RezervasyonOdemeler VALUES (100,1,10,1,NULL,0,'Aktif','TRY',100,'Nakit');
        """;
}
