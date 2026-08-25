using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddKantinSatisIptal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IptalStokHareketId",
                schema: "kantin",
                table: "KantinSatisSatirlari",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IptalAciklamasi",
                schema: "kantin",
                table: "KantinSatislar",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IptalEdenKullaniciId",
                schema: "kantin",
                table: "KantinSatislar",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IptalTarihi",
                schema: "kantin",
                table: "KantinSatislar",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_KantinSatisSatirlari_IptalStokHareketId",
                schema: "kantin",
                table: "KantinSatisSatirlari",
                column: "IptalStokHareketId",
                unique: true,
                filter: "[IsDeleted] = 0 AND [IptalStokHareketId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_KantinSatisSatirlari_StokHareketleri_IptalStokHareketId",
                schema: "kantin",
                table: "KantinSatisSatirlari",
                column: "IptalStokHareketId",
                principalSchema: "muhasebe",
                principalTable: "StokHareketleri",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KantinSatisSatirlari_StokHareketleri_IptalStokHareketId",
                schema: "kantin",
                table: "KantinSatisSatirlari");

            migrationBuilder.DropIndex(
                name: "IX_KantinSatisSatirlari_IptalStokHareketId",
                schema: "kantin",
                table: "KantinSatisSatirlari");

            migrationBuilder.DropColumn(
                name: "IptalStokHareketId",
                schema: "kantin",
                table: "KantinSatisSatirlari");

            migrationBuilder.DropColumn(
                name: "IptalAciklamasi",
                schema: "kantin",
                table: "KantinSatislar");

            migrationBuilder.DropColumn(
                name: "IptalEdenKullaniciId",
                schema: "kantin",
                table: "KantinSatislar");

            migrationBuilder.DropColumn(
                name: "IptalTarihi",
                schema: "kantin",
                table: "KantinSatislar");
        }
    }
}
