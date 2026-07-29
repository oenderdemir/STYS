using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddPosPaymentStatusWorker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SaglayiciDurumKodu",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SonSorgulamaHatasi",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SonrakiSorgulamaTarihi",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SorgulamaDenemeSayisi",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "TakipKilitBitisTarihi",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TakipKilitToken",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PosOdemeIslemleri_Durum_SonrakiSorgulamaTarihi_TakipKilitBitisTarihi",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                columns: new[] { "Durum", "SonrakiSorgulamaTarihi", "TakipKilitBitisTarihi" },
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PosOdemeIslemleri_Durum_SonrakiSorgulamaTarihi_TakipKilitBitisTarihi",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri");

            migrationBuilder.DropColumn(
                name: "SaglayiciDurumKodu",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri");

            migrationBuilder.DropColumn(
                name: "SonSorgulamaHatasi",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri");

            migrationBuilder.DropColumn(
                name: "SonrakiSorgulamaTarihi",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri");

            migrationBuilder.DropColumn(
                name: "SorgulamaDenemeSayisi",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri");

            migrationBuilder.DropColumn(
                name: "TakipKilitBitisTarihi",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri");

            migrationBuilder.DropColumn(
                name: "TakipKilitToken",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri");
        }
    }
}
