using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class FixMuhasebeFisKaynakUniqueIndexForTersKayit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF EXISTS (
    SELECT 1
    FROM sys.indexes i
    INNER JOIN sys.tables t ON i.object_id = t.object_id
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE i.name = 'IX_MuhasebeFisler_KaynakModul_KaynakId'
      AND t.name = 'MuhasebeFisler'
      AND s.name = 'muhasebe'
)
BEGIN
    DROP INDEX [IX_MuhasebeFisler_KaynakModul_KaynakId]
    ON [muhasebe].[MuhasebeFisler];
END
""");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeFisler_KaynakModul_KaynakId",
                schema: "muhasebe",
                table: "MuhasebeFisler",
                columns: new[] { "KaynakModul", "KaynakId" },
                unique: true,
                filter: "[KaynakModul] IS NOT NULL AND [KaynakId] IS NOT NULL AND [IsDeleted] = 0 AND [Durum] <> N'Iptal' AND [Durum] <> N'TersKayit'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF EXISTS (
    SELECT 1
    FROM sys.indexes i
    INNER JOIN sys.tables t ON i.object_id = t.object_id
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE i.name = 'IX_MuhasebeFisler_KaynakModul_KaynakId'
      AND t.name = 'MuhasebeFisler'
      AND s.name = 'muhasebe'
)
BEGIN
    DROP INDEX [IX_MuhasebeFisler_KaynakModul_KaynakId]
    ON [muhasebe].[MuhasebeFisler];
END
""");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeFisler_KaynakModul_KaynakId",
                schema: "muhasebe",
                table: "MuhasebeFisler",
                columns: new[] { "KaynakModul", "KaynakId" },
                unique: true,
                filter: "[KaynakModul] IS NOT NULL AND [KaynakId] IS NOT NULL AND [IsDeleted] = 0");
        }
    }
}