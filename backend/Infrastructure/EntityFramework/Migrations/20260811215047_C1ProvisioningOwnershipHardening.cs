using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class C1ProvisioningOwnershipHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PosTerminaller_KurumId_SaglayiciKodu_SerialNumber",
                schema: "entegrasyon",
                table: "PosTerminaller");

            migrationBuilder.DropIndex(
                name: "IX_PosCihazlari_KurumId_Saglayici_SeriNo",
                schema: "entegrasyon",
                table: "PosCihazlari");

            migrationBuilder.AddColumn<string>(
                name: "CanonicalAcquirerId",
                schema: "entegrasyon",
                table: "PosTerminaller",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CanonicalTerminalId",
                schema: "entegrasyon",
                table: "PosTerminaller",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE [entegrasyon].[PosTerminaller]
                SET [CanonicalAcquirerId] = UPPER(LTRIM(RTRIM(ISNULL([AcquirerId], '')))),
                    [CanonicalTerminalId] = UPPER(LTRIM(RTRIM([SerialNumber])))
                WHERE [IsDeleted] = 0
                """);

            migrationBuilder.CreateIndex(
                name: "IX_PosTerminaller_PosCihaziId_CanonicalAcquirerId_CanonicalTerminalId",
                schema: "entegrasyon",
                table: "PosTerminaller",
                columns: new[] { "PosCihaziId", "CanonicalAcquirerId", "CanonicalTerminalId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PosCihazlari_Saglayici_SeriNo",
                schema: "entegrasyon",
                table: "PosCihazlari",
                columns: new[] { "Saglayici", "SeriNo" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PosTerminaller_PosCihaziId_CanonicalAcquirerId_CanonicalTerminalId",
                schema: "entegrasyon",
                table: "PosTerminaller");

            migrationBuilder.DropIndex(
                name: "IX_PosCihazlari_Saglayici_SeriNo",
                schema: "entegrasyon",
                table: "PosCihazlari");

            migrationBuilder.DropColumn(
                name: "CanonicalAcquirerId",
                schema: "entegrasyon",
                table: "PosTerminaller");

            migrationBuilder.DropColumn(
                name: "CanonicalTerminalId",
                schema: "entegrasyon",
                table: "PosTerminaller");

            migrationBuilder.CreateIndex(
                name: "IX_PosTerminaller_KurumId_SaglayiciKodu_SerialNumber",
                schema: "entegrasyon",
                table: "PosTerminaller",
                columns: new[] { "KurumId", "SaglayiciKodu", "SerialNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PosCihazlari_KurumId_Saglayici_SeriNo",
                schema: "entegrasyon",
                table: "PosCihazlari",
                columns: new[] { "KurumId", "Saglayici", "SeriNo" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
