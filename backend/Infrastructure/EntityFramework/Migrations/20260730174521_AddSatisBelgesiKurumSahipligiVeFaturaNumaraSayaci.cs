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
            migrationBuilder.AddColumn<int>(
                name: "KurumId",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // ── Legacy veri backfill: KurumId'yi bağlı TesisId üzerinden doldur ──
            // Otoriter zincir: SatisBelgesi.TesisId -> Tesis.KurumId -> SatisBelgesi.KurumId
            // (bkz. SatisBelgesiService.ResolveKurumIdFromTesisAsync ile aynı mantık).
            migrationBuilder.Sql(@"
UPDATE sb
SET sb.KurumId = t.KurumId
FROM [muhasebe].[SatisBelgeleri] sb
INNER JOIN [dbo].[Tesisler] t ON t.Id = sb.TesisId
WHERE sb.TesisId IS NOT NULL;
");

            // TesisId'si bulunmayan veya geçerli bir tesise bağlanamayan (silinmiş/olmayan
            // TesisId) legacy belgeler için: sistemdeki ilk (Id'si en küçük) aktif Kurum'a
            // varsayılan olarak atanır. Sistemde HİÇ Kurum yoksa (ve hâlâ KurumId=0 olan satır
            // varsa) migration RAISERROR ile açıkça durur - rastgele/güvensiz bir değer ASLA
            // sessizce atanmaz.
            migrationBuilder.Sql(@"
DECLARE @varsayilanKurumId INT;
SELECT TOP 1 @varsayilanKurumId = Id FROM [dbo].[Kurumlar] WHERE IsDeleted = 0 ORDER BY Id;

IF @varsayilanKurumId IS NOT NULL
BEGIN
    UPDATE [muhasebe].[SatisBelgeleri]
    SET KurumId = @varsayilanKurumId
    WHERE KurumId = 0;
END

IF EXISTS (SELECT 1 FROM [muhasebe].[SatisBelgeleri] WHERE KurumId = 0)
BEGIN
    RAISERROR('SatisBelgeleri.KurumId backfill basarisiz: TesisId uzerinden veya varsayilan kurum ile eslenemeyen (sistemde hic Kurum bulunamadigi icin) kayitlar var. Migration durduruldu; lutfen once en az bir Kurum kaydi olusturun veya bu belgeleri elle inceleyip duzeltin.', 16, 1);
END
");

            // ── Kurum içi mükerrer ResmiFaturaNo kontrolü (yeni unique index'ten ÖNCE) ──
            // Mevcut kod tabanında ResmiFaturaNo'yu atayan hiçbir servis akışı yoktu, bu yüzden
            // pratikte bu tabloda satır bulunması BEKLENMEZ; yine de veri sessizce yeniden
            // numaralandırılmaz - mükerrer varsa migration açık bir hata ile durur.
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
