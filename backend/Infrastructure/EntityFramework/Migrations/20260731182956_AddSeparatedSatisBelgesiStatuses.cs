using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddSeparatedSatisBelgesiStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FaturalamaDurumu",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MuhasebeDurumu",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TicariDurum",
                schema: "muhasebe",
                table: "SatisBelgeleri",
                type: "int",
                nullable: true);

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

            // Geriye dönük backfill: MEVCUT (BelgeTipi, Durum) çiftinden, SatisBelgesiDurumProjection
            // (C#) ile BİREBİR AYNI eşleme matrisi kullanılarak üç yeni alan doldurulur. WHERE
            // koşulu YOKTUR - hem aktif (IsDeleted=0) hem soft-delete edilmiş (IsDeleted=1) TÜM
            // kayıtlar kapsanır. BelgeTipi ve Durum kolonlarının kendisi HİÇBİR ŞEKİLDE
            // DEĞİŞTİRİLMEZ; yalnızca üç yeni kolon SET edilir. Legacy IadeFaturasi(3) yönü
            // TAHMİN EDİLMEZ - StysTarafindanDuzenlenirMi mantığına göre zaten (BelgeTipi IN (2,7)
            // olmadığından) doğal olarak Uygulanamaz'a düşer, ayrı bir özel durum GEREKMEZ.
            //
            // BelgeTipi: FaturaTaslagi=1, SatisFaturasi=2, IadeFaturasi=3(legacy), Proforma=4,
            // AlisFaturasi=5, SatisIadeFaturasi=6, AlisIadeFaturasi=7.
            // Durum: Taslak=1, MuhasebeOnayinda=2, MuhasebeOnaylandi=3, Reddedildi=4,
            // FaturaKesildi=5, MusteriyeGonderildi=6, IptalEdildi=7.
            migrationBuilder.Sql("""
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
                    END;
                """);
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

            migrationBuilder.DropColumn(
                name: "FaturalamaDurumu",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.DropColumn(
                name: "MuhasebeDurumu",
                schema: "muhasebe",
                table: "SatisBelgeleri");

            migrationBuilder.DropColumn(
                name: "TicariDurum",
                schema: "muhasebe",
                table: "SatisBelgeleri");
        }
    }
}
