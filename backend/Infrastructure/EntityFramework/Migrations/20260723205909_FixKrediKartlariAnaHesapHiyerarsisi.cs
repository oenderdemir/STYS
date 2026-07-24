using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <summary>
    /// 20260428201154_ExpandFinancialAccountsModel migrationi, "1.10.109 KREDI KARTLARI" ana
    /// hesabini YANLIS SeviyeNo (4, olmasi gereken 3 yerine) ve YANLIS UstHesapId (1.10.102
    /// BANKALAR'in Id'si, olmasi gereken 1.10 HAZIR DEGERLER'in Id'si yerine) ile seed etmisti.
    /// MuhasebeHesapPlaniService.GetTreeChildrenAsync, agac dugumlerini TamKod prefix + SeviyeNo
    /// eslesmesiyle bulur (UstHesapId'yi KULLANMAZ) - bu yuzden "1.10.109" hem "Hazir Degerler"
    /// altinda (SeviyeNo uyusmuyor) HEM DE "Bankalar" altinda (TamKod prefix uyusmuyor) HICBIR
    /// agac genislemesiyle GORUNTULENEMIYORDU. MuhasebeDetayHesapService.CreateDetayHesapAsync
    /// yeni detay hesaplarin SeviyeNo'sunu `anaHesap.SeviyeNo + 1` olarak hesapladigi icin, bu hatali
    /// ana hesap altinda olusturulan HER kredi karti detay hesabi (KasaBankaHesap.Tip=KrediKarti)
    /// da AYNI sekilde gorunmez kaliyordu.
    ///
    /// Bu migration, 20260428201154 migrationini GERIYE DONUK degistirmeden (zaten uygulanmis
    /// veritabanlarinda tekrar calismayacagi icin) veriyi duzeltir: "1.10.109" ana hesabinin
    /// SeviyeNo/UstHesapId'sini "1.10" (HAZIR DEGERLER) referans alarak dogru degerlere getirir,
    /// ardindan altindaki TUM detay hesaplarin (TamKod "1.10.109." ile baslayanlar) SeviyeNo'sunu
    /// bir azaltarak yeni (dogru) ana hesap seviyesine gore senkronlar. Idempotent: "1.10.109"
    /// zaten dogru SeviyeNo/UstHesapId'ye sahipse (veya hic yoksa) hicbir satiri etkilemez.
    /// </summary>
    public partial class FixKrediKartlariAnaHesapHiyerarsisi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                SET NOCOUNT ON;

                DECLARE @HazirDegerlerId int, @HazirDegerlerSeviye int;
                DECLARE @KrediKartlariId int, @KrediKartlariEskiSeviye int, @KrediKartlariYeniSeviye int;

                SELECT TOP (1) @HazirDegerlerId = [Id], @HazirDegerlerSeviye = [SeviyeNo]
                FROM [muhasebe].[MuhasebeHesapPlanlari]
                WHERE [IsDeleted] = 0 AND [TesisId] IS NULL AND [TamKod] = N'1.10'
                ORDER BY [Id];

                SELECT TOP (1) @KrediKartlariId = [Id], @KrediKartlariEskiSeviye = [SeviyeNo]
                FROM [muhasebe].[MuhasebeHesapPlanlari]
                WHERE [IsDeleted] = 0 AND [TesisId] IS NULL AND [TamKod] = N'1.10.109'
                ORDER BY [Id];

                IF @HazirDegerlerId IS NOT NULL AND @KrediKartlariId IS NOT NULL
                BEGIN
                    SET @KrediKartlariYeniSeviye = @HazirDegerlerSeviye + 1;

                    IF @KrediKartlariEskiSeviye <> @KrediKartlariYeniSeviye
                    BEGIN
                        -- Ana hesabin altindaki (varsa) detay hesaplarin SeviyeNo'sunu, ana hesabin
                        -- KENDI duzeltmesinden ONCE, eski-yeni seviye farkina gore kaydir.
                        UPDATE [muhasebe].[MuhasebeHesapPlanlari]
                        SET [SeviyeNo] = [SeviyeNo] - (@KrediKartlariEskiSeviye - @KrediKartlariYeniSeviye)
                        WHERE [IsDeleted] = 0
                          AND [TamKod] LIKE N'1.10.109.%'
                          AND [UstHesapId] = @KrediKartlariId;
                    END;

                    UPDATE [muhasebe].[MuhasebeHesapPlanlari]
                    SET [SeviyeNo] = @KrediKartlariYeniSeviye,
                        [UstHesapId] = @HazirDegerlerId
                    WHERE [Id] = @KrediKartlariId
                      AND ([SeviyeNo] <> @KrediKartlariYeniSeviye OR [UstHesapId] <> @HazirDegerlerId);
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Kasitli olarak no-op: bu migration yalnizca hatali seed verisini onarir (veri onarimi),
            // yeni bir sema/yapi olusturmaz - geri alinacak bir "yapi" yoktur. Duzeltmeyi "geri almak"
            // hatali durumu KASITLI olarak yeniden uretmek anlamina gelirdi.
        }
    }
}
