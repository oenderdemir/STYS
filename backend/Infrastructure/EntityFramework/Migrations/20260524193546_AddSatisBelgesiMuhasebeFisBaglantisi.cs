using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddSatisBelgesiMuhasebeFisBaglantisi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MuhasebeFisId",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MuhasebeFisOlusturmaTarihi",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SatisBelgeleri_MuhasebeFisId",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                column: "MuhasebeFisId",
                unique: true,
                filter: "[MuhasebeFisId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_SatisBelgeleri_MuhasebeFisler_MuhasebeFisId",
                schema: "muhasebe",
                table: "SatisBelgeleri",
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
                name: "FK_SatisBelgeleri_MuhasebeFisler_MuhasebeFisId",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.DropIndex(
                name: "IX_SatisBelgeleri_MuhasebeFisId",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.DropColumn(
                name: "MuhasebeFisId",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.DropColumn(
                name: "MuhasebeFisOlusturmaTarihi",
                schema: "muhasebe",
                table: "SatisBelgeleri");
        }
    }
}
