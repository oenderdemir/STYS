using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class FazQ1_RemoveCariKartLegacyBankaFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
INSERT INTO [muhasebe].[CariKartBankaHesaplari]
(
    [CariKartId],
    [BankaAdi],
    [SubeAdi],
    [HesapNo],
    [Iban],
    [Aciklama],
    [IsDeleted],
    [CreatedAt],
    [UpdatedAt],
    [DeletedAt],
    [CreatedBy],
    [UpdatedBy],
    [DeletedBy]
)
SELECT
    c.[Id],
    c.[BankaAdi],
    NULL,
    NULL,
    c.[Iban],
    N'Legacy cari kart banka bilgisi',
    0,
    SYSUTCDATETIME(),
    NULL,
    NULL,
    NULL,
    NULL,
    NULL
FROM [muhasebe].[CariKartlar] c
WHERE (c.[BankaAdi] IS NOT NULL OR c.[Iban] IS NOT NULL)
  AND NOT EXISTS
  (
      SELECT 1
      FROM [muhasebe].[CariKartBankaHesaplari] b
      WHERE b.[CariKartId] = c.[Id]
        AND b.[IsDeleted] = 0
  );
");

            migrationBuilder.DropColumn(
                name: "BankaAdi",
                schema: "muhasebe",
                table: "CariKartlar");

            migrationBuilder.DropColumn(
                name: "Iban",
                schema: "muhasebe",
                table: "CariKartlar");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Veri geri dönüşü tam garanti edilmez; kolonlar yalnızca şema olarak geri eklenir.
            migrationBuilder.AddColumn<string>(
                name: "BankaAdi",
                schema: "muhasebe",
                table: "CariKartlar",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Iban",
                schema: "muhasebe",
                table: "CariKartlar",
                type: "nvarchar(34)",
                maxLength: 34,
                nullable: true);
        }
    }
}
