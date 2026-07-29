using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddPavoUniCloudIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "entegrasyon");

            migrationBuilder.AddColumn<int>(
                name: "PavoOdemeIslemiId",
                schema: "dbo",
                table: "RezervasyonOdemeler",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PavoTerminaller",
                schema: "entegrasyon",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KurumId = table.Column<int>(type: "int", nullable: false),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    KasaBankaHesapId = table.Column<int>(type: "int", nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourceTerminalReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    TargetFingerprint = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PairingId = table.Column<long>(type: "bigint", nullable: true),
                    PairingCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    EslesmeOnayliMi = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_PavoTerminaller", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PavoTerminaller_KasaBankaHesaplari_KasaBankaHesapId",
                        column: x => x.KasaBankaHesapId,
                        principalSchema: "muhasebe",
                        principalTable: "KasaBankaHesaplari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PavoTerminaller_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PavoOdemeIslemleri",
                schema: "entegrasyon",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KurumId = table.Column<int>(type: "int", nullable: false),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    RezervasyonId = table.Column<int>(type: "int", nullable: false),
                    PavoTerminalId = table.Column<int>(type: "int", nullable: false),
                    KasaBankaHesapId = table.Column<int>(type: "int", nullable: false),
                    CariKartId = table.Column<int>(type: "int", nullable: true),
                    RezervasyonOdemeId = table.Column<int>(type: "int", nullable: true),
                    PaymentLinkReference = table.Column<string>(type: "nvarchar(96)", maxLength: 96, nullable: false),
                    PaymentLinkId = table.Column<long>(type: "bigint", nullable: true),
                    Tutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ParaBirimi = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Durum = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    RetrievalReferenceNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    AcquirerReference = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    AuthorizationCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    HataMesaji = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    SonPavoYaniti = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SonSorgulamaTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TamamlanmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_PavoOdemeIslemleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PavoOdemeIslemleri_KasaBankaHesaplari_KasaBankaHesapId",
                        column: x => x.KasaBankaHesapId,
                        principalSchema: "muhasebe",
                        principalTable: "KasaBankaHesaplari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PavoOdemeIslemleri_PavoTerminaller_PavoTerminalId",
                        column: x => x.PavoTerminalId,
                        principalSchema: "entegrasyon",
                        principalTable: "PavoTerminaller",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PavoOdemeIslemleri_RezervasyonOdemeler_RezervasyonOdemeId",
                        column: x => x.RezervasyonOdemeId,
                        principalSchema: "dbo",
                        principalTable: "RezervasyonOdemeler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PavoOdemeIslemleri_Rezervasyonlar_RezervasyonId",
                        column: x => x.RezervasyonId,
                        principalSchema: "dbo",
                        principalTable: "Rezervasyonlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonOdemeler_PavoOdemeIslemiId",
                schema: "dbo",
                table: "RezervasyonOdemeler",
                column: "PavoOdemeIslemiId",
                unique: true,
                filter: "[PavoOdemeIslemiId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PavoOdemeIslemleri_KasaBankaHesapId",
                schema: "entegrasyon",
                table: "PavoOdemeIslemleri",
                column: "KasaBankaHesapId");

            migrationBuilder.CreateIndex(
                name: "IX_PavoOdemeIslemleri_KurumId_PaymentLinkReference",
                schema: "entegrasyon",
                table: "PavoOdemeIslemleri",
                columns: new[] { "KurumId", "PaymentLinkReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PavoOdemeIslemleri_PavoTerminalId",
                schema: "entegrasyon",
                table: "PavoOdemeIslemleri",
                column: "PavoTerminalId");

            migrationBuilder.CreateIndex(
                name: "IX_PavoOdemeIslemleri_PaymentLinkId",
                schema: "entegrasyon",
                table: "PavoOdemeIslemleri",
                column: "PaymentLinkId",
                filter: "[PaymentLinkId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PavoOdemeIslemleri_RezervasyonId",
                schema: "entegrasyon",
                table: "PavoOdemeIslemleri",
                column: "RezervasyonId");

            migrationBuilder.CreateIndex(
                name: "IX_PavoOdemeIslemleri_RezervasyonOdemeId",
                schema: "entegrasyon",
                table: "PavoOdemeIslemleri",
                column: "RezervasyonOdemeId",
                unique: true,
                filter: "[RezervasyonOdemeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PavoOdemeIslemleri_TesisId_Durum",
                schema: "entegrasyon",
                table: "PavoOdemeIslemleri",
                columns: new[] { "TesisId", "Durum" });

            migrationBuilder.CreateIndex(
                name: "IX_PavoTerminaller_KasaBankaHesapId",
                schema: "entegrasyon",
                table: "PavoTerminaller",
                column: "KasaBankaHesapId");

            migrationBuilder.CreateIndex(
                name: "IX_PavoTerminaller_KurumId_SerialNumber",
                schema: "entegrasyon",
                table: "PavoTerminaller",
                columns: new[] { "KurumId", "SerialNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PavoTerminaller_TesisId_KasaBankaHesapId_AktifMi",
                schema: "entegrasyon",
                table: "PavoTerminaller",
                columns: new[] { "TesisId", "KasaBankaHesapId", "AktifMi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_RezervasyonOdemeler_PavoOdemeIslemleri_PavoOdemeIslemiId",
                schema: "dbo",
                table: "RezervasyonOdemeler",
                column: "PavoOdemeIslemiId",
                principalSchema: "entegrasyon",
                principalTable: "PavoOdemeIslemleri",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RezervasyonOdemeler_PavoOdemeIslemleri_PavoOdemeIslemiId",
                schema: "dbo",
                table: "RezervasyonOdemeler");

            migrationBuilder.DropTable(
                name: "PavoOdemeIslemleri",
                schema: "entegrasyon");

            migrationBuilder.DropTable(
                name: "PavoTerminaller",
                schema: "entegrasyon");

            migrationBuilder.DropIndex(
                name: "IX_RezervasyonOdemeler_PavoOdemeIslemiId",
                schema: "dbo",
                table: "RezervasyonOdemeler");

            migrationBuilder.DropColumn(
                name: "PavoOdemeIslemiId",
                schema: "dbo",
                table: "RezervasyonOdemeler");
        }
    }
}
