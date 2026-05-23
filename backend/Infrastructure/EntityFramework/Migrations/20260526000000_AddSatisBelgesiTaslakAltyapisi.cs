using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260526000000_AddSatisBelgesiTaslakAltyapisi")]
public partial class AddSatisBelgesiTaslakAltyapisi : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        const string muhasebeSchema = "muhasebe";

        // SatisBelgeleri
        migrationBuilder.CreateTable(
            name: "SatisBelgeleri",
            schema: muhasebeSchema,
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                BelgeNo = table.Column<string>(maxLength: 50, nullable: false),
                BelgeTipi = table.Column<int>(nullable: false, defaultValue: 1),
                Durum = table.Column<int>(nullable: false, defaultValue: 1),
                KaynakModul = table.Column<int>(nullable: false, defaultValue: 1),
                KaynakTipi = table.Column<string>(maxLength: 100, nullable: true),
                KaynakId = table.Column<string>(maxLength: 100, nullable: true),
                TesisId = table.Column<int>(nullable: true),
                BelgeTarihi = table.Column<DateTime>(nullable: false),
                VadeTarihi = table.Column<DateTime>(nullable: true),
                MusteriUnvan = table.Column<string>(maxLength: 250, nullable: true),
                MusteriAdSoyad = table.Column<string>(maxLength: 250, nullable: true),
                MusteriVergiNo = table.Column<string>(maxLength: 20, nullable: true),
                MusteriTcKimlikNo = table.Column<string>(maxLength: 20, nullable: true),
                MusteriVergiDairesi = table.Column<string>(maxLength: 100, nullable: true),
                MusteriAdres = table.Column<string>(maxLength: 500, nullable: true),
                MusteriEposta = table.Column<string>(maxLength: 150, nullable: true),
                MusteriTelefon = table.Column<string>(maxLength: 50, nullable: true),
                KurumsalMi = table.Column<bool>(nullable: false),
                ToplamMatrah = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                ToplamKdv = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                GenelToplam = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                Aciklama = table.Column<string>(maxLength: 1000, nullable: true),
                RedNedeni = table.Column<string>(maxLength: 1000, nullable: true),
                ResmiFaturaNo = table.Column<string>(maxLength: 50, nullable: true),
                EBelgeUuid = table.Column<string>(maxLength: 100, nullable: true),
                MuhasebeOnayinaGonderilmeTarihi = table.Column<DateTime>(nullable: true),
                MuhasebeOnayTarihi = table.Column<DateTime>(nullable: true),
                FaturaKesimTarihi = table.Column<DateTime>(nullable: true),
                MusteriyeGonderimTarihi = table.Column<DateTime>(nullable: true),
                CreatedAt = table.Column<DateTime>(nullable: false),
                CreatedBy = table.Column<string>(maxLength: 128, nullable: false),
                UpdatedAt = table.Column<DateTime>(nullable: false),
                UpdatedBy = table.Column<string>(maxLength: 128, nullable: false),
                IsDeleted = table.Column<bool>(nullable: false, defaultValue: false),
                DeletedAt = table.Column<DateTime>(nullable: true),
                DeletedBy = table.Column<string>(maxLength: 128, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SatisBelgeleri", x => x.Id);
            });

        // SatisBelgesiSatirlari
        migrationBuilder.CreateTable(
            name: "SatisBelgesiSatirlari",
            schema: muhasebeSchema,
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                SatisBelgesiId = table.Column<int>(nullable: false),
                SiraNo = table.Column<int>(nullable: false),
                SatirTipi = table.Column<int>(nullable: false, defaultValue: 99),
                Aciklama = table.Column<string>(maxLength: 500, nullable: false),
                Miktar = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                BirimFiyat = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                Matrah = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                KdvUygulamaTipi = table.Column<int>(nullable: false, defaultValue: 1),
                KdvIstisnaTanimId = table.Column<int>(nullable: true),
                KdvIstisnaKodu = table.Column<string>(maxLength: 50, nullable: true),
                KdvIstisnaAciklamasi = table.Column<string>(maxLength: 1000, nullable: true),
                KdvOrani = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                KdvTutari = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                SatirToplami = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                KaynakSatirId = table.Column<string>(maxLength: 100, nullable: true),
                CreatedAt = table.Column<DateTime>(nullable: false),
                CreatedBy = table.Column<string>(maxLength: 128, nullable: false),
                UpdatedAt = table.Column<DateTime>(nullable: false),
                UpdatedBy = table.Column<string>(maxLength: 128, nullable: false),
                IsDeleted = table.Column<bool>(nullable: false, defaultValue: false),
                DeletedAt = table.Column<DateTime>(nullable: true),
                DeletedBy = table.Column<string>(maxLength: 128, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SatisBelgesiSatirlari", x => x.Id);
                table.ForeignKey(
                    name: "FK_SatisBelgesiSatirlari_SatisBelgeleri_SatisBelgesiId",
                    column: x => x.SatisBelgesiId,
                    principalSchema: muhasebeSchema,
                    principalTable: "SatisBelgeleri",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_SatisBelgesiSatirlari_KdvIstisnaTanimlari_KdvIstisnaTanimId",
                    column: x => x.KdvIstisnaTanimId,
                    principalSchema: muhasebeSchema,
                    principalTable: "KdvIstisnaTanimlari",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        // Indexes
        migrationBuilder.CreateIndex(
            name: "IX_SatisBelgeleri_BelgeNo",
            schema: muhasebeSchema,
            table: "SatisBelgeleri",
            column: "BelgeNo",
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_SatisBelgeleri_KaynakModul_KaynakTipi_KaynakId",
            schema: muhasebeSchema,
            table: "SatisBelgeleri",
            columns: new[] { "KaynakModul", "KaynakTipi", "KaynakId" },
            unique: true,
            filter: "[IsDeleted] = 0 AND [KaynakId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_SatisBelgeleri_TesisId_BelgeTarihi",
            schema: muhasebeSchema,
            table: "SatisBelgeleri",
            columns: new[] { "TesisId", "BelgeTarihi" });

        migrationBuilder.CreateIndex(
            name: "IX_SatisBelgeleri_Durum",
            schema: muhasebeSchema,
            table: "SatisBelgeleri",
            column: "Durum");

        migrationBuilder.CreateIndex(
            name: "IX_SatisBelgeleri_KaynakModul",
            schema: muhasebeSchema,
            table: "SatisBelgeleri",
            column: "KaynakModul");

        migrationBuilder.CreateIndex(
            name: "IX_SatisBelgesiSatirlari_SatisBelgesiId",
            schema: muhasebeSchema,
            table: "SatisBelgesiSatirlari",
            column: "SatisBelgesiId");

        migrationBuilder.CreateIndex(
            name: "IX_SatisBelgesiSatirlari_KdvIstisnaTanimId",
            schema: muhasebeSchema,
            table: "SatisBelgesiSatirlari",
            column: "KdvIstisnaTanimId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        const string muhasebeSchema = "muhasebe";

        migrationBuilder.DropTable(
            name: "SatisBelgesiSatirlari",
            schema: muhasebeSchema);

        migrationBuilder.DropTable(
            name: "SatisBelgeleri",
            schema: muhasebeSchema);
    }
}
