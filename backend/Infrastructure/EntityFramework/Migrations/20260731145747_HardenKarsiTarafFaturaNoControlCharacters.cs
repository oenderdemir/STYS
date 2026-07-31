using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class HardenKarsiTarafFaturaNoControlCharacters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SatisBelgeleri_KarsiTarafFaturaNo_Format",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SatisBelgeleri_KarsiTarafFaturaNo_Format",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                sql: "[KarsiTarafFaturaNo] IS NULL OR (DATALENGTH([KarsiTarafFaturaNo]) > 0 AND DATALENGTH([KarsiTarafFaturaNo]) = DATALENGTH(LTRIM(RTRIM([KarsiTarafFaturaNo]))) AND [KarsiTarafFaturaNo] COLLATE Latin1_General_BIN2 NOT LIKE '%[' + CHAR(1) + '-' + CHAR(31) + CHAR(127) + NCHAR(128) + '-' + NCHAR(159) + ']%')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SatisBelgeleri_KarsiTarafFaturaNo_Format",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SatisBelgeleri_KarsiTarafFaturaNo_Format",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                sql: "[KarsiTarafFaturaNo] IS NULL OR (DATALENGTH([KarsiTarafFaturaNo]) > 0 AND DATALENGTH([KarsiTarafFaturaNo]) = DATALENGTH(LTRIM(RTRIM([KarsiTarafFaturaNo]))) AND [KarsiTarafFaturaNo] COLLATE Latin1_General_BIN2 NOT LIKE '%[' + CHAR(1) + '-' + CHAR(31) + ']%')");
        }
    }
}
