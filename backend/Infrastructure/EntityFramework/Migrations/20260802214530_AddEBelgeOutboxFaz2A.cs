using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddEBelgeOutboxFaz2A : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EBelgeOutboxMesajlari",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KurumId = table.Column<int>(type: "int", nullable: false),
                    EBelgeKaydiId = table.Column<int>(type: "int", nullable: false),
                    IsTuru = table.Column<int>(type: "int", nullable: false),
                    Durum = table.Column<int>(type: "int", nullable: false),
                    DenemeSayisi = table.Column<int>(type: "int", nullable: false),
                    SonrakiDenemeZamaniUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    KilitToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    KilitBitisZamaniUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IslemBaslamaZamaniUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TamamlanmaZamaniUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SonHataKodu = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SonHataMesaji = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_EBelgeOutboxMesajlari", x => x.Id);
                    table.CheckConstraint("CK_EBelgeOutboxMesajlari_DenemeSayisi", "[DenemeSayisi] >= 0");
                    table.CheckConstraint("CK_EBelgeOutboxMesajlari_Durum", "[Durum] IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_EBelgeOutboxMesajlari_IsTuru", "[IsTuru] IN (1)");
                    table.ForeignKey(
                        name: "FK_EBelgeOutboxMesajlari_EBelgeKayitlari_EBelgeKaydiId_KurumId",
                        columns: x => new { x.EBelgeKaydiId, x.KurumId },
                        principalSchema: "muhasebe",
                        principalTable: "EBelgeKayitlari",
                        principalColumns: new[] { "Id", "KurumId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EBelgeOutboxMesajlari_EBelgeKaydiId_IsTuru",
                schema: "muhasebe",
                table: "EBelgeOutboxMesajlari",
                columns: new[] { "EBelgeKaydiId", "IsTuru" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EBelgeOutboxMesajlari_EBelgeKaydiId_KurumId",
                schema: "muhasebe",
                table: "EBelgeOutboxMesajlari",
                columns: new[] { "EBelgeKaydiId", "KurumId" });

            migrationBuilder.CreateIndex(
                name: "IX_EBelgeOutboxMesajlari_Durum_SonrakiDenemeZamaniUtc_KilitBitisZamaniUtc",
                schema: "muhasebe",
                table: "EBelgeOutboxMesajlari",
                columns: new[] { "Durum", "SonrakiDenemeZamaniUtc", "KilitBitisZamaniUtc" });

            migrationBuilder.Sql("""
INSERT INTO [muhasebe].[EBelgeOutboxMesajlari]
    ([KurumId],
     [EBelgeKaydiId],
     [IsTuru],
     [Durum],
     [DenemeSayisi],
     [SonrakiDenemeZamaniUtc],
     [KilitToken],
     [KilitBitisZamaniUtc],
     [IslemBaslamaZamaniUtc],
     [TamamlanmaZamaniUtc],
     [SonHataKodu],
     [SonHataMesaji],
     [IsDeleted],
     [CreatedAt],
     [UpdatedAt],
     [DeletedAt],
     [CreatedBy],
     [UpdatedBy],
     [DeletedBy])
SELECT
    kayit.[KurumId],
    kayit.[Id],
    1,
    1,
    0,
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    0,
    SYSUTCDATETIME(),
    NULL,
    NULL,
    N'migration:20260802214530_AddEBelgeOutboxFaz2A',
    NULL,
    NULL
FROM [muhasebe].[EBelgeKayitlari] AS kayit
WHERE kayit.[IsDeleted] = 0
  AND NOT EXISTS (
      SELECT 1
      FROM [muhasebe].[EBelgeOutboxMesajlari] AS mevcut
      WHERE mevcut.[EBelgeKaydiId] = kayit.[Id]
        AND mevcut.[IsTuru] = 1
  );
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EBelgeOutboxMesajlari",
                schema: "muhasebe");
        }
    }
}
