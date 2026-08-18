using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class PosGunSonu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PosGunSonuIslemleri",
                schema: "entegrasyon",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KurumId = table.Column<int>(type: "int", nullable: false),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    PosCihaziId = table.Column<int>(type: "int", nullable: false),
                    AgentCommandId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UseSummary = table.Column<bool>(type: "bit", nullable: false),
                    Print = table.Column<bool>(type: "bit", nullable: false),
                    Durum = table.Column<int>(type: "int", nullable: false),
                    GunSonuMesaji = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    BatchNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    EodDateTime = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PavoErrorCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PavoMessage = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    EodDataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BaslatilmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TamamlanmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequestedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
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
                    table.PrimaryKey("PK_PosGunSonuIslemleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PosGunSonuIslemleri_PosCihazlari_PosCihaziId",
                        column: x => x.PosCihaziId,
                        principalSchema: "entegrasyon",
                        principalTable: "PosCihazlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PosGunSonuSlipleri",
                schema: "entegrasyon",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KurumId = table.Column<int>(type: "int", nullable: false),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    PosGunSonuIslemiId = table.Column<int>(type: "int", nullable: false),
                    PosCihaziId = table.Column<int>(type: "int", nullable: false),
                    SlipTipi = table.Column<int>(type: "int", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, defaultValue: "image/png"),
                    StoragePath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DosyaBoyutu = table.Column<long>(type: "bigint", nullable: false),
                    OlusturulmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
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
                    table.PrimaryKey("PK_PosGunSonuSlipleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PosGunSonuSlipleri_PosGunSonuIslemleri_PosGunSonuIslemiId",
                        column: x => x.PosGunSonuIslemiId,
                        principalSchema: "entegrasyon",
                        principalTable: "PosGunSonuIslemleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PosGunSonuIslemleri_AgentCommandId",
                schema: "entegrasyon",
                table: "PosGunSonuIslemleri",
                column: "AgentCommandId");

            migrationBuilder.CreateIndex(
                name: "IX_PosGunSonuIslemleri_BaslatilmaTarihi",
                schema: "entegrasyon",
                table: "PosGunSonuIslemleri",
                column: "BaslatilmaTarihi");

            migrationBuilder.CreateIndex(
                name: "IX_PosGunSonuIslemleri_KurumId",
                schema: "entegrasyon",
                table: "PosGunSonuIslemleri",
                column: "KurumId");

            migrationBuilder.CreateIndex(
                name: "IX_PosGunSonuIslemleri_PosCihaziId",
                schema: "entegrasyon",
                table: "PosGunSonuIslemleri",
                column: "PosCihaziId");

            migrationBuilder.CreateIndex(
                name: "IX_PosGunSonuIslemleri_TesisId",
                schema: "entegrasyon",
                table: "PosGunSonuIslemleri",
                column: "TesisId");

            migrationBuilder.CreateIndex(
                name: "IX_PosGunSonuSlipleri_KurumId",
                schema: "entegrasyon",
                table: "PosGunSonuSlipleri",
                column: "KurumId");

            migrationBuilder.CreateIndex(
                name: "IX_PosGunSonuSlipleri_PosCihaziId",
                schema: "entegrasyon",
                table: "PosGunSonuSlipleri",
                column: "PosCihaziId");

            migrationBuilder.CreateIndex(
                name: "IX_PosGunSonuSlipleri_PosGunSonuIslemiId",
                schema: "entegrasyon",
                table: "PosGunSonuSlipleri",
                column: "PosGunSonuIslemiId");

            migrationBuilder.CreateIndex(
                name: "IX_PosGunSonuSlipleri_PosGunSonuIslemiId_SlipTipi_Sha256",
                schema: "entegrasyon",
                table: "PosGunSonuSlipleri",
                columns: new[] { "PosGunSonuIslemiId", "SlipTipi", "Sha256" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PosGunSonuSlipleri_Sha256",
                schema: "entegrasyon",
                table: "PosGunSonuSlipleri",
                column: "Sha256");

            migrationBuilder.CreateIndex(
                name: "IX_PosGunSonuSlipleri_TesisId",
                schema: "entegrasyon",
                table: "PosGunSonuSlipleri",
                column: "TesisId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PosGunSonuSlipleri",
                schema: "entegrasyon");

            migrationBuilder.DropTable(
                name: "PosGunSonuIslemleri",
                schema: "entegrasyon");
        }
    }
}
