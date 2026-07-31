using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class MakeSeparatedSatisBelgesiStatusesAuthoritative : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Adım: NULL kalan (veya AddSeparatedSatisBelgesiStatuses migration'ından SONRA,
            // eski uygulama kodu tarafından oluşturulmuş) satırları AYNI (BelgeTipi + legacy Durum)
            // eşleme matrisiyle yeniden backfill et - WHERE koşulu yalnızca hâlâ NULL olan satırları
            // hedefler, zaten dolu satırlara DOKUNULMAZ. BelgeTipi/Durum kolonlarının kendisi
            // DEĞİŞTİRİLMEZ.
            //
            // BelgeTipi: FaturaTaslagi=1, SatisFaturasi=2, IadeFaturasi=3(legacy), Proforma=4,
            // AlisFaturasi=5, SatisIadeFaturasi=6, AlisIadeFaturasi=7.
            // Durum: Taslak=1, MuhasebeOnayinda=2, MuhasebeOnaylandi=3, Reddedildi=4,
            // FaturaKesildi=5, MusteriyeGonderildi=6, IptalEdildi=7.
            migrationBuilder.Sql("""
                SET NOCOUNT ON;

                UPDATE [muhasebe].[SatisBelgeleri]
                SET
                    [TicariDurum] = CASE [Durum]
                        WHEN 1 THEN 1  -- Taslak -> Taslak
                        WHEN 7 THEN 3  -- IptalEdildi -> IptalEdildi
                        ELSE 2         -- diğer tüm mevcut durumlar -> Hazir
                    END,
                    [MuhasebeDurumu] = CASE [Durum]
                        WHEN 1 THEN 1  -- Taslak -> Bekliyor
                        WHEN 2 THEN 2  -- MuhasebeOnayinda -> Onayda
                        WHEN 3 THEN 3  -- MuhasebeOnaylandi -> Onaylandi
                        WHEN 4 THEN 4  -- Reddedildi -> Reddedildi
                        WHEN 5 THEN 3  -- FaturaKesildi -> Onaylandi
                        WHEN 6 THEN 3  -- MusteriyeGonderildi -> Onaylandi
                        WHEN 7 THEN 5  -- IptalEdildi -> IptalEdildi
                    END,
                    [FaturalamaDurumu] = CASE
                        WHEN [Durum] = 5 THEN 4  -- FaturaKesildi -> Kesildi (öncelik, belge tipinden bağımsız)
                        WHEN [Durum] = 6 THEN 5  -- MusteriyeGonderildi -> MusteriyeGonderildi (öncelik)
                        WHEN [Durum] = 7 THEN 6  -- IptalEdildi -> IptalEdildi (öncelik)
                        WHEN [BelgeTipi] IN (2, 7) AND [Durum] = 3 THEN 3  -- SatisFaturasi/AlisIadeFaturasi + MuhasebeOnaylandi -> KesimBekliyor
                        WHEN [BelgeTipi] IN (2, 7) THEN 2                 -- SatisFaturasi/AlisIadeFaturasi + daha erken durum -> Baslatilmadi
                        ELSE 1                                            -- AlisFaturasi/SatisIadeFaturasi/FaturaTaslagi/Proforma/legacy IadeFaturasi -> Uygulanamaz
                    END
                WHERE [TicariDurum] IS NULL OR [MuhasebeDurumu] IS NULL OR [FaturalamaDurumu] IS NULL;
                """);

            // 2. Adım: Backfill sonrasında HÂLÂ NULL kalan bir durum alanı varsa (ör. Durum
            // sütununda beklenmeyen/tanımsız bir değer bulunması gibi bir veri tutarsızlığı
            // nedeniyle), migration açık bir SQL THROW ile DURDURULUR - kolonlar sessizce (ör.
            // rastgele bir varsayılan değerle) NOT NULL yapılmaz.
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM [muhasebe].[SatisBelgeleri]
                    WHERE [TicariDurum] IS NULL OR [MuhasebeDurumu] IS NULL OR [FaturalamaDurumu] IS NULL
                )
                BEGIN
                    THROW 51000, 'SatisBelgeleri: backfill sonrasi bir veya daha fazla durum alani (TicariDurum/MuhasebeDurumu/FaturalamaDurumu) NULL kaldi; migration durduruldu.', 1;
                END
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_SatisBelgeleri_FaturalamaDurumu",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SatisBelgeleri_MuhasebeDurumu",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SatisBelgeleri_TicariDurum",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.AlterColumn<int>(
                name: "TicariDurum",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "MuhasebeDurumu",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "FaturalamaDurumu",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_SatisBelgeleri_FaturalamaDurumu",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                sql: "[FaturalamaDurumu] IN (1, 2, 3, 4, 5, 6)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SatisBelgeleri_MuhasebeDurumu",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                sql: "[MuhasebeDurumu] IN (1, 2, 3, 4, 5)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SatisBelgeleri_TicariDurum",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                sql: "[TicariDurum] IN (1, 2, 3)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SatisBelgeleri_FaturalamaDurumu",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SatisBelgeleri_MuhasebeDurumu",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SatisBelgeleri_TicariDurum",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.AlterColumn<int>(
                name: "TicariDurum",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "MuhasebeDurumu",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "FaturalamaDurumu",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SatisBelgeleri_FaturalamaDurumu",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                sql: "[FaturalamaDurumu] IS NULL OR [FaturalamaDurumu] IN (1, 2, 3, 4, 5, 6)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SatisBelgeleri_MuhasebeDurumu",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                sql: "[MuhasebeDurumu] IS NULL OR [MuhasebeDurumu] IN (1, 2, 3, 4, 5)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SatisBelgeleri_TicariDurum",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                sql: "[TicariDurum] IS NULL OR [TicariDurum] IN (1, 2, 3)");
        }
    }
}
