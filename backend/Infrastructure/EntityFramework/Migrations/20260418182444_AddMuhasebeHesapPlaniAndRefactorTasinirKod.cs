using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddMuhasebeHesapPlaniAndRefactorTasinirKod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Duzey1Kod",
                schema: "muhasebe",
                table: "TasinirKodlar");

            migrationBuilder.DropColumn(
                name: "Duzey2Kod",
                schema: "muhasebe",
                table: "TasinirKodlar");

            migrationBuilder.DropColumn(
                name: "Duzey3Kod",
                schema: "muhasebe",
                table: "TasinirKodlar");

            migrationBuilder.DropColumn(
                name: "Duzey4Kod",
                schema: "muhasebe",
                table: "TasinirKodlar");

            migrationBuilder.DropColumn(
                name: "Duzey5Kod",
                schema: "muhasebe",
                table: "TasinirKodlar");

            migrationBuilder.AddColumn<string>(
                name: "Kod",
                schema: "muhasebe",
                table: "TasinirKodlar",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [muhasebe].[TasinirKodlar]
                SET [Kod] =
                    CASE
                        WHEN CHARINDEX('.', [TamKod]) > 0 THEN RIGHT([TamKod], CHARINDEX('.', REVERSE([TamKod])) - 1)
                        ELSE [TamKod]
                    END
                WHERE [Kod] IS NULL OR LTRIM(RTRIM([Kod])) = N'';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Kod",
                schema: "muhasebe",
                table: "TasinirKodlar",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "MuhasebeHesapPlanlari",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kod = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TamKod = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SeviyeNo = table.Column<int>(type: "int", nullable: false),
                    UstHesapId = table.Column<int>(type: "int", nullable: true),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
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
                    table.PrimaryKey("PK_MuhasebeHesapPlanlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MuhasebeHesapPlanlari_MuhasebeHesapPlanlari_UstHesapId",
                        column: x => x.UstHesapId,
                        principalSchema: "muhasebe",
                        principalTable: "MuhasebeHesapPlanlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TasinirKodlar_UstKodId_Kod",
                schema: "muhasebe",
                table: "TasinirKodlar",
                columns: new[] { "UstKodId", "Kod" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeHesapPlanlari_TamKod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                column: "TamKod",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeHesapPlanlari_UstHesapId",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                column: "UstHesapId",
                filter: "[IsDeleted] = 0 AND [UstHesapId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeHesapPlanlari_UstHesapId_Kod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                columns: new[] { "UstHesapId", "Kod" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MuhasebeHesapPlanlari",
                schema: "muhasebe");

            migrationBuilder.DropIndex(
                name: "IX_TasinirKodlar_UstKodId_Kod",
                schema: "muhasebe",
                table: "TasinirKodlar");

            migrationBuilder.DropColumn(
                name: "Kod",
                schema: "muhasebe",
                table: "TasinirKodlar");

            migrationBuilder.AddColumn<string>(
                name: "Duzey1Kod",
                schema: "muhasebe",
                table: "TasinirKodlar",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Duzey2Kod",
                schema: "muhasebe",
                table: "TasinirKodlar",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Duzey3Kod",
                schema: "muhasebe",
                table: "TasinirKodlar",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Duzey4Kod",
                schema: "muhasebe",
                table: "TasinirKodlar",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Duzey5Kod",
                schema: "muhasebe",
                table: "TasinirKodlar",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);
        }
    }
}
