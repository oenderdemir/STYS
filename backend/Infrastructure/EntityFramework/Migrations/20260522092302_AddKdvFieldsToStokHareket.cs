using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddKdvFieldsToStokHareket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KdvIstisnaAciklamasi",
                schema: "muhasebe",
                table: "StokHareketleri",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KdvIstisnaKodu",
                schema: "muhasebe",
                table: "StokHareketleri",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KdvIstisnaTanimId",
                schema: "muhasebe",
                table: "StokHareketleri",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "KdvOrani",
                schema: "muhasebe",
                table: "StokHareketleri",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "KdvTutari",
                schema: "muhasebe",
                table: "StokHareketleri",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "KdvUygulamaTipi",
                schema: "muhasebe",
                table: "StokHareketleri",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_StokHareketleri_KdvIstisnaTanimId",
                schema: "muhasebe",
                table: "StokHareketleri",
                column: "KdvIstisnaTanimId");

            migrationBuilder.AddForeignKey(
                name: "FK_StokHareketleri_KdvIstisnaTanimlari_KdvIstisnaTanimId",
                schema: "muhasebe",
                table: "StokHareketleri",
                column: "KdvIstisnaTanimId",
                principalSchema: "muhasebe",
                principalTable: "KdvIstisnaTanimlari",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Mevcut kayitlarin KdvUygulamaTipi degerini Kdvli (1) olarak guncelle
            // KdvIstisnaTanimlari tablosu zaten 20260521210000_AddKdvIstisnaTanimlari migration'inda olusturulmustur
            migrationBuilder.Sql(
                "UPDATE [muhasebe].[StokHareketleri] SET [KdvUygulamaTipi] = 1 WHERE [KdvUygulamaTipi] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StokHareketleri_KdvIstisnaTanimlari_KdvIstisnaTanimId",
                schema: "muhasebe",
                table: "StokHareketleri");

            migrationBuilder.DropIndex(
                name: "IX_StokHareketleri_KdvIstisnaTanimId",
                schema: "muhasebe",
                table: "StokHareketleri");

            migrationBuilder.DropColumn(
                name: "KdvIstisnaAciklamasi",
                schema: "muhasebe",
                table: "StokHareketleri");

            migrationBuilder.DropColumn(
                name: "KdvIstisnaKodu",
                schema: "muhasebe",
                table: "StokHareketleri");

            migrationBuilder.DropColumn(
                name: "KdvIstisnaTanimId",
                schema: "muhasebe",
                table: "StokHareketleri");

            migrationBuilder.DropColumn(
                name: "KdvOrani",
                schema: "muhasebe",
                table: "StokHareketleri");

            migrationBuilder.DropColumn(
                name: "KdvTutari",
                schema: "muhasebe",
                table: "StokHareketleri");

            migrationBuilder.DropColumn(
                name: "KdvUygulamaTipi",
                schema: "muhasebe",
                table: "StokHareketleri");
        }
    }
}
