using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class FixTasinirKodMuhasebeHesapEslemeFaz1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TasinirKodMuhasebeHesapEslemeleri_TasinirKodId_MuhasebeHesapPlaniId_IslemTuru",
                schema: "muhasebe",
                table: "TasinirKodMuhasebeHesapEslemeleri");

            migrationBuilder.AlterColumn<string>(
                name: "MalzemeTipi",
                schema: "muhasebe",
                table: "TasinirKodMuhasebeHesapEslemeleri",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "IslemTuru",
                schema: "muhasebe",
                table: "TasinirKodMuhasebeHesapEslemeleri",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "HareketTipi",
                schema: "muhasebe",
                table: "TasinirKodMuhasebeHesapEslemeleri",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.CreateIndex(
                name: "IX_TasinirKodMuhasebeHesapEslemeleri_TasinirKodId",
                schema: "muhasebe",
                table: "TasinirKodMuhasebeHesapEslemeleri",
                column: "TasinirKodId");

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
            migrationBuilder.DropIndex(
                name: "IX_TasinirKodMuhasebeHesapEslemeleri_TasinirKodId",
                schema: "muhasebe",
                table: "TasinirKodMuhasebeHesapEslemeleri");

            migrationBuilder.DropIndex(
                name: "IX_TasinirKodMuhasebeHesapEslemeleri_TasinirKodId_MalzemeTipi_HareketTipi",
                schema: "muhasebe",
                table: "TasinirKodMuhasebeHesapEslemeleri");

            migrationBuilder.AlterColumn<string>(
                name: "MalzemeTipi",
                schema: "muhasebe",
                table: "TasinirKodMuhasebeHesapEslemeleri",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "IslemTuru",
                schema: "muhasebe",
                table: "TasinirKodMuhasebeHesapEslemeleri",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "HareketTipi",
                schema: "muhasebe",
                table: "TasinirKodMuhasebeHesapEslemeleri",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.CreateIndex(
                name: "IX_TasinirKodMuhasebeHesapEslemeleri_TasinirKodId_MuhasebeHesapPlaniId_IslemTuru",
                schema: "muhasebe",
                table: "TasinirKodMuhasebeHesapEslemeleri",
                columns: new[] { "TasinirKodId", "MuhasebeHesapPlaniId", "IslemTuru" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
