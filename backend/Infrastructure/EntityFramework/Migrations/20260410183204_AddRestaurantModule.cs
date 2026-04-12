using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Restoranlar",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
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
                    table.PrimaryKey("PK_Restoranlar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Restoranlar_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RestoranMasalari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RestoranId = table.Column<int>(type: "int", nullable: false),
                    MasaNo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Kapasite = table.Column<int>(type: "int", nullable: false),
                    Durum = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
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
                    table.PrimaryKey("PK_RestoranMasalari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RestoranMasalari_Restoranlar_RestoranId",
                        column: x => x.RestoranId,
                        principalSchema: "dbo",
                        principalTable: "Restoranlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RestoranMenuKategorileri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RestoranId = table.Column<int>(type: "int", nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SiraNo = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_RestoranMenuKategorileri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RestoranMenuKategorileri_Restoranlar_RestoranId",
                        column: x => x.RestoranId,
                        principalSchema: "dbo",
                        principalTable: "Restoranlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RestoranSiparisleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RestoranId = table.Column<int>(type: "int", nullable: false),
                    RestoranMasaId = table.Column<int>(type: "int", nullable: true),
                    SiparisNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SiparisDurumu = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ToplamTutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OdenenTutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    KalanTutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ParaBirimi = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    OdemeDurumu = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Notlar = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    SiparisTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_RestoranSiparisleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RestoranSiparisleri_RestoranMasalari_RestoranMasaId",
                        column: x => x.RestoranMasaId,
                        principalSchema: "dbo",
                        principalTable: "RestoranMasalari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestoranSiparisleri_Restoranlar_RestoranId",
                        column: x => x.RestoranId,
                        principalSchema: "dbo",
                        principalTable: "Restoranlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RestoranMenuUrunleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RestoranMenuKategoriId = table.Column<int>(type: "int", nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Fiyat = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ParaBirimi = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    HazirlamaSuresiDakika = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_RestoranMenuUrunleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RestoranMenuUrunleri_RestoranMenuKategorileri_RestoranMenuKategoriId",
                        column: x => x.RestoranMenuKategoriId,
                        principalSchema: "dbo",
                        principalTable: "RestoranMenuKategorileri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RestoranOdemeleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RestoranSiparisId = table.Column<int>(type: "int", nullable: false),
                    OdemeTipi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Tutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ParaBirimi = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    OdemeTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    RezervasyonId = table.Column<int>(type: "int", nullable: true),
                    RezervasyonOdemeId = table.Column<int>(type: "int", nullable: true),
                    Durum = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IslemReferansNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
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
                    table.PrimaryKey("PK_RestoranOdemeleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RestoranOdemeleri_RestoranSiparisleri_RestoranSiparisId",
                        column: x => x.RestoranSiparisId,
                        principalSchema: "dbo",
                        principalTable: "RestoranSiparisleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestoranOdemeleri_RezervasyonOdemeler_RezervasyonOdemeId",
                        column: x => x.RezervasyonOdemeId,
                        principalSchema: "dbo",
                        principalTable: "RezervasyonOdemeler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestoranOdemeleri_Rezervasyonlar_RezervasyonId",
                        column: x => x.RezervasyonId,
                        principalSchema: "dbo",
                        principalTable: "Rezervasyonlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RestoranSiparisKalemleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RestoranSiparisId = table.Column<int>(type: "int", nullable: false),
                    RestoranMenuUrunId = table.Column<int>(type: "int", nullable: false),
                    UrunAdiSnapshot = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    BirimFiyat = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Miktar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SatirToplam = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Notlar = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
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
                    table.PrimaryKey("PK_RestoranSiparisKalemleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RestoranSiparisKalemleri_RestoranMenuUrunleri_RestoranMenuUrunId",
                        column: x => x.RestoranMenuUrunId,
                        principalSchema: "dbo",
                        principalTable: "RestoranMenuUrunleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestoranSiparisKalemleri_RestoranSiparisleri_RestoranSiparisId",
                        column: x => x.RestoranSiparisId,
                        principalSchema: "dbo",
                        principalTable: "RestoranSiparisleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Restoranlar_TesisId_Ad",
                schema: "dbo",
                table: "Restoranlar",
                columns: new[] { "TesisId", "Ad" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_RestoranMasalari_RestoranId_MasaNo",
                schema: "dbo",
                table: "RestoranMasalari",
                columns: new[] { "RestoranId", "MasaNo" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_RestoranMenuKategorileri_RestoranId_Ad",
                schema: "dbo",
                table: "RestoranMenuKategorileri",
                columns: new[] { "RestoranId", "Ad" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_RestoranMenuKategorileri_RestoranId_SiraNo",
                schema: "dbo",
                table: "RestoranMenuKategorileri",
                columns: new[] { "RestoranId", "SiraNo" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RestoranMenuUrunleri_RestoranMenuKategoriId_Ad",
                schema: "dbo",
                table: "RestoranMenuUrunleri",
                columns: new[] { "RestoranMenuKategoriId", "Ad" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RestoranOdemeleri_IslemReferansNo",
                schema: "dbo",
                table: "RestoranOdemeleri",
                column: "IslemReferansNo",
                filter: "[IsDeleted] = 0 AND [IslemReferansNo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RestoranOdemeleri_RestoranSiparisId_OdemeTarihi",
                schema: "dbo",
                table: "RestoranOdemeleri",
                columns: new[] { "RestoranSiparisId", "OdemeTarihi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RestoranOdemeleri_RestoranSiparisId_RezervasyonId_OdemeTipi",
                schema: "dbo",
                table: "RestoranOdemeleri",
                columns: new[] { "RestoranSiparisId", "RezervasyonId", "OdemeTipi" },
                filter: "[IsDeleted] = 0 AND [RezervasyonId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RestoranOdemeleri_RezervasyonId",
                schema: "dbo",
                table: "RestoranOdemeleri",
                column: "RezervasyonId");

            migrationBuilder.CreateIndex(
                name: "IX_RestoranOdemeleri_RezervasyonOdemeId",
                schema: "dbo",
                table: "RestoranOdemeleri",
                column: "RezervasyonOdemeId");

            migrationBuilder.CreateIndex(
                name: "IX_RestoranSiparisKalemleri_RestoranMenuUrunId",
                schema: "dbo",
                table: "RestoranSiparisKalemleri",
                column: "RestoranMenuUrunId");

            migrationBuilder.CreateIndex(
                name: "IX_RestoranSiparisKalemleri_RestoranSiparisId",
                schema: "dbo",
                table: "RestoranSiparisKalemleri",
                column: "RestoranSiparisId",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RestoranSiparisleri_RestoranId_SiparisTarihi",
                schema: "dbo",
                table: "RestoranSiparisleri",
                columns: new[] { "RestoranId", "SiparisTarihi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RestoranSiparisleri_RestoranMasaId_SiparisDurumu",
                schema: "dbo",
                table: "RestoranSiparisleri",
                columns: new[] { "RestoranMasaId", "SiparisDurumu" },
                filter: "[IsDeleted] = 0 AND [RestoranMasaId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RestoranSiparisleri_SiparisNo",
                schema: "dbo",
                table: "RestoranSiparisleri",
                column: "SiparisNo",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RestoranOdemeleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RestoranSiparisKalemleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RestoranMenuUrunleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RestoranSiparisleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RestoranMenuKategorileri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RestoranMasalari",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Restoranlar",
                schema: "dbo");
        }
    }
}
