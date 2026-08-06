using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddEBelgeKurumPolitikasiFaz2B10 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KurumEBelgePolitikalari",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KurumId = table.Column<int>(type: "int", nullable: false),
                    EntegrasyonYontemi = table.Column<int>(type: "int", nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    AktivasyonYerelTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HariciSistemKodu = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PolitikaSurumu = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
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
                    table.PrimaryKey("PK_KurumEBelgePolitikalari", x => x.Id);
                    table.UniqueConstraint("AK_KurumEBelgePolitikalari_Id_KurumId", x => new { x.Id, x.KurumId });
                    table.CheckConstraint("CK_KurumEBelgePolitikalari_EntegrasyonYontemi", "[EntegrasyonYontemi] IN (0, 1, 2, 3, 4, 5)");
                    table.CheckConstraint("CK_KurumEBelgePolitikalari_PolitikaSurumu", "[PolitikaSurumu] >= 1");
                    table.ForeignKey(
                        name: "FK_KurumEBelgePolitikalari_Kurumlar_KurumId",
                        column: x => x.KurumId,
                        principalSchema: "dbo",
                        principalTable: "Kurumlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KurumEBelgePolitikaRevizyonlari",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KurumId = table.Column<int>(type: "int", nullable: false),
                    KurumEBelgePolitikasiId = table.Column<int>(type: "int", nullable: false),
                    EskiSurum = table.Column<int>(type: "int", nullable: false),
                    YeniSurum = table.Column<int>(type: "int", nullable: false),
                    EskiYontem = table.Column<int>(type: "int", nullable: false),
                    YeniYontem = table.Column<int>(type: "int", nullable: false),
                    EskiAktifMi = table.Column<bool>(type: "bit", nullable: false),
                    YeniAktifMi = table.Column<bool>(type: "bit", nullable: false),
                    DegisiklikZamaniUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DegisiklikNedeni = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
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
                    table.PrimaryKey("PK_KurumEBelgePolitikaRevizyonlari", x => x.Id);
                    table.CheckConstraint("CK_KurumEBelgePolitikaRevizyonlari_EskiYontem", "[EskiYontem] IN (0, 1, 2, 3, 4, 5)");
                    table.CheckConstraint("CK_KurumEBelgePolitikaRevizyonlari_Surumler", "[EskiSurum] >= 0 AND [YeniSurum] >= 1");
                    table.CheckConstraint("CK_KurumEBelgePolitikaRevizyonlari_YeniYontem", "[YeniYontem] IN (0, 1, 2, 3, 4, 5)");
                    table.ForeignKey(
                        name: "FK_KurumEBelgePolitikaRevizyonlari_KurumEBelgePolitikalari_KurumEBelgePolitikasiId_KurumId",
                        columns: x => new { x.KurumEBelgePolitikasiId, x.KurumId },
                        principalSchema: "muhasebe",
                        principalTable: "KurumEBelgePolitikalari",
                        principalColumns: new[] { "Id", "KurumId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SatisBelgesiEBelgeKararlari",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KurumId = table.Column<int>(type: "int", nullable: false),
                    SatisBelgesiId = table.Column<int>(type: "int", nullable: false),
                    KurumEBelgePolitikasiId = table.Column<int>(type: "int", nullable: true),
                    PolitikaSurumu = table.Column<int>(type: "int", nullable: true),
                    EntegrasyonYontemi = table.Column<int>(type: "int", nullable: false),
                    KararNedeni = table.Column<int>(type: "int", nullable: false),
                    YerelSnapshotOlustur = table.Column<bool>(type: "bit", nullable: false),
                    YerelUnsignedUblOlustur = table.Column<bool>(type: "bit", nullable: false),
                    YerelImzaOlustur = table.Column<bool>(type: "bit", nullable: false),
                    OtomatikGonderimYap = table.Column<bool>(type: "bit", nullable: false),
                    KararZamaniUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EBelgeKaydiId = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_SatisBelgesiEBelgeKararlari", x => x.Id);
                    table.UniqueConstraint("AK_SatisBelgesiEBelgeKararlari_Id_KurumId", x => new { x.Id, x.KurumId });
                    table.CheckConstraint("CK_SatisBelgesiEBelgeKararlari_EntegrasyonYontemi", "[EntegrasyonYontemi] IN (0, 1, 2, 3, 4, 5)");
                    table.CheckConstraint("CK_SatisBelgesiEBelgeKararlari_KararNedeni", "[KararNedeni] IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10)");
                    table.CheckConstraint("CK_SatisBelgesiEBelgeKararlari_PolitikaSurumu", "[PolitikaSurumu] IS NULL OR [PolitikaSurumu] >= 1");
                    table.ForeignKey(
                        name: "FK_SatisBelgesiEBelgeKararlari_EBelgeKayitlari_EBelgeKaydiId_KurumId",
                        columns: x => new { x.EBelgeKaydiId, x.KurumId },
                        principalSchema: "muhasebe",
                        principalTable: "EBelgeKayitlari",
                        principalColumns: new[] { "Id", "KurumId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SatisBelgesiEBelgeKararlari_KurumEBelgePolitikalari_KurumEBelgePolitikasiId_KurumId",
                        columns: x => new { x.KurumEBelgePolitikasiId, x.KurumId },
                        principalSchema: "muhasebe",
                        principalTable: "KurumEBelgePolitikalari",
                        principalColumns: new[] { "Id", "KurumId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SatisBelgesiEBelgeKararlari_SatisBelgeleri_SatisBelgesiId_KurumId",
                        columns: x => new { x.SatisBelgesiId, x.KurumId },
                        principalSchema: "muhasebe",
                        principalTable: "SatisBelgeleri",
                        principalColumns: new[] { "Id", "KurumId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KurumEBelgePolitikalari_KurumId",
                schema: "muhasebe",
                table: "KurumEBelgePolitikalari",
                column: "KurumId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KurumEBelgePolitikaRevizyonlari_KurumEBelgePolitikasiId_KurumId",
                schema: "muhasebe",
                table: "KurumEBelgePolitikaRevizyonlari",
                columns: new[] { "KurumEBelgePolitikasiId", "KurumId" });

            migrationBuilder.CreateIndex(
                name: "IX_KurumEBelgePolitikaRevizyonlari_KurumEBelgePolitikasiId_YeniSurum",
                schema: "muhasebe",
                table: "KurumEBelgePolitikaRevizyonlari",
                columns: new[] { "KurumEBelgePolitikasiId", "YeniSurum" });

            migrationBuilder.CreateIndex(
                name: "IX_SatisBelgesiEBelgeKararlari_EBelgeKaydiId_KurumId",
                schema: "muhasebe",
                table: "SatisBelgesiEBelgeKararlari",
                columns: new[] { "EBelgeKaydiId", "KurumId" });

            migrationBuilder.CreateIndex(
                name: "IX_SatisBelgesiEBelgeKararlari_KurumEBelgePolitikasiId_KurumId",
                schema: "muhasebe",
                table: "SatisBelgesiEBelgeKararlari",
                columns: new[] { "KurumEBelgePolitikasiId", "KurumId" });

            migrationBuilder.CreateIndex(
                name: "IX_SatisBelgesiEBelgeKararlari_KurumId_SatisBelgesiId",
                schema: "muhasebe",
                table: "SatisBelgesiEBelgeKararlari",
                columns: new[] { "KurumId", "SatisBelgesiId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SatisBelgesiEBelgeKararlari_SatisBelgesiId_KurumId",
                schema: "muhasebe",
                table: "SatisBelgesiEBelgeKararlari",
                columns: new[] { "SatisBelgesiId", "KurumId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KurumEBelgePolitikaRevizyonlari",
                schema: "muhasebe");

            migrationBuilder.DropTable(
                name: "SatisBelgesiEBelgeKararlari",
                schema: "muhasebe");

            migrationBuilder.DropTable(
                name: "KurumEBelgePolitikalari",
                schema: "muhasebe");
        }
    }
}
