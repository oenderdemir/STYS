using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddSatisBelgesiKurumSahipligiVeFaturaNumaraSayaci : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // KurumId ÖNCE NULLABLE eklenir (kalıcı bir default DEĞER BIRAKILMAZ) - böylece
            // backfill sırasında geçerli bir Tesis'e bağlanamayan legacy kayıtlar açıkça NULL
            // kalabilir; kolonu NOT NULL yapmadan ÖNCE bu NULL'lar tespit edilip migration
            // durdurulabilir. Hiçbir kayda varsayılan/ilk/rastgele bir Kurum ATANMAZ.
            migrationBuilder.AddColumn<int>(
                name: "KurumId",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                type: "int",
                nullable: true);

            // ── Legacy veri backfill: KurumId'yi YALNIZCA gerçekten bağlı ve geçerli bir Tesis
            // kaydı üzerinden doldur ── Otoriter zincir: SatisBelgesi.TesisId -> Tesis.KurumId
            // (bkz. SatisBelgesiService.ResolveKurumIdFromTesisAsync ile aynı mantık).
            migrationBuilder.Sql(@"
UPDATE sb
SET sb.KurumId = t.KurumId
FROM [muhasebe].[SatisBelgeleri] sb
INNER JOIN [dbo].[Tesisler] t ON t.Id = sb.TesisId
WHERE sb.TesisId IS NOT NULL;
");

            // TesisId'si NULL olan veya geçerli bir Tesis'e bağlanamayan (silinmiş/olmayan
            // TesisId) legacy belgeler KurumId=NULL olarak kalır. Böyle bir kayıt varsa migration
            // AÇIK bir hata ile durur - hata mesajı eşleşmeyen kayıt SAYISINI belirtir. Hiçbir
            // varsayılan/ilk/rastgele Kuruma ATAMA yapılmaz; bu davranış yanlış tenant sahipliği
            // ve kurumlar arası veri sızıntısı riski taşıdığı için kasıtlı olarak KALDIRILDI.
            migrationBuilder.Sql(@"
DECLARE @eslesmeyenSayisi INT;
SELECT @eslesmeyenSayisi = COUNT(*) FROM [muhasebe].[SatisBelgeleri] WHERE KurumId IS NULL;

IF @eslesmeyenSayisi > 0
BEGIN
    DECLARE @hataMesaji NVARCHAR(4000) = N'SatisBelgeleri.KurumId backfill basarisiz: ' +
        CAST(@eslesmeyenSayisi AS NVARCHAR(20)) +
        N' kayit gecerli bir Tesis uzerinden bir Kuruma baglanamadi (TesisId NULL veya Tesisler tablosunda bulunamiyor). ' +
        N'Bu kayitlara varsayilan/ilk/rastgele bir Kurum ATANMAZ - yanlis tenant sahipligi ve kurumlar arasi veri sizintisi olusturabilir. ' +
        N'Migration durduruldu; lutfen bu kayitlari elle inceleyip dogru TesisId/KurumId ile eslestirin (veya gecersizse soft-delete edin), sonra migration''i tekrar calistirin.';
    RAISERROR(@hataMesaji, 16, 1);
END
");

            // Tüm satırlar başarıyla dolduruldu (yukarıdaki kontrolü geçti) - kolon artık NOT
            // NULL yapılabilir. Kalıcı bir default değer (0 vb.) YOKTUR.
            migrationBuilder.AlterColumn<int>(
                name: "KurumId",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            // ── Kurum içi mükerrer ResmiFaturaNo kontrolü (yeni unique index'ten ÖNCE) ──
            // Mevcut kod tabanında ResmiFaturaNo'yu atayan hiçbir servis akışı bu migration'dan
            // önce yoktu, bu yüzden pratikte bu tabloda satır bulunması BEKLENMEZ; yine de veri
            // sessizce yeniden numaralandırılmaz - mükerrer varsa migration açık bir hata ile durur.
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT KurumId, ResmiFaturaNo
    FROM [muhasebe].[SatisBelgeleri]
    WHERE IsDeleted = 0 AND ResmiFaturaNo IS NOT NULL
    GROUP BY KurumId, ResmiFaturaNo
    HAVING COUNT(*) > 1
)
BEGIN
    RAISERROR('SatisBelgeleri icinde ayni Kurum + ResmiFaturaNo kombinasyonuna sahip mukerrer kayitlar tespit edildi. Yeni unique index olusturulmadan once bu kayitlar elle incelenmeli. Migration durduruldu.', 16, 1);
END
");

            migrationBuilder.CreateTable(
                name: "KurumFaturaNumaraSayaclari",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KurumId = table.Column<int>(type: "int", nullable: false),
                    MaliYil = table.Column<int>(type: "int", nullable: false),
                    SeriKodu = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    SonNumara = table.Column<int>(type: "int", nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_KurumFaturaNumaraSayaclari", x => x.Id);
                    table.CheckConstraint("CK_KurumFaturaNumaraSayaclari_SeriKodu", "LEN([SeriKodu]) = 3 AND [SeriKodu] COLLATE Latin1_General_BIN2 NOT LIKE '%[^A-Z0-9]%'");
                    table.CheckConstraint("CK_KurumFaturaNumaraSayaclari_SonNumara", "[SonNumara] >= 0 AND [SonNumara] <= 999999999");
                    table.ForeignKey(
                        name: "FK_KurumFaturaNumaraSayaclari_Kurumlar_KurumId",
                        column: x => x.KurumId,
                        principalSchema: "dbo",
                        principalTable: "Kurumlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SatisBelgeleri_KurumId",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                column: "KurumId");

            migrationBuilder.CreateIndex(
                name: "IX_SatisBelgeleri_KurumId_ResmiFaturaNo",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                columns: new[] { "KurumId", "ResmiFaturaNo" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [ResmiFaturaNo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_KurumFaturaNumaraSayaclari_KurumId_MaliYil_SeriKodu",
                schema: "muhasebe",
                table: "KurumFaturaNumaraSayaclari",
                columns: new[] { "KurumId", "MaliYil", "SeriKodu" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_SatisBelgeleri_Kurumlar_KurumId",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                column: "KurumId",
                principalSchema: "dbo",
                principalTable: "Kurumlar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SatisBelgeleri_Kurumlar_KurumId",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.DropTable(
                name: "KurumFaturaNumaraSayaclari",
                schema: "muhasebe");

            migrationBuilder.DropIndex(
                name: "IX_SatisBelgeleri_KurumId",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.DropIndex(
                name: "IX_SatisBelgeleri_KurumId_ResmiFaturaNo",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.DropColumn(
                name: "KurumId",
                schema: "muhasebe",
                table: "SatisBelgeleri");
        }
    }
}
