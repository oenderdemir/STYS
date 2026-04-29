using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class EnforceMuhasebeHesapPlaniLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Depolar_Kod",
                schema: "muhasebe",
                table: "Depolar");

            migrationBuilder.AddColumn<string>(
                name: "AnaMuhasebeHesapKodu",
                schema: "muhasebe",
                table: "TasinirKartlar",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MuhasebeHesapPlaniId",
                schema: "muhasebe",
                table: "TasinirKartlar",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MuhasebeHesapSiraNo",
                schema: "muhasebe",
                table: "TasinirKartlar",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnaMuhasebeHesapKodu",
                schema: "muhasebe",
                table: "Depolar",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MuhasebeHesapSiraNo",
                schema: "muhasebe",
                table: "Depolar",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TasinirKartlar_MuhasebeHesapPlaniId",
                schema: "muhasebe",
                table: "TasinirKartlar",
                column: "MuhasebeHesapPlaniId",
                filter: "[IsDeleted] = 0 AND [MuhasebeHesapPlaniId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TasinirKartlar_TesisId_AnaMuhasebeHesapKodu",
                schema: "muhasebe",
                table: "TasinirKartlar",
                columns: new[] { "TesisId", "AnaMuhasebeHesapKodu" },
                filter: "[IsDeleted] = 0 AND [TesisId] IS NOT NULL AND [AnaMuhasebeHesapKodu] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Depolar_TesisId_AnaMuhasebeHesapKodu",
                schema: "muhasebe",
                table: "Depolar",
                columns: new[] { "TesisId", "AnaMuhasebeHesapKodu" },
                filter: "[IsDeleted] = 0 AND [TesisId] IS NOT NULL AND [AnaMuhasebeHesapKodu] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Depolar_TesisId_Kod",
                schema: "muhasebe",
                table: "Depolar",
                columns: new[] { "TesisId", "Kod" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_TasinirKartlar_MuhasebeHesapPlanlari_MuhasebeHesapPlaniId",
                schema: "muhasebe",
                table: "TasinirKartlar",
                column: "MuhasebeHesapPlaniId",
                principalSchema: "muhasebe",
                principalTable: "MuhasebeHesapPlanlari",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TasinirKartlar_MuhasebeHesapPlanlari_MuhasebeHesapPlaniId",
                schema: "muhasebe",
                table: "TasinirKartlar");

            migrationBuilder.DropIndex(
                name: "IX_TasinirKartlar_MuhasebeHesapPlaniId",
                schema: "muhasebe",
                table: "TasinirKartlar");

            migrationBuilder.DropIndex(
                name: "IX_TasinirKartlar_TesisId_AnaMuhasebeHesapKodu",
                schema: "muhasebe",
                table: "TasinirKartlar");

            migrationBuilder.DropIndex(
                name: "IX_Depolar_TesisId_AnaMuhasebeHesapKodu",
                schema: "muhasebe",
                table: "Depolar");

            migrationBuilder.DropIndex(
                name: "IX_Depolar_TesisId_Kod",
                schema: "muhasebe",
                table: "Depolar");

            migrationBuilder.DropColumn(
                name: "AnaMuhasebeHesapKodu",
                schema: "muhasebe",
                table: "TasinirKartlar");

            migrationBuilder.DropColumn(
                name: "MuhasebeHesapPlaniId",
                schema: "muhasebe",
                table: "TasinirKartlar");

            migrationBuilder.DropColumn(
                name: "MuhasebeHesapSiraNo",
                schema: "muhasebe",
                table: "TasinirKartlar");

            migrationBuilder.DropColumn(
                name: "AnaMuhasebeHesapKodu",
                schema: "muhasebe",
                table: "Depolar");

            migrationBuilder.DropColumn(
                name: "MuhasebeHesapSiraNo",
                schema: "muhasebe",
                table: "Depolar");

            migrationBuilder.CreateIndex(
                name: "IX_Depolar_Kod",
                schema: "muhasebe",
                table: "Depolar",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
