using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class C1AgentPavoDeviceRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PosCihazlari_KurumId_SeriNo",
                schema: "entegrasyon",
                table: "PosCihazlari");

            migrationBuilder.AddColumn<string>(
                name: "AgentLocalDeviceId",
                schema: "entegrasyon",
                table: "PosCihazlari",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PosCihazlari_AgentLocalDeviceId",
                schema: "entegrasyon",
                table: "PosCihazlari",
                column: "AgentLocalDeviceId",
                filter: "[AgentLocalDeviceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PosCihazlari_KurumId_Saglayici_SeriNo",
                schema: "entegrasyon",
                table: "PosCihazlari",
                columns: new[] { "KurumId", "Saglayici", "SeriNo" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PosCihazlari_AgentLocalDeviceId",
                schema: "entegrasyon",
                table: "PosCihazlari");

            migrationBuilder.DropIndex(
                name: "IX_PosCihazlari_KurumId_Saglayici_SeriNo",
                schema: "entegrasyon",
                table: "PosCihazlari");

            migrationBuilder.DropColumn(
                name: "AgentLocalDeviceId",
                schema: "entegrasyon",
                table: "PosCihazlari");

            migrationBuilder.CreateIndex(
                name: "IX_PosCihazlari_KurumId_SeriNo",
                schema: "entegrasyon",
                table: "PosCihazlari",
                columns: new[] { "KurumId", "SeriNo" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
