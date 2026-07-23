using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddPosValorFisNoSayaci : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PosValorFisNoSayaclari",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    MaliYil = table.Column<int>(type: "int", nullable: false),
                    SonNumara = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_PosValorFisNoSayaclari", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PosValorFisNoSayaclari_TesisId_MaliYil",
                schema: "muhasebe",
                table: "PosValorFisNoSayaclari",
                columns: new[] { "TesisId", "MaliYil" },
                unique: true,
                filter: "[IsDeleted] = 0");

            // Mevcut MuhasebeFisler icinde KaynakModul=PosTahsilatValorTransferi olan, "{MaliYil}-VLR-NNNNNN"
            // formatindaki fisleri tesis+mali yil bazinda tarayip sayaci GERCEK en buyuk numaraya gore
            // baslatir. Idempotent: NOT EXISTS ile korunur, migration birden fazla kez calistirilsa
            // (ornegin script olarak yeniden uygulansa) mevcut sayac satirlarinin uzerine yazmaz.
            migrationBuilder.Sql(
                """
                SET NOCOUNT ON;

                INSERT INTO [muhasebe].[PosValorFisNoSayaclari]
                    ([TesisId], [MaliYil], [SonNumara], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                SELECT
                    f.[TesisId],
                    f.[MaliYil],
                    MAX(TRY_CAST(RIGHT(f.[FisNo], 6) AS int)),
                    0,
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME(),
                    N'migration_pos_valor_fis_no_sayaci',
                    N'migration_pos_valor_fis_no_sayaci'
                FROM [muhasebe].[MuhasebeFisler] f
                WHERE f.[KaynakModul] = N'PosTahsilatValorTransferi'
                  AND f.[IsDeleted] = 0
                  AND f.[FisNo] LIKE '%-VLR-[0-9][0-9][0-9][0-9][0-9][0-9]'
                GROUP BY f.[TesisId], f.[MaliYil]
                HAVING MAX(TRY_CAST(RIGHT(f.[FisNo], 6) AS int)) IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1 FROM [muhasebe].[PosValorFisNoSayaclari] s
                       WHERE s.[TesisId] = f.[TesisId] AND s.[MaliYil] = f.[MaliYil] AND s.[IsDeleted] = 0
                   );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PosValorFisNoSayaclari",
                schema: "muhasebe");
        }
    }
}
