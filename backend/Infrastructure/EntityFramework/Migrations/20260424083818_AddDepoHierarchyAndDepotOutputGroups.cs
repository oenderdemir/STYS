using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddDepoHierarchyAndDepotOutputGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AvansGenel",
                schema: "muhasebe",
                table: "Depolar",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MalzemeKayitTipi",
                schema: "muhasebe",
                table: "Depolar",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "MalzemeleriAyriKayittaTut");

            migrationBuilder.AddColumn<int>(
                name: "MuhasebeHesapPlaniId",
                schema: "muhasebe",
                table: "Depolar",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SatisFiyatlariniGoster",
                schema: "muhasebe",
                table: "Depolar",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "UstDepoId",
                schema: "muhasebe",
                table: "Depolar",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DepoCikisGruplari",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepoId = table.Column<int>(type: "int", nullable: false),
                    CikisGrupAdi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    KarOrani = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    LokasyonId = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepoCikisGruplari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DepoCikisGruplari_Depolar_DepoId",
                        column: x => x.DepoId,
                        principalSchema: "muhasebe",
                        principalTable: "Depolar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Depolar_MuhasebeHesapPlaniId",
                schema: "muhasebe",
                table: "Depolar",
                column: "MuhasebeHesapPlaniId",
                filter: "[IsDeleted] = 0 AND [MuhasebeHesapPlaniId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Depolar_UstDepoId",
                schema: "muhasebe",
                table: "Depolar",
                column: "UstDepoId",
                filter: "[IsDeleted] = 0 AND [UstDepoId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DepoCikisGruplari_DepoId",
                schema: "muhasebe",
                table: "DepoCikisGruplari",
                column: "DepoId",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DepoCikisGruplari_DepoId_CikisGrupAdi",
                schema: "muhasebe",
                table: "DepoCikisGruplari",
                columns: new[] { "DepoId", "CikisGrupAdi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_Depolar_Depolar_UstDepoId",
                schema: "muhasebe",
                table: "Depolar",
                column: "UstDepoId",
                principalSchema: "muhasebe",
                principalTable: "Depolar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Depolar_MuhasebeHesapPlanlari_MuhasebeHesapPlaniId",
                schema: "muhasebe",
                table: "Depolar",
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
                name: "FK_Depolar_Depolar_UstDepoId",
                schema: "muhasebe",
                table: "Depolar");

            migrationBuilder.DropForeignKey(
                name: "FK_Depolar_MuhasebeHesapPlanlari_MuhasebeHesapPlaniId",
                schema: "muhasebe",
                table: "Depolar");

            migrationBuilder.DropTable(
                name: "DepoCikisGruplari",
                schema: "muhasebe");

            migrationBuilder.DropIndex(
                name: "IX_Depolar_MuhasebeHesapPlaniId",
                schema: "muhasebe",
                table: "Depolar");

            migrationBuilder.DropIndex(
                name: "IX_Depolar_UstDepoId",
                schema: "muhasebe",
                table: "Depolar");

            migrationBuilder.DropColumn(
                name: "AvansGenel",
                schema: "muhasebe",
                table: "Depolar");

            migrationBuilder.DropColumn(
                name: "MalzemeKayitTipi",
                schema: "muhasebe",
                table: "Depolar");

            migrationBuilder.DropColumn(
                name: "MuhasebeHesapPlaniId",
                schema: "muhasebe",
                table: "Depolar");

            migrationBuilder.DropColumn(
                name: "SatisFiyatlariniGoster",
                schema: "muhasebe",
                table: "Depolar");

            migrationBuilder.DropColumn(
                name: "UstDepoId",
                schema: "muhasebe",
                table: "Depolar");
        }
    }
}
