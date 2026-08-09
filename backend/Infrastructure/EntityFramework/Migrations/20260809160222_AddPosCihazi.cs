using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddPosCihazi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PosCihaziId",
                schema: "entegrasyon",
                table: "PosTerminaller",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PosCihazlari",
                schema: "entegrasyon",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KurumId = table.Column<int>(type: "int", nullable: false),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    AgentId = table.Column<int>(type: "int", nullable: true),
                    Saglayici = table.Column<int>(type: "int", nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SeriNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IpAdresi = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    HttpPort = table.Column<int>(type: "int", nullable: true),
                    HttpsPort = table.Column<int>(type: "int", nullable: true),
                    Fingerprint = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    TargetFingerprint = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PairingCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    PairingId = table.Column<long>(type: "bigint", nullable: true),
                    EslesmeOnayliMi = table.Column<bool>(type: "bit", nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    SonBaglantiTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Aciklama = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
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
                    table.PrimaryKey("PK_PosCihazlari", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PosTerminaller_PosCihaziId",
                schema: "entegrasyon",
                table: "PosTerminaller",
                column: "PosCihaziId");

            migrationBuilder.CreateIndex(
                name: "IX_PosCihazlari_AgentId",
                schema: "entegrasyon",
                table: "PosCihazlari",
                column: "AgentId",
                filter: "[AgentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PosCihazlari_KurumId_SeriNo",
                schema: "entegrasyon",
                table: "PosCihazlari",
                columns: new[] { "KurumId", "SeriNo" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_PosTerminaller_PosCihazlari_PosCihaziId",
                schema: "entegrasyon",
                table: "PosTerminaller",
                column: "PosCihaziId",
                principalSchema: "entegrasyon",
                principalTable: "PosCihazlari",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PosTerminaller_PosCihazlari_PosCihaziId",
                schema: "entegrasyon",
                table: "PosTerminaller");

            migrationBuilder.DropTable(
                name: "PosCihazlari",
                schema: "entegrasyon");

            migrationBuilder.DropIndex(
                name: "IX_PosTerminaller_PosCihaziId",
                schema: "entegrasyon",
                table: "PosTerminaller");

            migrationBuilder.DropColumn(
                name: "PosCihaziId",
                schema: "entegrasyon",
                table: "PosTerminaller");
        }
    }
}
