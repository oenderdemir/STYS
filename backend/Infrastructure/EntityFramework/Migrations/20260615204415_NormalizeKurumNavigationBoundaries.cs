using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeKurumNavigationBoundaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KampDonemleri_Kurumlar_KurumId",
                schema: "dbo",
                table: "KampDonemleri");

            migrationBuilder.DropForeignKey(
                name: "FK_Rezervasyonlar_Kurumlar_KurumId",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropIndex(
                name: "IX_Rezervasyonlar_KurumId_ReferansNo",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropIndex(
                name: "IX_Rezervasyonlar_KurumId_RezervasyonDurumu",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropIndex(
                name: "IX_Rezervasyonlar_KurumId_TesisId_GirisTarihi_CikisTarihi",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropIndex(
                name: "IX_Rezervasyonlar_TesisId",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropIndex(
                name: "IX_KampDonemleri_KampProgramiId",
                schema: "dbo",
                table: "KampDonemleri");

            migrationBuilder.DropIndex(
                name: "IX_KampDonemleri_KurumId_KampProgramiId_Ad",
                schema: "dbo",
                table: "KampDonemleri");

            migrationBuilder.DropIndex(
                name: "IX_KampDonemleri_KurumId_Kod",
                schema: "dbo",
                table: "KampDonemleri");

            migrationBuilder.DropColumn(
                name: "KurumId",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropColumn(
                name: "KurumId",
                schema: "dbo",
                table: "KampDonemleri");

            migrationBuilder.CreateIndex(
                name: "IX_Rezervasyonlar_ReferansNo",
                schema: "dbo",
                table: "Rezervasyonlar",
                column: "ReferansNo",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Rezervasyonlar_TesisId_GirisTarihi_CikisTarihi",
                schema: "dbo",
                table: "Rezervasyonlar",
                columns: new[] { "TesisId", "GirisTarihi", "CikisTarihi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Rezervasyonlar_TesisId_RezervasyonDurumu",
                schema: "dbo",
                table: "Rezervasyonlar",
                columns: new[] { "TesisId", "RezervasyonDurumu" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampDonemleri_KampProgramiId_Ad",
                schema: "dbo",
                table: "KampDonemleri",
                columns: new[] { "KampProgramiId", "Ad" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampDonemleri_KampProgramiId_Kod",
                schema: "dbo",
                table: "KampDonemleri",
                columns: new[] { "KampProgramiId", "Kod" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Rezervasyonlar_ReferansNo",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropIndex(
                name: "IX_Rezervasyonlar_TesisId_GirisTarihi_CikisTarihi",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropIndex(
                name: "IX_Rezervasyonlar_TesisId_RezervasyonDurumu",
                schema: "dbo",
                table: "Rezervasyonlar");

            migrationBuilder.DropIndex(
                name: "IX_KampDonemleri_KampProgramiId_Ad",
                schema: "dbo",
                table: "KampDonemleri");

            migrationBuilder.DropIndex(
                name: "IX_KampDonemleri_KampProgramiId_Kod",
                schema: "dbo",
                table: "KampDonemleri");

            migrationBuilder.AddColumn<int>(
                name: "KurumId",
                schema: "dbo",
                table: "Rezervasyonlar",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KurumId",
                schema: "dbo",
                table: "KampDonemleri",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE d
                SET d.KurumId = p.KurumId
                FROM [dbo].[KampDonemleri] d
                INNER JOIN [dbo].[KampProgramlari] p ON p.Id = d.KampProgramiId
                WHERE d.KurumId IS NULL;
            ");

            migrationBuilder.Sql(@"
                UPDATE r
                SET r.KurumId = t.KurumId
                FROM [dbo].[Rezervasyonlar] r
                INNER JOIN [dbo].[Tesisler] t ON t.Id = r.TesisId
                WHERE r.KurumId IS NULL;
            ");

            migrationBuilder.AlterColumn<int>(
                name: "KurumId",
                schema: "dbo",
                table: "Rezervasyonlar",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "KurumId",
                schema: "dbo",
                table: "KampDonemleri",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rezervasyonlar_KurumId_ReferansNo",
                schema: "dbo",
                table: "Rezervasyonlar",
                columns: new[] { "KurumId", "ReferansNo" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Rezervasyonlar_KurumId_RezervasyonDurumu",
                schema: "dbo",
                table: "Rezervasyonlar",
                columns: new[] { "KurumId", "RezervasyonDurumu" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Rezervasyonlar_KurumId_TesisId_GirisTarihi_CikisTarihi",
                schema: "dbo",
                table: "Rezervasyonlar",
                columns: new[] { "KurumId", "TesisId", "GirisTarihi", "CikisTarihi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Rezervasyonlar_TesisId",
                schema: "dbo",
                table: "Rezervasyonlar",
                column: "TesisId");

            migrationBuilder.CreateIndex(
                name: "IX_KampDonemleri_KampProgramiId",
                schema: "dbo",
                table: "KampDonemleri",
                column: "KampProgramiId");

            migrationBuilder.CreateIndex(
                name: "IX_KampDonemleri_KurumId_KampProgramiId_Ad",
                schema: "dbo",
                table: "KampDonemleri",
                columns: new[] { "KurumId", "KampProgramiId", "Ad" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampDonemleri_KurumId_Kod",
                schema: "dbo",
                table: "KampDonemleri",
                columns: new[] { "KurumId", "Kod" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_KampDonemleri_Kurumlar_KurumId",
                schema: "dbo",
                table: "KampDonemleri",
                column: "KurumId",
                principalSchema: "dbo",
                principalTable: "Kurumlar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Rezervasyonlar_Kurumlar_KurumId",
                schema: "dbo",
                table: "Rezervasyonlar",
                column: "KurumId",
                principalSchema: "dbo",
                principalTable: "Kurumlar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
