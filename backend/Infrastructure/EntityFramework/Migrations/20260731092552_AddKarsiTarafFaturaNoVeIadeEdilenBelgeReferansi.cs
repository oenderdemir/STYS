using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddKarsiTarafFaturaNoVeIadeEdilenBelgeReferansi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IadeEdilenBelgeId",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KarsiTarafFaturaNo",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SatisBelgeleri_IadeEdilenBelgeId",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                column: "IadeEdilenBelgeId");

            migrationBuilder.CreateIndex(
                name: "IX_SatisBelgeleri_KurumId_CariKartId_KarsiTarafFaturaNo",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                columns: new[] { "KurumId", "CariKartId", "KarsiTarafFaturaNo" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [KarsiTarafFaturaNo] IS NOT NULL AND [BelgeTipi] IN (5, 6)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SatisBelgeleri_IadeEdilenBelgeId_BelgeTipi",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                sql: "[IadeEdilenBelgeId] IS NULL OR [BelgeTipi] IN (6, 7)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SatisBelgeleri_IadeEdilenBelgeId_NotSelf",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                sql: "[IadeEdilenBelgeId] IS NULL OR [IadeEdilenBelgeId] <> [Id]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SatisBelgeleri_KarsiTarafFaturaNo_BelgeTipi",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                sql: "[KarsiTarafFaturaNo] IS NULL OR [BelgeTipi] IN (5, 6)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SatisBelgeleri_KarsiTarafFaturaNo_Format",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                sql: "[KarsiTarafFaturaNo] IS NULL OR (LEN([KarsiTarafFaturaNo]) > 0 AND [KarsiTarafFaturaNo] = LTRIM(RTRIM([KarsiTarafFaturaNo])) AND [KarsiTarafFaturaNo] COLLATE Latin1_General_BIN2 NOT LIKE '%[' + CHAR(1) + '-' + CHAR(31) + ']%')");

            migrationBuilder.AddForeignKey(
                name: "FK_SatisBelgeleri_SatisBelgeleri_IadeEdilenBelgeId",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                column: "IadeEdilenBelgeId",
                principalSchema: "muhasebe",
                principalTable: "SatisBelgeleri",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SatisBelgeleri_SatisBelgeleri_IadeEdilenBelgeId",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.DropIndex(
                name: "IX_SatisBelgeleri_IadeEdilenBelgeId",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.DropIndex(
                name: "IX_SatisBelgeleri_KurumId_CariKartId_KarsiTarafFaturaNo",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SatisBelgeleri_IadeEdilenBelgeId_BelgeTipi",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SatisBelgeleri_IadeEdilenBelgeId_NotSelf",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SatisBelgeleri_KarsiTarafFaturaNo_BelgeTipi",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SatisBelgeleri_KarsiTarafFaturaNo_Format",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.DropColumn(
                name: "IadeEdilenBelgeId",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.DropColumn(
                name: "KarsiTarafFaturaNo",
                schema: "muhasebe",
                table: "SatisBelgeleri");
        }
    }
}
