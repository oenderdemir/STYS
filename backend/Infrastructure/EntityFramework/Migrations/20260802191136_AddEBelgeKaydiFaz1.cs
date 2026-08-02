using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddEBelgeKaydiFaz1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF EXISTS (
    SELECT 1
    FROM [muhasebe].[SatisBelgeleri]
    WHERE [ResmiFaturaNo] IS NOT NULL
    GROUP BY [KurumId], [ResmiFaturaNo]
    HAVING COUNT(*) > 1
)
BEGIN
    THROW 51000, 'Cannot remove the IsDeleted filter from IX_SatisBelgeleri_KurumId_ResmiFaturaNo because historical duplicates exist for the same KurumId and ResmiFaturaNo. Resolve duplicates first.', 1;
END
""");

            migrationBuilder.DropIndex(
                name: "IX_SatisBelgeleri_KurumId_ResmiFaturaNo",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_SatisBelgeleri_Id_KurumId",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                columns: new[] { "Id", "KurumId" });

            migrationBuilder.CreateTable(
                name: "EBelgeKayitlari",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KurumId = table.Column<int>(type: "int", nullable: false),
                    SatisBelgesiId = table.Column<int>(type: "int", nullable: false),
                    EBelgeUuid = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EBelgeKanali = table.Column<int>(type: "int", nullable: false),
                    Durum = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_EBelgeKayitlari", x => x.Id);
                    table.CheckConstraint("CK_EBelgeKayitlari_Durum", "[Durum] IN (1)");
                });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_EBelgeKayitlari_Id_KurumId",
                schema: "muhasebe",
                table: "EBelgeKayitlari",
                columns: new[] { "Id", "KurumId" });

            migrationBuilder.CreateTable(
                name: "EBelgeSnapshots",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KurumId = table.Column<int>(type: "int", nullable: false),
                    EBelgeKaydiId = table.Column<int>(type: "int", nullable: false),
                    BelgeVersiyonu = table.Column<int>(type: "int", nullable: false),
                    SnapshotSchemaVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CanonicalJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CanonicalSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_EBelgeSnapshots", x => x.Id);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_EBelgeKayitlari_SatisBelgeleri_SatisBelgesiId_KurumId",
                schema: "muhasebe",
                table: "EBelgeKayitlari",
                columns: new[] { "SatisBelgesiId", "KurumId" },
                principalSchema: "muhasebe",
                principalTable: "SatisBelgeleri",
                principalColumns: new[] { "Id", "KurumId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EBelgeSnapshots_EBelgeKayitlari_EBelgeKaydiId_KurumId",
                schema: "muhasebe",
                table: "EBelgeSnapshots",
                columns: new[] { "EBelgeKaydiId", "KurumId" },
                principalSchema: "muhasebe",
                principalTable: "EBelgeKayitlari",
                principalColumns: new[] { "Id", "KurumId" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.CreateIndex(
                name: "IX_SatisBelgeleri_KurumId_ResmiFaturaNo",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                columns: new[] { "KurumId", "ResmiFaturaNo" },
                unique: true,
                filter: "[ResmiFaturaNo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EBelgeKayitlari_EBelgeUuid",
                schema: "muhasebe",
                table: "EBelgeKayitlari",
                column: "EBelgeUuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EBelgeKayitlari_SatisBelgesiId",
                schema: "muhasebe",
                table: "EBelgeKayitlari",
                column: "SatisBelgesiId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EBelgeKayitlari_SatisBelgesiId_KurumId",
                schema: "muhasebe",
                table: "EBelgeKayitlari",
                columns: new[] { "SatisBelgesiId", "KurumId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EBelgeSnapshots_EBelgeKaydiId_KurumId",
                schema: "muhasebe",
                table: "EBelgeSnapshots",
                columns: new[] { "EBelgeKaydiId", "KurumId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EBelgeSnapshots_EBelgeKaydiId_BelgeVersiyonu",
                schema: "muhasebe",
                table: "EBelgeSnapshots",
                columns: new[] { "EBelgeKaydiId", "BelgeVersiyonu" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EBelgeSnapshots",
                schema: "muhasebe");

            migrationBuilder.DropTable(
                name: "EBelgeKayitlari",
                schema: "muhasebe");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_SatisBelgeleri_Id_KurumId",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.DropIndex(
                name: "IX_SatisBelgeleri_KurumId_ResmiFaturaNo",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.CreateIndex(
                name: "IX_SatisBelgeleri_KurumId_ResmiFaturaNo",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                columns: new[] { "KurumId", "ResmiFaturaNo" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [ResmiFaturaNo] IS NOT NULL");
        }
    }
}
