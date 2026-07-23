using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// PosValorFisNoSayaclari (AddPosValorFisNoSayaci migrationinda olusturuldu) tablosunu MEVCUT
    /// POS valor transfer fislerinden geriye donuk doldurur/duzeltir. Onceden uygulanmis
    /// AddPosValorFisNoSayaci migrationi GERIYE DONUK DEGISTIRILMEZ (zaten calistirilmis
    /// veritabanlarinda tekrar Up() calismaz) - bu yuzden eksik/geri kalmis sayaclar AYRI, yeni bir
    /// migration ile giderilir. Iki adim (idempotent, tekrar tekrar calistirilabilir):
    ///   1) Bu tesis/mali yil icin HIC sayac satiri yoksa, MuhasebeFisler'deki gercek en buyuk
    ///      "{MaliYil}-VLR-NNNNNN" numarasindan bir satir olusturur.
    ///   2) Var olan bir sayac satirinin SonNumara'si gercek maksimumdan KUCUKSE yukseltir - ASLA
    ///      AZALTMAZ (baska bir eszamanli islemin zaten ilerlettigi bir sayaci geriye almaz).
    /// </summary>
    public partial class BackfillPosValorFisNoSayaclari : Migration
    {
        private const string GercekMaksimumCte = """
            WITH GercekMaks AS (
                SELECT
                    f.[TesisId],
                    f.[MaliYil],
                    MAX(TRY_CAST(RIGHT(f.[FisNo], 6) AS int)) AS GercekMaksNumara
                FROM [muhasebe].[MuhasebeFisler] f
                WHERE f.[KaynakModul] = N'PosTahsilatValorTransferi'
                  AND f.[IsDeleted] = 0
                  AND f.[FisNo] LIKE '%-VLR-[0-9][0-9][0-9][0-9][0-9][0-9]'
                GROUP BY f.[TesisId], f.[MaliYil]
                HAVING MAX(TRY_CAST(RIGHT(f.[FisNo], 6) AS int)) IS NOT NULL
            )
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                SET NOCOUNT ON;

                {GercekMaksimumCte}
                -- 1) Eksik sayaclari olustur (bu tesis/yil icin hic sayac satiri yoksa).
                INSERT INTO [muhasebe].[PosValorFisNoSayaclari]
                    ([TesisId], [MaliYil], [SonNumara], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                SELECT
                    gm.[TesisId], gm.[MaliYil], gm.[GercekMaksNumara], 0, SYSUTCDATETIME(), SYSUTCDATETIME(),
                    N'migration_backfill_pos_valor_fis_no_sayaci', N'migration_backfill_pos_valor_fis_no_sayaci'
                FROM GercekMaks gm
                WHERE NOT EXISTS (
                    SELECT 1 FROM [muhasebe].[PosValorFisNoSayaclari] s
                    WHERE s.[TesisId] = gm.[TesisId] AND s.[MaliYil] = gm.[MaliYil] AND s.[IsDeleted] = 0
                );
                """);

            migrationBuilder.Sql($"""
                SET NOCOUNT ON;

                {GercekMaksimumCte}
                -- 2) Var olan ama gercek veriden GERI KALMIS sayaclari yukselt - ASLA AZALTMA.
                UPDATE s
                SET s.[SonNumara] = gm.[GercekMaksNumara],
                    s.[UpdatedAt] = SYSUTCDATETIME(),
                    s.[UpdatedBy] = N'migration_backfill_pos_valor_fis_no_sayaci'
                FROM [muhasebe].[PosValorFisNoSayaclari] s
                INNER JOIN GercekMaks gm ON gm.[TesisId] = s.[TesisId] AND gm.[MaliYil] = s.[MaliYil]
                WHERE s.[IsDeleted] = 0 AND s.[SonNumara] < gm.[GercekMaksNumara];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Kasitli olarak no-op: bu migration yalnizca MEVCUT sayac degerlerini duzeltir/tamamlar
            // (veri onarimi), yeni bir sema/yapi olusturmaz - geri alinacak bir "yapi" yoktur. Sayaci
            // migration-oncesi degerine "geri almak" da guvenli degildir (baska islemler bu arada
            // ilerletmis olabilir).
        }
    }
}
