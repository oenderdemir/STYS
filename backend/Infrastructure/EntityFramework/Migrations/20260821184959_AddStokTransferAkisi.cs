using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddStokTransferAkisi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "KarsiDepoId",
                schema: "muhasebe",
                table: "StokHareketleri",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TransferGrupId",
                schema: "muhasebe",
                table: "StokHareketleri",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransferYonu",
                schema: "muhasebe",
                table: "StokHareketleri",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StokHareketleri_KarsiDepoId",
                schema: "muhasebe",
                table: "StokHareketleri",
                column: "KarsiDepoId");

            migrationBuilder.CreateIndex(
                name: "IX_StokHareketleri_TransferGrupId",
                schema: "muhasebe",
                table: "StokHareketleri",
                column: "TransferGrupId",
                filter: "[IsDeleted] = 0 AND [TransferGrupId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_StokHareketleri_Depolar_KarsiDepoId",
                schema: "muhasebe",
                table: "StokHareketleri",
                column: "KarsiDepoId",
                principalSchema: "muhasebe",
                principalTable: "Depolar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StokHareketleri_Depolar_KarsiDepoId",
                schema: "muhasebe",
                table: "StokHareketleri");

            migrationBuilder.DropIndex(
                name: "IX_StokHareketleri_KarsiDepoId",
                schema: "muhasebe",
                table: "StokHareketleri");

            migrationBuilder.DropIndex(
                name: "IX_StokHareketleri_TransferGrupId",
                schema: "muhasebe",
                table: "StokHareketleri");

            migrationBuilder.DropColumn(
                name: "KarsiDepoId",
                schema: "muhasebe",
                table: "StokHareketleri");

            migrationBuilder.DropColumn(
                name: "TransferGrupId",
                schema: "muhasebe",
                table: "StokHareketleri");

            migrationBuilder.DropColumn(
                name: "TransferYonu",
                schema: "muhasebe",
                table: "StokHareketleri");
        }
    }
}
