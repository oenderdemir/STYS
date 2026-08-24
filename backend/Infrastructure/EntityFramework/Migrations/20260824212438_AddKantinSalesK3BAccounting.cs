using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddKantinSalesK3BAccounting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MuhasebeFisId",
                schema: "kantin",
                table: "KantinSatislar",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MuhasebeFisOlusturmaTarihi",
                schema: "kantin",
                table: "KantinSatislar",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_KantinSatislar_MuhasebeFisId",
                schema: "kantin",
                table: "KantinSatislar",
                column: "MuhasebeFisId",
                unique: true,
                filter: "[IsDeleted] = 0 AND [MuhasebeFisId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_KantinSatislar_MuhasebeFisler_MuhasebeFisId",
                schema: "kantin",
                table: "KantinSatislar",
                column: "MuhasebeFisId",
                principalSchema: "muhasebe",
                principalTable: "MuhasebeFisler",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KantinSatislar_MuhasebeFisler_MuhasebeFisId",
                schema: "kantin",
                table: "KantinSatislar");

            migrationBuilder.DropIndex(
                name: "IX_KantinSatislar_MuhasebeFisId",
                schema: "kantin",
                table: "KantinSatislar");

            migrationBuilder.DropColumn(
                name: "MuhasebeFisId",
                schema: "kantin",
                table: "KantinSatislar");

            migrationBuilder.DropColumn(
                name: "MuhasebeFisOlusturmaTarihi",
                schema: "kantin",
                table: "KantinSatislar");
        }
    }
}
