using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddStokMaliyetPolitikasiPerFiscalYear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StokMaliyetPolitikalari",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    MaliYil = table.Column<int>(type: "int", nullable: false),
                    MaliyetYontemi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StokMaliyetPolitikalari", x => x.Id);
                    table.CheckConstraint("CK_StokMaliyetPolitikalari_MaliyetYontemi", "[MaliyetYontemi] IN (N'AgirlikliOrtalama', N'FIFO', N'LIFO')");
                    table.ForeignKey(
                        name: "FK_StokMaliyetPolitikalari_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StokMaliyetPolitikalari_TesisId_MaliYil",
                schema: "muhasebe",
                table: "StokMaliyetPolitikalari",
                columns: new[] { "TesisId", "MaliYil" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.Sql(
                """
                INSERT INTO [muhasebe].[StokMaliyetPolitikalari]
                    ([TesisId], [MaliYil], [MaliyetYontemi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                SELECT DISTINCT
                    d.[TesisId],
                    md.[MaliYil],
                    N'AgirlikliOrtalama',
                    0,
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME(),
                    N'migration',
                    N'migration'
                FROM [muhasebe].[StokHareketleri] sh
                INNER JOIN [muhasebe].[Depolar] d ON d.[Id] = sh.[DepoId] AND d.[IsDeleted] = 0
                INNER JOIN [muhasebe].[MuhasebeDonemler] md
                    ON md.[TesisId] = d.[TesisId]
                    AND md.[IsDeleted] = 0
                    AND sh.[HareketTarihi] >= md.[BaslangicTarihi]
                    AND sh.[HareketTarihi] <= md.[BitisTarihi]
                WHERE sh.[IsDeleted] = 0
                  AND (sh.[MaliyetBirimFiyat] IS NOT NULL OR sh.[MaliyetTutari] IS NOT NULL)
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM [muhasebe].[StokMaliyetPolitikalari] p
                      WHERE p.[IsDeleted] = 0
                        AND p.[TesisId] = d.[TesisId]
                        AND p.[MaliYil] = md.[MaliYil]
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StokMaliyetPolitikalari",
                schema: "muhasebe");
        }
    }
}
