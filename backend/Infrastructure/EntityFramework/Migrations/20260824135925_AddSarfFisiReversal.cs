using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddSarfFisiReversal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IptalAciklamasi",
                schema: "muhasebe",
                table: "SarfFisleri",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IptalEdenKullaniciId",
                schema: "muhasebe",
                table: "SarfFisleri",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IptalTarihi",
                schema: "muhasebe",
                table: "SarfFisleri",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IptalStokHareketId",
                schema: "muhasebe",
                table: "SarfFisiSatirlari",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SarfFisiSatirlari_IptalStokHareketId",
                schema: "muhasebe",
                table: "SarfFisiSatirlari",
                column: "IptalStokHareketId",
                filter: "[IsDeleted] = 0 AND [IptalStokHareketId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_SarfFisiSatirlari_StokHareketleri_IptalStokHareketId",
                schema: "muhasebe",
                table: "SarfFisiSatirlari",
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
                name: "FK_SarfFisiSatirlari_StokHareketleri_IptalStokHareketId",
                schema: "muhasebe",
                table: "SarfFisiSatirlari");

            migrationBuilder.DropIndex(
                name: "IX_SarfFisiSatirlari_IptalStokHareketId",
                schema: "muhasebe",
                table: "SarfFisiSatirlari");

            migrationBuilder.DropColumn(
                name: "IptalAciklamasi",
                schema: "muhasebe",
                table: "SarfFisleri");

            migrationBuilder.DropColumn(
                name: "IptalEdenKullaniciId",
                schema: "muhasebe",
                table: "SarfFisleri");

            migrationBuilder.DropColumn(
                name: "IptalTarihi",
                schema: "muhasebe",
                table: "SarfFisleri");

            migrationBuilder.DropColumn(
                name: "IptalStokHareketId",
                schema: "muhasebe",
                table: "SarfFisiSatirlari");
        }
    }
}
