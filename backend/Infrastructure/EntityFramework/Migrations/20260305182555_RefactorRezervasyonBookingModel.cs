using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class RefactorRezervasyonBookingModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Rezervasyonlar_Odalar_OdaId",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropIndex(
                name: "IX_Rezervasyonlar_OdaId_GirisTarihi_CikisTarihi",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.AddColumn<int>(
                name: "TesisId",
                schema: "dbo",
                table: "Rezervasyonlar",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MisafirAdiSoyadi",
                schema: "dbo",
                table: "Rezervasyonlar",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "Bilinmeyen Misafir");

            migrationBuilder.AddColumn<string>(
                name: "MisafirEposta",
                schema: "dbo",
                table: "Rezervasyonlar",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MisafirTelefon",
                schema: "dbo",
                table: "Rezervasyonlar",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "-");

            migrationBuilder.AddColumn<string>(
                name: "Notlar",
                schema: "dbo",
                table: "Rezervasyonlar",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasaportNo",
                schema: "dbo",
                table: "Rezervasyonlar",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RezervasyonDurumu",
                schema: "dbo",
                table: "Rezervasyonlar",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Onayli");

            migrationBuilder.AddColumn<string>(
                name: "TcKimlikNo",
                schema: "dbo",
                table: "Rezervasyonlar",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RezervasyonSegmentleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RezervasyonId = table.Column<int>(type: "int", nullable: false),
                    SegmentSirasi = table.Column<int>(type: "int", nullable: false),
                    BaslangicTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BitisTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_RezervasyonSegmentleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RezervasyonSegmentleri_Rezervasyonlar_RezervasyonId",
                        column: x => x.RezervasyonId,
                        principalSchema: "dbo",
                        principalTable: "Rezervasyonlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RezervasyonSegmentOdaAtamalari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RezervasyonSegmentId = table.Column<int>(type: "int", nullable: false),
                    OdaId = table.Column<int>(type: "int", nullable: false),
                    AyrilanKisiSayisi = table.Column<int>(type: "int", nullable: false),
                    OdaNoSnapshot = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BinaAdiSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OdaTipiAdiSnapshot = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PaylasimliMiSnapshot = table.Column<bool>(type: "bit", nullable: false),
                    KapasiteSnapshot = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_RezervasyonSegmentOdaAtamalari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RezervasyonSegmentOdaAtamalari_Odalar_OdaId",
                        column: x => x.OdaId,
                        principalSchema: "dbo",
                        principalTable: "Odalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RezervasyonSegmentOdaAtamalari_RezervasyonSegmentleri_RezervasyonSegmentId",
                        column: x => x.RezervasyonSegmentId,
                        principalSchema: "dbo",
                        principalTable: "RezervasyonSegmentleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Rezervasyonlar_RezervasyonDurumu",
                schema: "dbo",
                table: "Rezervasyonlar",
                column: "RezervasyonDurumu",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonSegmentleri_BaslangicTarihi_BitisTarihi",
                schema: "dbo",
                table: "RezervasyonSegmentleri",
                columns: new[] { "BaslangicTarihi", "BitisTarihi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonSegmentleri_RezervasyonId",
                schema: "dbo",
                table: "RezervasyonSegmentleri",
                column: "RezervasyonId");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonSegmentleri_RezervasyonId_SegmentSirasi",
                schema: "dbo",
                table: "RezervasyonSegmentleri",
                columns: new[] { "RezervasyonId", "SegmentSirasi" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonSegmentOdaAtamalari_OdaId",
                schema: "dbo",
                table: "RezervasyonSegmentOdaAtamalari",
                column: "OdaId",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonSegmentOdaAtamalari_RezervasyonSegmentId_OdaId",
                schema: "dbo",
                table: "RezervasyonSegmentOdaAtamalari",
                columns: new[] { "RezervasyonSegmentId", "OdaId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.Sql("""
                UPDATE r
                SET r.TesisId = b.TesisId
                FROM dbo.Rezervasyonlar r
                INNER JOIN dbo.Odalar o ON o.Id = r.OdaId
                INNER JOIN dbo.Binalar b ON b.Id = o.BinaId
                WHERE r.TesisId IS NULL;

                IF EXISTS (SELECT 1 FROM dbo.Rezervasyonlar WHERE TesisId IS NULL)
                BEGIN
                    THROW 50001, 'Rezervasyon TesisId donusumu basarisiz oldu.', 1;
                END;

                INSERT INTO dbo.RezervasyonSegmentleri (
                    RezervasyonId,
                    SegmentSirasi,
                    BaslangicTarihi,
                    BitisTarihi,
                    IsDeleted,
                    CreatedAt,
                    UpdatedAt,
                    DeletedAt,
                    CreatedBy,
                    UpdatedBy,
                    DeletedBy
                )
                SELECT
                    r.Id,
                    1,
                    r.GirisTarihi,
                    r.CikisTarihi,
                    r.IsDeleted,
                    r.CreatedAt,
                    r.UpdatedAt,
                    r.DeletedAt,
                    r.CreatedBy,
                    r.UpdatedBy,
                    r.DeletedBy
                FROM dbo.Rezervasyonlar r
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM dbo.RezervasyonSegmentleri rs
                    WHERE rs.RezervasyonId = r.Id
                );

                INSERT INTO dbo.RezervasyonSegmentOdaAtamalari (
                    RezervasyonSegmentId,
                    OdaId,
                    AyrilanKisiSayisi,
                    OdaNoSnapshot,
                    BinaAdiSnapshot,
                    OdaTipiAdiSnapshot,
                    PaylasimliMiSnapshot,
                    KapasiteSnapshot,
                    IsDeleted,
                    CreatedAt,
                    UpdatedAt,
                    DeletedAt,
                    CreatedBy,
                    UpdatedBy,
                    DeletedBy
                )
                SELECT
                    rs.Id,
                    r.OdaId,
                    r.KisiSayisi,
                    COALESCE(NULLIF(r.OdaNoSnapshot, ''), o.OdaNo),
                    b.Ad,
                    COALESCE(NULLIF(r.OdaTipiAdiSnapshot, ''), ot.Ad),
                    ot.PaylasimliMi,
                    ot.Kapasite,
                    r.IsDeleted,
                    r.CreatedAt,
                    r.UpdatedAt,
                    r.DeletedAt,
                    r.CreatedBy,
                    r.UpdatedBy,
                    r.DeletedBy
                FROM dbo.Rezervasyonlar r
                INNER JOIN dbo.RezervasyonSegmentleri rs ON rs.RezervasyonId = r.Id AND rs.SegmentSirasi = 1
                INNER JOIN dbo.Odalar o ON o.Id = r.OdaId
                INNER JOIN dbo.Binalar b ON b.Id = o.BinaId
                INNER JOIN dbo.TesisOdaTipleri ot ON ot.Id = o.TesisOdaTipiId
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM dbo.RezervasyonSegmentOdaAtamalari rsa
                    WHERE rsa.RezervasyonSegmentId = rs.Id
                );
                """);

            migrationBuilder.AlterColumn<int>(
                name: "TesisId",
                schema: "dbo",
                table: "Rezervasyonlar",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Rezervasyonlar_Tesisler_TesisId",
                schema: "dbo",
                table: "Rezervasyonlar",
                column: "TesisId",
                principalSchema: "dbo",
                principalTable: "Tesisler",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.CreateIndex(
                name: "IX_Rezervasyonlar_TesisId_GirisTarihi_CikisTarihi",
                schema: "dbo",
                table: "Rezervasyonlar",
                columns: new[] { "TesisId", "GirisTarihi", "CikisTarihi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.DropColumn(
                name: "OdaNoSnapshot",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropColumn(
                name: "OdaTipiAdiSnapshot",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropColumn(
                name: "OdaId",
                schema: "dbo",
                table: "Rezervasyonlar");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Rezervasyonlar_Tesisler_TesisId",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropIndex(
                name: "IX_Rezervasyonlar_TesisId_GirisTarihi_CikisTarihi",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropTable(
                name: "RezervasyonSegmentOdaAtamalari",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RezervasyonSegmentleri",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_Rezervasyonlar_RezervasyonDurumu",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.AddColumn<int>(
                name: "OdaId",
                schema: "dbo",
                table: "Rezervasyonlar",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OdaNoSnapshot",
                schema: "dbo",
                table: "Rezervasyonlar",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OdaTipiAdiSnapshot",
                schema: "dbo",
                table: "Rezervasyonlar",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE r
                SET
                    r.OdaId = pick.OdaId,
                    r.OdaNoSnapshot = pick.OdaNoSnapshot,
                    r.OdaTipiAdiSnapshot = pick.OdaTipiAdiSnapshot
                FROM dbo.Rezervasyonlar r
                OUTER APPLY (
                    SELECT TOP (1)
                        o.Id AS OdaId,
                        o.OdaNo AS OdaNoSnapshot,
                        ot.Ad AS OdaTipiAdiSnapshot
                    FROM dbo.Odalar o
                    INNER JOIN dbo.Binalar b ON b.Id = o.BinaId
                    INNER JOIN dbo.TesisOdaTipleri ot ON ot.Id = o.TesisOdaTipiId
                    WHERE b.TesisId = r.TesisId
                    ORDER BY o.Id
                ) pick
                WHERE r.OdaId IS NULL;

                IF EXISTS (SELECT 1 FROM dbo.Rezervasyonlar WHERE OdaId IS NULL)
                BEGIN
                    THROW 50002, 'Rezervasyon OdaId geri donusumu basarisiz oldu.', 1;
                END;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "OdaId",
                schema: "dbo",
                table: "Rezervasyonlar",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "MisafirAdiSoyadi",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropColumn(
                name: "MisafirEposta",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropColumn(
                name: "MisafirTelefon",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropColumn(
                name: "Notlar",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropColumn(
                name: "PasaportNo",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropColumn(
                name: "RezervasyonDurumu",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropColumn(
                name: "TcKimlikNo",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropColumn(
                name: "TesisId",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.CreateIndex(
                name: "IX_Rezervasyonlar_OdaId_GirisTarihi_CikisTarihi",
                schema: "dbo",
                table: "Rezervasyonlar",
                columns: new[] { "OdaId", "GirisTarihi", "CikisTarihi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_Rezervasyonlar_Odalar_OdaId",
                schema: "dbo",
                table: "Rezervasyonlar",
                column: "OdaId",
                principalSchema: "dbo",
                principalTable: "Odalar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
