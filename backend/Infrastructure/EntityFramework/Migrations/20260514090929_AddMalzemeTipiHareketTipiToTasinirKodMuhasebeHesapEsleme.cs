using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddMalzemeTipiHareketTipiToTasinirKodMuhasebeHesapEsleme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnaHesapKodu",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DetayHesapMi",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HareketGorebilirMi",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "HesapTipi",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ResmiKod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UygulamaKodu",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TasinirKodMuhasebeHesapEslemeleri",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TasinirKodId = table.Column<int>(type: "int", nullable: false),
                    MuhasebeHesapPlaniId = table.Column<int>(type: "int", nullable: false),
                    IslemTuru = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MalzemeTipi = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    HareketTipi = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    VarsayilanMi = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_TasinirKodMuhasebeHesapEslemeleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TasinirKodMuhasebeHesapEslemeleri_MuhasebeHesapPlanlari_MuhasebeHesapPlaniId",
                        column: x => x.MuhasebeHesapPlaniId,
                        principalSchema: "muhasebe",
                        principalTable: "MuhasebeHesapPlanlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TasinirKodMuhasebeHesapEslemeleri_TasinirKodlar_TasinirKodId",
                        column: x => x.TasinirKodId,
                        principalSchema: "muhasebe",
                        principalTable: "TasinirKodlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TasinirKodMuhasebeHesapEslemeleri_MuhasebeHesapPlaniId",
                schema: "muhasebe",
                table: "TasinirKodMuhasebeHesapEslemeleri",
                column: "MuhasebeHesapPlaniId");

            migrationBuilder.CreateIndex(
                name: "IX_TasinirKodMuhasebeHesapEslemeleri_TasinirKodId_MalzemeTipi_HareketTipi",
                schema: "muhasebe",
                table: "TasinirKodMuhasebeHesapEslemeleri",
                columns: new[] { "TasinirKodId", "MalzemeTipi", "HareketTipi" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1 AND [VarsayilanMi] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TasinirKodMuhasebeHesapEslemeleri",
                schema: "muhasebe");

            migrationBuilder.DropColumn(
                name: "AnaHesapKodu",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari");

            migrationBuilder.DropColumn(
                name: "DetayHesapMi",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari");

            migrationBuilder.DropColumn(
                name: "HareketGorebilirMi",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari");

            migrationBuilder.DropColumn(
                name: "HesapTipi",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari");

            migrationBuilder.DropColumn(
                name: "ResmiKod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari");

            migrationBuilder.DropColumn(
                name: "UygulamaKodu",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari");
        }
    }
}
