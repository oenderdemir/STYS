using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddKantinSatisNoktalari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KantinSatisNoktalari",
                schema: "kantin",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KantinId = table.Column<int>(type: "int", nullable: false),
                    Kod = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VarsayilanNakitKasaId = table.Column<int>(type: "int", nullable: true),
                    VarsayilanPosHesapId = table.Column<int>(type: "int", nullable: true),
                    VarsayilanMi = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_KantinSatisNoktalari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KantinSatisNoktalari_Kantinler_KantinId",
                        column: x => x.KantinId,
                        principalSchema: "kantin",
                        principalTable: "Kantinler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KantinSatisNoktalari_KasaBankaHesaplari_VarsayilanNakitKasaId",
                        column: x => x.VarsayilanNakitKasaId,
                        principalSchema: "muhasebe",
                        principalTable: "KasaBankaHesaplari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KantinSatisNoktalari_KasaBankaHesaplari_VarsayilanPosHesapId",
                        column: x => x.VarsayilanPosHesapId,
                        principalSchema: "muhasebe",
                        principalTable: "KasaBankaHesaplari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Her mevcut Kantin icin "ANA" satis noktasi olustur ve eski
            // Kantin.VarsayilanNakitKasaId / VarsayilanPosHesapId degerlerini tasi.
            migrationBuilder.Sql(@"
                INSERT INTO [kantin].[KantinSatisNoktalari]
                    ([KantinId], [Kod], [Ad], [VarsayilanNakitKasaId], [VarsayilanPosHesapId],
                     [VarsayilanMi], [AktifMi], [Aciklama], [IsDeleted], [CreatedAt])
                SELECT
                    [Id], 'ANA', N'Ana Satış Noktası', [VarsayilanNakitKasaId], [VarsayilanPosHesapId],
                    CAST(1 AS bit), CAST(1 AS bit), NULL, CAST(0 AS bit), SYSUTCDATETIME()
                FROM [kantin].[Kantinler];
            ");

            migrationBuilder.AddColumn<int>(
                name: "SatisNoktasiId",
                schema: "kantin",
                table: "KantinSatislar",
                type: "int",
                nullable: true);

            // Mevcut KantinSatis kayitlarini kendi Kantin'inin ANA satis noktasina bagla.
            migrationBuilder.Sql(@"
                UPDATE s
                SET s.[SatisNoktasiId] = n.[Id]
                FROM [kantin].[KantinSatislar] s
                INNER JOIN [kantin].[KantinSatisNoktalari] n ON n.[KantinId] = s.[KantinId]
                WHERE s.[SatisNoktasiId] IS NULL;
            ");

            migrationBuilder.AlterColumn<int>(
                name: "SatisNoktasiId",
                schema: "kantin",
                table: "KantinSatislar",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_KantinSatislar_SatisNoktasiId",
                schema: "kantin",
                table: "KantinSatislar",
                column: "SatisNoktasiId",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KantinSatisNoktalari_KantinId",
                schema: "kantin",
                table: "KantinSatisNoktalari",
                column: "KantinId",
                unique: true,
                filter: "[IsDeleted] = 0 AND [VarsayilanMi] = 1 AND [AktifMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_KantinSatisNoktalari_KantinId_Kod",
                schema: "kantin",
                table: "KantinSatisNoktalari",
                columns: new[] { "KantinId", "Kod" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KantinSatisNoktalari_VarsayilanNakitKasaId",
                schema: "kantin",
                table: "KantinSatisNoktalari",
                column: "VarsayilanNakitKasaId",
                filter: "[IsDeleted] = 0 AND [VarsayilanNakitKasaId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_KantinSatisNoktalari_VarsayilanPosHesapId",
                schema: "kantin",
                table: "KantinSatisNoktalari",
                column: "VarsayilanPosHesapId",
                filter: "[IsDeleted] = 0 AND [VarsayilanPosHesapId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_KantinSatislar_KantinSatisNoktalari_SatisNoktasiId",
                schema: "kantin",
                table: "KantinSatislar",
                column: "SatisNoktasiId",
                principalSchema: "kantin",
                principalTable: "KantinSatisNoktalari",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropForeignKey(
                name: "FK_Kantinler_KasaBankaHesaplari_VarsayilanNakitKasaId",
                schema: "kantin",
                table: "Kantinler");

            migrationBuilder.DropForeignKey(
                name: "FK_Kantinler_KasaBankaHesaplari_VarsayilanPosHesapId",
                schema: "kantin",
                table: "Kantinler");

            migrationBuilder.DropIndex(
                name: "IX_Kantinler_VarsayilanNakitKasaId",
                schema: "kantin",
                table: "Kantinler");

            migrationBuilder.DropIndex(
                name: "IX_Kantinler_VarsayilanPosHesapId",
                schema: "kantin",
                table: "Kantinler");

            migrationBuilder.DropColumn(
                name: "VarsayilanNakitKasaId",
                schema: "kantin",
                table: "Kantinler");

            migrationBuilder.DropColumn(
                name: "VarsayilanPosHesapId",
                schema: "kantin",
                table: "Kantinler");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KantinSatislar_KantinSatisNoktalari_SatisNoktasiId",
                schema: "kantin",
                table: "KantinSatislar");

            migrationBuilder.DropTable(
                name: "KantinSatisNoktalari",
                schema: "kantin");

            migrationBuilder.DropIndex(
                name: "IX_KantinSatislar_SatisNoktasiId",
                schema: "kantin",
                table: "KantinSatislar");

            migrationBuilder.DropColumn(
                name: "SatisNoktasiId",
                schema: "kantin",
                table: "KantinSatislar");

            migrationBuilder.AddColumn<int>(
                name: "VarsayilanNakitKasaId",
                schema: "kantin",
                table: "Kantinler",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VarsayilanPosHesapId",
                schema: "kantin",
                table: "Kantinler",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Kantinler_VarsayilanNakitKasaId",
                schema: "kantin",
                table: "Kantinler",
                column: "VarsayilanNakitKasaId",
                filter: "[IsDeleted] = 0 AND [VarsayilanNakitKasaId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Kantinler_VarsayilanPosHesapId",
                schema: "kantin",
                table: "Kantinler",
                column: "VarsayilanPosHesapId",
                filter: "[IsDeleted] = 0 AND [VarsayilanPosHesapId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Kantinler_KasaBankaHesaplari_VarsayilanNakitKasaId",
                schema: "kantin",
                table: "Kantinler",
                column: "VarsayilanNakitKasaId",
                principalSchema: "muhasebe",
                principalTable: "KasaBankaHesaplari",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Kantinler_KasaBankaHesaplari_VarsayilanPosHesapId",
                schema: "kantin",
                table: "Kantinler",
                column: "VarsayilanPosHesapId",
                principalSchema: "muhasebe",
                principalTable: "KasaBankaHesaplari",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
