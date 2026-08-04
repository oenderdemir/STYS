using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddEBelgeArtifactFaz2B6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_EBelgeKayitlari_Durum",
                schema: "muhasebe",
                table: "EBelgeKayitlari");

            migrationBuilder.CreateTable(
                name: "EBelgeArtifactlari",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KurumId = table.Column<int>(type: "int", nullable: false),
                    EBelgeKaydiId = table.Column<int>(type: "int", nullable: false),
                    ArtifactTipi = table.Column<int>(type: "int", nullable: false),
                    ArtifactAsamasi = table.Column<int>(type: "int", nullable: false),
                    RuleSetId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SnapshotSchemaVersion = table.Column<int>(type: "int", nullable: false),
                    KaynakSnapshotSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ArtifactSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Icerik = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    MimeType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DosyaAdi = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    OlusturulmaZamaniUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_EBelgeArtifactlari", x => x.Id);
                    table.CheckConstraint("CK_EBelgeArtifactlari_ArtifactAsamasi", "[ArtifactAsamasi] IN (1)");
                    table.CheckConstraint("CK_EBelgeArtifactlari_ArtifactTipi", "[ArtifactTipi] IN (1)");
                    table.ForeignKey(
                        name: "FK_EBelgeArtifactlari_EBelgeKayitlari_EBelgeKaydiId_KurumId",
                        columns: x => new { x.EBelgeKaydiId, x.KurumId },
                        principalSchema: "muhasebe",
                        principalTable: "EBelgeKayitlari",
                        principalColumns: new[] { "Id", "KurumId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_EBelgeKayitlari_Durum",
                schema: "muhasebe",
                table: "EBelgeKayitlari",
                sql: "[Durum] IN (1, 2, 3)");

            migrationBuilder.CreateIndex(
                name: "IX_EBelgeArtifactlari_EBelgeKaydiId_KurumId",
                schema: "muhasebe",
                table: "EBelgeArtifactlari",
                columns: new[] { "EBelgeKaydiId", "KurumId" });

            migrationBuilder.CreateIndex(
                name: "IX_EBelgeArtifactlari_KurumId_EBelgeKaydiId_ArtifactTipi_ArtifactAsamasi",
                schema: "muhasebe",
                table: "EBelgeArtifactlari",
                columns: new[] { "KurumId", "EBelgeKaydiId", "ArtifactTipi", "ArtifactAsamasi" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EBelgeArtifactlari",
                schema: "muhasebe");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EBelgeKayitlari_Durum",
                schema: "muhasebe",
                table: "EBelgeKayitlari");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EBelgeKayitlari_Durum",
                schema: "muhasebe",
                table: "EBelgeKayitlari",
                sql: "[Durum] IN (1)");
        }
    }
}
