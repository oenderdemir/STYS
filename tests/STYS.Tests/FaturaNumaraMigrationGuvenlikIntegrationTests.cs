using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// AddSatisBelgesiKurumSahipligiVeFaturaNumaraSayaci migration'ının, TesisId üzerinden geçerli
/// bir Kuruma bağlanamayan legacy SatisBelgesi kayıtlarına ASLA varsayılan/ilk/rastgele bir Kurum
/// atamadığını, bunun yerine açık ve anlaşılır bir hata ile (eşleşmeyen kayıt sayısını belirterek)
/// durduğunu doğrular.
///
/// NOT: Bu görevin çalıştığı ortamda, migration geçmişini SIFIRDAN (boş bir veritabanında) tam
/// olarak replaylemek mümkün değildir - geçmişteki bazı migration'lar bu görevle ilgisiz şekilde
/// paylaşılan platform veritabanlarına (ör. [TODBase].[MenuItemRoles]) çapraz-veritabanı referans
/// içerir ve bu veritabanı taze/izole bir test veritabanının yanında mevcut değildir. Bu yüzden:
/// 1) Migration'ın Up() metodundaki backfill/guard T-SQL mantığı, GERÇEK SQL Server'a karşı,
///    yalnızca ilgili sütunları içeren minimal bir şema üzerinde BİREBİR AYNI script ile çalıştırılır
///    (SqlServerLogicTest) — bu, RAISERROR/hata mesajı/satır sayısı davranışının fiilen SQL Server
///    tarafından doğru yürütüldüğünü kanıtlar.
/// 2) Migration dosyasının KAYNAK METNİ, eski "ilk aktif kuruma ata" davranışının GERÇEKTEN
///    kaldırıldığını ve yeni nullable-önce/backfill/açık-hata deseninin GERÇEKTEN mevcut olduğunu
///    doğrulamak için okunur (MigrationKaynagiIcerikKontrolu) — DB gerektirmez, migration dosyası
///    yanlışlıkla eski davranışa geri alınırsa/hedef testler unutulursa bu testi kırar.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class FaturaNumaraMigrationGuvenlikIntegrationTests
{
    private static string GetMigrationFilePath([CallerFilePath] string testFilePath = "")
    {
        // tests/STYS.Tests/FaturaNumaraMigrationGuvenlikIntegrationTests.cs -> repo kökü -> migration dosyası
        var testsDir = Path.GetDirectoryName(testFilePath)!;
        var repoRoot = Path.GetFullPath(Path.Combine(testsDir, "..", ".."));
        return Path.Combine(
            repoRoot,
            "backend", "Infrastructure", "EntityFramework", "Migrations",
            "20260730184240_AddSatisBelgesiKurumSahipligiVeFaturaNumaraSayaci.cs");
    }

    [Fact]
    public void MigrationKaynagiIcerikKontrolu_VarsayilanKurumAtamaDeseniKaldirilmisNullableOnceBackfillMevcut()
    {
        var migrationPath = GetMigrationFilePath();
        Assert.True(File.Exists(migrationPath), $"Migration dosyası bulunamadı: {migrationPath}");

        var kaynak = File.ReadAllText(migrationPath);

        // Eski, KALDIRILMASI GEREKEN davranış: hiçbir yerde "ilk/varsayılan aktif kuruma ata" SQL'i
        // KALMAMALI.
        Assert.DoesNotContain("varsayilanKurumId", kaynak);
        Assert.DoesNotContain("SELECT TOP 1", kaynak, StringComparison.OrdinalIgnoreCase);

        // Yeni davranış: KurumId önce NULLABLE eklenir (kalıcı defaultValue: 0 YOKTUR).
        Assert.Contains("nullable: true", kaynak);
        Assert.DoesNotContain("defaultValue: 0", kaynak);

        // Backfill sonrası kolon NOT NULL yapılır (AlterColumn ile, oldNullable: true'dan).
        Assert.Contains("AlterColumn<int>", kaynak);
        Assert.Contains("oldNullable: true", kaynak);

        // Eşleşmeyen kayıt sayısını belirten açık hata mesajı mevcut.
        Assert.Contains("KurumId backfill basarisiz", kaynak);
        Assert.Contains("eslesmeyenSayisi", kaynak);
        Assert.Contains("varsayilan/ilk/rastgele bir Kurum ATANMAZ", kaynak);

        // Mevcut ResmiFaturaNo mükerrer kontrolü korunmuş.
        Assert.Contains("mukerrer kayitlar tespit edildi", kaynak);

        // KurumId FK/index'leri korunmuş.
        Assert.Contains("FK_SatisBelgeleri_Kurumlar_KurumId", kaynak);
        Assert.Contains("IX_SatisBelgeleri_KurumId_ResmiFaturaNo", kaynak);
    }

    [IntegrationFact]
    public async Task SqlServerLogicTest_TesisiBulunamayanLegacyBelgeyeVarsayilanKurumAtanmaz_AcikHatailaDurur()
    {
        var baseConnectionString = SatisBelgesiMuhasebeTestSupport.ConnectionString!;
        var tempDbName = $"STYSDB_MigTest_{Guid.NewGuid():N}"[..30];

        var masterConnectionString = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = "master"
        }.ConnectionString;

        var tempConnectionString = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = tempDbName
        }.ConnectionString;

        await using (var masterConnection = new SqlConnection(masterConnectionString))
        {
            await masterConnection.OpenAsync();
            await using var createCmd = masterConnection.CreateCommand();
            createCmd.CommandText = $"CREATE DATABASE [{tempDbName}]";
            await createCmd.ExecuteNonQueryAsync();
        }

        try
        {
            await using var connection = new SqlConnection(tempConnectionString);
            await connection.OpenAsync();

            await using (var setupCmd = connection.CreateCommand())
            {
                setupCmd.CommandText = @"
CREATE SCHEMA muhasebe;
";
                await setupCmd.ExecuteNonQueryAsync();
            }

            await using (var setupCmd = connection.CreateCommand())
            {
                setupCmd.CommandText = @"
CREATE TABLE dbo.Kurumlar (Id INT IDENTITY PRIMARY KEY, IsDeleted BIT NOT NULL DEFAULT 0);
CREATE TABLE dbo.Tesisler (Id INT IDENTITY PRIMARY KEY, KurumId INT NOT NULL);
CREATE TABLE muhasebe.SatisBelgeleri (
    Id INT IDENTITY PRIMARY KEY,
    TesisId INT NULL,
    ResmiFaturaNo NVARCHAR(50) NULL,
    IsDeleted BIT NOT NULL DEFAULT 0
);
";
                await setupCmd.ExecuteNonQueryAsync();
            }

            // Legacy belge: TesisId NULL - geçerli bir Tesis üzerinden HİÇBİR şekilde bir Kuruma
            // bağlanamaz.
            await using (var seedCmd = connection.CreateCommand())
            {
                seedCmd.CommandText = "INSERT INTO muhasebe.SatisBelgeleri (TesisId, IsDeleted) VALUES (NULL, 0);";
                await seedCmd.ExecuteNonQueryAsync();
            }

            // ── Migration Up()'taki adımlarla BİREBİR AYNI T-SQL ──
            await using (var addColCmd = connection.CreateCommand())
            {
                addColCmd.CommandText = "ALTER TABLE muhasebe.SatisBelgeleri ADD KurumId INT NULL;";
                await addColCmd.ExecuteNonQueryAsync();
            }

            await using (var backfillCmd = connection.CreateCommand())
            {
                backfillCmd.CommandText = @"
UPDATE sb
SET sb.KurumId = t.KurumId
FROM muhasebe.SatisBelgeleri sb
INNER JOIN dbo.Tesisler t ON t.Id = sb.TesisId
WHERE sb.TesisId IS NOT NULL;
";
                await backfillCmd.ExecuteNonQueryAsync();
            }

            var ex = await Assert.ThrowsAsync<SqlException>(async () =>
            {
                await using var guardCmd = connection.CreateCommand();
                guardCmd.CommandText = @"
DECLARE @eslesmeyenSayisi INT;
SELECT @eslesmeyenSayisi = COUNT(*) FROM muhasebe.SatisBelgeleri WHERE KurumId IS NULL;

IF @eslesmeyenSayisi > 0
BEGIN
    DECLARE @hataMesaji NVARCHAR(4000) = N'SatisBelgeleri.KurumId backfill basarisiz: ' +
        CAST(@eslesmeyenSayisi AS NVARCHAR(20)) +
        N' kayit gecerli bir Tesis uzerinden bir Kuruma baglanamadi (TesisId NULL veya Tesisler tablosunda bulunamiyor). ' +
        N'Bu kayitlara varsayilan/ilk/rastgele bir Kurum ATANMAZ - yanlis tenant sahipligi ve kurumlar arasi veri sizintisi olusturabilir. ' +
        N'Migration durduruldu; lutfen bu kayitlari elle inceleyip dogru TesisId/KurumId ile eslestirin (veya gecersizse soft-delete edin), sonra migration''i tekrar calistirin.';
    RAISERROR(@hataMesaji, 16, 1);
END
";
                await guardCmd.ExecuteNonQueryAsync();
            });

            Assert.Contains("KurumId backfill basarisiz", ex.Message);
            Assert.Contains("1 kayit", ex.Message);
            Assert.Contains("varsayilan/ilk/rastgele bir Kurum ATANMAZ", ex.Message);

            // Doğrulama: satır hâlâ KurumId=NULL - hiçbir varsayılan/ilk Kuruma atanmadı.
            await using var verifyCmd = connection.CreateCommand();
            verifyCmd.CommandText = "SELECT KurumId FROM muhasebe.SatisBelgeleri WHERE TesisId IS NULL;";
            var kurumIdDb = await verifyCmd.ExecuteScalarAsync();
            Assert.True(kurumIdDb is null or DBNull);
        }
        finally
        {
            await using var masterConnection = new SqlConnection(masterConnectionString);
            await masterConnection.OpenAsync();
            await using var dropCmd = masterConnection.CreateCommand();
            dropCmd.CommandText = $@"
ALTER DATABASE [{tempDbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE [{tempDbName}];";
            await dropCmd.ExecuteNonQueryAsync();
        }
    }
}
