using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class Faz70SatisBelgesiSatiriAlanlari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Birim",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Adet");

            migrationBuilder.AddColumn<int>(
                name: "DepoId",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IndirimTutari",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "TasinirKartId",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TevkifatPay",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TevkifatPayda",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TevkifatTutari",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_SatisBelgesiSatirlari_DepoId",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari",
                column: "DepoId");

            migrationBuilder.CreateIndex(
                name: "IX_SatisBelgesiSatirlari_TasinirKartId",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari",
                column: "TasinirKartId");

            migrationBuilder.AddForeignKey(
                name: "FK_SatisBelgesiSatirlari_Depolar_DepoId",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari",
                column: "DepoId",
                principalSchema: "muhasebe",
                principalTable: "Depolar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SatisBelgesiSatirlari_TasinirKartlar_TasinirKartId",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari",
                column: "TasinirKartId",
                principalSchema: "muhasebe",
                principalTable: "TasinirKartlar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SatisBelgesiSatirlari_Depolar_DepoId",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari");

            migrationBuilder.DropForeignKey(
                name: "FK_SatisBelgesiSatirlari_TasinirKartlar_TasinirKartId",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari");

            migrationBuilder.DropIndex(
                name: "IX_SatisBelgesiSatirlari_DepoId",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari");

            migrationBuilder.DropIndex(
                name: "IX_SatisBelgesiSatirlari_TasinirKartId",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari");

            migrationBuilder.DropColumn(
                name: "Birim",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari");

            migrationBuilder.DropColumn(
                name: "DepoId",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari");

            migrationBuilder.DropColumn(
                name: "IndirimTutari",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari");

            migrationBuilder.DropColumn(
                name: "TasinirKartId",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari");

            migrationBuilder.DropColumn(
                name: "TevkifatPay",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari");

            migrationBuilder.DropColumn(
                name: "TevkifatPayda",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari");

            migrationBuilder.DropColumn(
                name: "TevkifatTutari",
                schema: "muhasebe",
                table: "SatisBelgesiSatirlari");
        }
    }
}
