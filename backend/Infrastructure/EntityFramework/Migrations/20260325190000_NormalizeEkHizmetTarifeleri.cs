using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    [DbContext(typeof(StysAppDbContext))]
    [Migration("20260325190000_NormalizeEkHizmetTarifeleri")]
    public partial class NormalizeEkHizmetTarifeleri : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EkHizmetler",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    BirimAdi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EkHizmetler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EkHizmetler_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddColumn<int>(
                name: "EkHizmetId",
                schema: "dbo",
                table: "EkHizmetTarifeleri",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EkHizmetId",
                schema: "dbo",
                table: "RezervasyonEkHizmetler",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                INSERT INTO [dbo].[EkHizmetler] ([TesisId], [Ad], [Aciklama], [BirimAdi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [DeletedAt], [CreatedBy], [UpdatedBy], [DeletedBy])
                SELECT DISTINCT
                    t.[TesisId],
                    t.[Ad],
                    t.[Aciklama],
                    t.[BirimAdi],
                    CAST(1 AS bit),
                    CAST(0 AS bit),
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME(),
                    NULL,
                    N'migration',
                    N'migration',
                    NULL
                FROM [dbo].[EkHizmetTarifeleri] t
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[EkHizmetler] e
                    WHERE e.[TesisId] = t.[TesisId]
                      AND e.[Ad] = t.[Ad]
                      AND ISNULL(e.[Aciklama], N'') = ISNULL(t.[Aciklama], N'')
                      AND e.[BirimAdi] = t.[BirimAdi]
                      AND e.[IsDeleted] = 0
                );
                """);

            migrationBuilder.Sql(
                """
                UPDATE t
                SET t.[EkHizmetId] = e.[Id]
                FROM [dbo].[EkHizmetTarifeleri] t
                INNER JOIN [dbo].[EkHizmetler] e
                    ON e.[TesisId] = t.[TesisId]
                   AND e.[Ad] = t.[Ad]
                   AND ISNULL(e.[Aciklama], N'') = ISNULL(t.[Aciklama], N'')
                   AND e.[BirimAdi] = t.[BirimAdi]
                   AND e.[IsDeleted] = 0;
                """);

            migrationBuilder.Sql(
                """
                UPDATE r
                SET r.[EkHizmetId] = t.[EkHizmetId]
                FROM [dbo].[RezervasyonEkHizmetler] r
                INNER JOIN [dbo].[EkHizmetTarifeleri] t ON t.[Id] = r.[EkHizmetTarifeId];
                """);

            migrationBuilder.AlterColumn<int>(
                name: "EkHizmetId",
                schema: "dbo",
                table: "RezervasyonEkHizmetler",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "EkHizmetId",
                schema: "dbo",
                table: "EkHizmetTarifeleri",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.DropIndex(
                name: "IX_EkHizmetTarifeleri_TesisId_Ad_BaslangicTarihi_BitisTarihi",
                schema: "dbo",
                table: "EkHizmetTarifeleri");

            migrationBuilder.DropColumn(
                name: "Aciklama",
                schema: "dbo",
                table: "EkHizmetTarifeleri");

            migrationBuilder.DropColumn(
                name: "Ad",
                schema: "dbo",
                table: "EkHizmetTarifeleri");

            migrationBuilder.DropColumn(
                name: "BirimAdi",
                schema: "dbo",
                table: "EkHizmetTarifeleri");

            migrationBuilder.CreateIndex(
                name: "IX_EkHizmetler_TesisId_Ad",
                schema: "dbo",
                table: "EkHizmetler",
                columns: new[] { "TesisId", "Ad" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_EkHizmetTarifeleri_EkHizmetId",
                schema: "dbo",
                table: "EkHizmetTarifeleri",
                column: "EkHizmetId");

            migrationBuilder.CreateIndex(
                name: "IX_EkHizmetTarifeleri_TesisId_EkHizmetId_BaslangicTarihi_BitisTarihi",
                schema: "dbo",
                table: "EkHizmetTarifeleri",
                columns: new[] { "TesisId", "EkHizmetId", "BaslangicTarihi", "BitisTarihi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonEkHizmetler_EkHizmetId",
                schema: "dbo",
                table: "RezervasyonEkHizmetler",
                column: "EkHizmetId");

            migrationBuilder.AddForeignKey(
                name: "FK_EkHizmetTarifeleri_EkHizmetler_EkHizmetId",
                schema: "dbo",
                table: "EkHizmetTarifeleri",
                column: "EkHizmetId",
                principalSchema: "dbo",
                principalTable: "EkHizmetler",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RezervasyonEkHizmetler_EkHizmetler_EkHizmetId",
                schema: "dbo",
                table: "RezervasyonEkHizmetler",
                column: "EkHizmetId",
                principalSchema: "dbo",
                principalTable: "EkHizmetler",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EkHizmetTarifeleri_EkHizmetler_EkHizmetId",
                schema: "dbo",
                table: "EkHizmetTarifeleri");

            migrationBuilder.DropForeignKey(
                name: "FK_RezervasyonEkHizmetler_EkHizmetler_EkHizmetId",
                schema: "dbo",
                table: "RezervasyonEkHizmetler");

            migrationBuilder.DropIndex(
                name: "IX_EkHizmetTarifeleri_EkHizmetId",
                schema: "dbo",
                table: "EkHizmetTarifeleri");

            migrationBuilder.DropIndex(
                name: "IX_EkHizmetTarifeleri_TesisId_EkHizmetId_BaslangicTarihi_BitisTarihi",
                schema: "dbo",
                table: "EkHizmetTarifeleri");

            migrationBuilder.DropIndex(
                name: "IX_RezervasyonEkHizmetler_EkHizmetId",
                schema: "dbo",
                table: "RezervasyonEkHizmetler");

            migrationBuilder.AddColumn<string>(
                name: "Aciklama",
                schema: "dbo",
                table: "EkHizmetTarifeleri",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ad",
                schema: "dbo",
                table: "EkHizmetTarifeleri",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BirimAdi",
                schema: "dbo",
                table: "EkHizmetTarifeleri",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Adet");

            migrationBuilder.Sql(
                """
                UPDATE t
                SET
                    t.[Ad] = e.[Ad],
                    t.[Aciklama] = e.[Aciklama],
                    t.[BirimAdi] = e.[BirimAdi]
                FROM [dbo].[EkHizmetTarifeleri] t
                INNER JOIN [dbo].[EkHizmetler] e ON e.[Id] = t.[EkHizmetId];
                """);

            migrationBuilder.CreateIndex(
                name: "IX_EkHizmetTarifeleri_TesisId_Ad_BaslangicTarihi_BitisTarihi",
                schema: "dbo",
                table: "EkHizmetTarifeleri",
                columns: new[] { "TesisId", "Ad", "BaslangicTarihi", "BitisTarihi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.DropColumn(
                name: "EkHizmetId",
                schema: "dbo",
                table: "RezervasyonEkHizmetler");

            migrationBuilder.DropColumn(
                name: "EkHizmetId",
                schema: "dbo",
                table: "EkHizmetTarifeleri");

            migrationBuilder.DropTable(
                name: "EkHizmetler",
                schema: "dbo");
        }
    }
}
