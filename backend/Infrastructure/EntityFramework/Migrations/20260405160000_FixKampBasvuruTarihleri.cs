using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260405160000_FixKampBasvuruTarihleri")]
public partial class FixKampBasvuruTarihleri : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Talimat: "Basvurularin, en gec 02 Mayis 2025 Cuma gunu mesai saati bitimine kadar yapilmasi gerekmektedir."
        // Tum donemler icin tek basvuru bitis tarihi: 2025-05-02
        // Basvuru baslangicindan bahsedilmiyor ama makul bir acilis tarihi olarak 2025-03-01 kullaniyoruz.
        migrationBuilder.Sql(
            """
            DECLARE @ProgramId int = (SELECT TOP 1 [Id] FROM [dbo].[KampProgramlari] WHERE [Kod] = N'YAZ_KAMPI');

            IF @ProgramId IS NOT NULL
            BEGIN
                UPDATE [dbo].[KampDonemleri]
                SET [BasvuruBaslangicTarihi] = '2025-03-01',
                    [BasvuruBitisTarihi] = '2025-05-02'
                WHERE [KampProgramiId] = @ProgramId
                  AND [Kod] LIKE N'2025-YAZ-%';
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Geri alinirsa orijinal seed daki dinamik tarihlere donulur
        migrationBuilder.Sql(
            """
            DECLARE @ProgramId int = (SELECT TOP 1 [Id] FROM [dbo].[KampProgramlari] WHERE [Kod] = N'YAZ_KAMPI');

            IF @ProgramId IS NOT NULL
            BEGIN
                UPDATE d
                SET d.[BasvuruBaslangicTarihi] = DATEADD(day, -30, d.[KonaklamaBaslangicTarihi]),
                    d.[BasvuruBitisTarihi] = DATEADD(day, -7, d.[KonaklamaBaslangicTarihi])
                FROM [dbo].[KampDonemleri] d
                WHERE d.[KampProgramiId] = @ProgramId
                  AND d.[Kod] LIKE N'2025-YAZ-%';
            END
            """);
    }
}
