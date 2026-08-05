using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddEBelgeSigningFaz2B7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_EBelgeOutboxMesajlari_IsTuru",
                schema: "muhasebe",
                table: "EBelgeOutboxMesajlari");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EBelgeKayitlari_Durum",
                schema: "muhasebe",
                table: "EBelgeKayitlari");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EBelgeArtifactlari_ArtifactAsamasi",
                schema: "muhasebe",
                table: "EBelgeArtifactlari");

            migrationBuilder.AddColumn<string>(
                name: "DigestAlgoritmasi",
                schema: "muhasebe",
                table: "EBelgeArtifactlari",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImzaAlgoritmasi",
                schema: "muhasebe",
                table: "EBelgeArtifactlari",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImzaProfili",
                schema: "muhasebe",
                table: "EBelgeArtifactlari",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ImzalamaZamaniUtc",
                schema: "muhasebe",
                table: "EBelgeArtifactlari",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImzalayanSertifikaSha256ParmakIzi",
                schema: "muhasebe",
                table: "EBelgeArtifactlari",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "KaynakArtifactId",
                schema: "muhasebe",
                table: "EBelgeArtifactlari",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KaynakArtifactSha256",
                schema: "muhasebe",
                table: "EBelgeArtifactlari",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_EBelgeArtifactlari_Id_KurumId",
                schema: "muhasebe",
                table: "EBelgeArtifactlari",
                columns: new[] { "Id", "KurumId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_EBelgeOutboxMesajlari_IsTuru",
                schema: "muhasebe",
                table: "EBelgeOutboxMesajlari",
                sql: "[IsTuru] IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EBelgeKayitlari_Durum",
                schema: "muhasebe",
                table: "EBelgeKayitlari",
                sql: "[Durum] IN (1, 2, 3, 4, 5)");

            migrationBuilder.CreateIndex(
                name: "IX_EBelgeArtifactlari_KaynakArtifactId_KurumId",
                schema: "muhasebe",
                table: "EBelgeArtifactlari",
                columns: new[] { "KaynakArtifactId", "KurumId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_EBelgeArtifactlari_ArtifactAsamasi",
                schema: "muhasebe",
                table: "EBelgeArtifactlari",
                sql: "[ArtifactAsamasi] IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EBelgeArtifactlari_ImzaAlanlari",
                schema: "muhasebe",
                table: "EBelgeArtifactlari",
                sql: "([ArtifactAsamasi] = 2 AND [KaynakArtifactId] IS NOT NULL AND [KaynakArtifactSha256] IS NOT NULL\r\n    AND [ImzaProfili] IS NOT NULL AND [ImzaAlgoritmasi] IS NOT NULL AND [DigestAlgoritmasi] IS NOT NULL\r\n    AND [ImzalayanSertifikaSha256ParmakIzi] IS NOT NULL AND [ImzalamaZamaniUtc] IS NOT NULL)\r\nOR\r\n([ArtifactAsamasi] = 1 AND [KaynakArtifactId] IS NULL AND [KaynakArtifactSha256] IS NULL\r\n    AND [ImzaProfili] IS NULL AND [ImzaAlgoritmasi] IS NULL AND [DigestAlgoritmasi] IS NULL\r\n    AND [ImzalayanSertifikaSha256ParmakIzi] IS NULL AND [ImzalamaZamaniUtc] IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_EBelgeArtifactlari_EBelgeArtifactlari_KaynakArtifactId_KurumId",
                schema: "muhasebe",
                table: "EBelgeArtifactlari",
                columns: new[] { "KaynakArtifactId", "KurumId" },
                principalSchema: "muhasebe",
                principalTable: "EBelgeArtifactlari",
                principalColumns: new[] { "Id", "KurumId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EBelgeArtifactlari_EBelgeArtifactlari_KaynakArtifactId_KurumId",
                schema: "muhasebe",
                table: "EBelgeArtifactlari");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EBelgeOutboxMesajlari_IsTuru",
                schema: "muhasebe",
                table: "EBelgeOutboxMesajlari");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EBelgeKayitlari_Durum",
                schema: "muhasebe",
                table: "EBelgeKayitlari");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_EBelgeArtifactlari_Id_KurumId",
                schema: "muhasebe",
                table: "EBelgeArtifactlari");

            migrationBuilder.DropIndex(
                name: "IX_EBelgeArtifactlari_KaynakArtifactId_KurumId",
                schema: "muhasebe",
                table: "EBelgeArtifactlari");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EBelgeArtifactlari_ArtifactAsamasi",
                schema: "muhasebe",
                table: "EBelgeArtifactlari");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EBelgeArtifactlari_ImzaAlanlari",
                schema: "muhasebe",
                table: "EBelgeArtifactlari");

            migrationBuilder.DropColumn(
                name: "DigestAlgoritmasi",
                schema: "muhasebe",
                table: "EBelgeArtifactlari");

            migrationBuilder.DropColumn(
                name: "ImzaAlgoritmasi",
                schema: "muhasebe",
                table: "EBelgeArtifactlari");

            migrationBuilder.DropColumn(
                name: "ImzaProfili",
                schema: "muhasebe",
                table: "EBelgeArtifactlari");

            migrationBuilder.DropColumn(
                name: "ImzalamaZamaniUtc",
                schema: "muhasebe",
                table: "EBelgeArtifactlari");

            migrationBuilder.DropColumn(
                name: "ImzalayanSertifikaSha256ParmakIzi",
                schema: "muhasebe",
                table: "EBelgeArtifactlari");

            migrationBuilder.DropColumn(
                name: "KaynakArtifactId",
                schema: "muhasebe",
                table: "EBelgeArtifactlari");

            migrationBuilder.DropColumn(
                name: "KaynakArtifactSha256",
                schema: "muhasebe",
                table: "EBelgeArtifactlari");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EBelgeOutboxMesajlari_IsTuru",
                schema: "muhasebe",
                table: "EBelgeOutboxMesajlari",
                sql: "[IsTuru] IN (1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EBelgeKayitlari_Durum",
                schema: "muhasebe",
                table: "EBelgeKayitlari",
                sql: "[Durum] IN (1, 2, 3)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EBelgeArtifactlari_ArtifactAsamasi",
                schema: "muhasebe",
                table: "EBelgeArtifactlari",
                sql: "[ArtifactAsamasi] IN (1)");
        }
    }
}
