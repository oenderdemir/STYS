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
    /// veritabanlarinda tekrar calismayacagi icin) veriyi duzeltir. Duzeltme DETERMINISTIKTIR
    /// (goreli kaydirma DEGIL, mutlak hedef deger atamasi) - bu yuzden ana hesabin SeviyeNo'su
    /// DAHA ONCE elle KISMEN duzeltilmis olsa bile (ornegin ana=3 ama detay hesaplar hala eski
    /// seviyede kalmis olsa bile) detay hesaplar da dogru sekilde onarilir; ayrica zaten dogru
    /// olan kayitlara DOKUNULMAZ (her UPDATE kendi WHERE kosuluyla yalnizca GERCEKTEN yanlis olan
    /// satirlari hedefler). Idempotent: migration ikinci kez calistirilsa sonuc DEGISMEZ.
    /// </summary>
    public partial class FixKrediKartlariAnaHesapHiyerarsisi : Migration
    {
        /// <summary>
        /// Migration'in UYGULADIGI TAM SQL - testlerde (bkz.
        /// tests/STYS.Tests/FixKrediKartlariAnaHesapHiyerarsisiMigrationTests.cs) migration
        /// gecmisini degistirmeden AYNI SQL'i gercek bir SQL Server transaction'i icinde
        /// calistirip dogrulayabilmek icin public olarak disari acilir.
        /// </summary>
        public const string FixSql =
            """
            SET NOCOUNT ON;

            DECLARE @HazirDegerlerId int, @HazirDegerlerSeviye int;
            DECLARE @KrediKartlariId int, @KrediKartlariYeniSeviye int, @DetaySeviye int;

            SELECT TOP (1) @HazirDegerlerId = [Id], @HazirDegerlerSeviye = [SeviyeNo]
            FROM [muhasebe].[MuhasebeHesapPlanlari]
            WHERE [IsDeleted] = 0 AND [TesisId] IS NULL AND [TamKod] = N'1.10'
            ORDER BY [Id];

            SELECT TOP (1) @KrediKartlariId = [Id]
            FROM [muhasebe].[MuhasebeHesapPlanlari]
            WHERE [IsDeleted] = 0 AND [TesisId] IS NULL AND [TamKod] = N'1.10.109'
            ORDER BY [Id];

            IF @HazirDegerlerId IS NOT NULL AND @KrediKartlariId IS NOT NULL
            BEGIN
                IF @HazirDegerlerSeviye IS NULL
                BEGIN
                    THROW 51003, N'1.10 HAZIR DEGERLER ana hesabinin SeviyeNo degeri NULL - migration guvenli sekilde devam edemiyor.', 1;
                END;

                SET @KrediKartlariYeniSeviye = @HazirDegerlerSeviye + 1;
                SET @DetaySeviye = @KrediKartlariYeniSeviye + 1;

                -- "1.10.109"a DOGRUDAN bagli, aktif detay hesaplarin SeviyeNo'sunu DETERMINISTIK
                -- olarak (goreli kaydirma DEGIL, mutlak hedef deger) dogru degere sabitler - boylece
                -- ana hesap DAHA ONCE elle kismen duzeltilmis olsa bile (ana=3 ama detay=5 gibi)
                -- detay hesaplar da onarilir; zaten dogru seviyedeki satirlara DOKUNULMAZ ([SeviyeNo]
                -- <> @DetaySeviye kosulu). Kapsam guvenligi: yalnizca [UstHesapId] = @KrediKartlariId
                -- VE [IsDeleted] = 0 olan satirlar hedeflenir (baska hesap turlerine/tesislere
                -- dokunulmaz); MUMKUN OLDUGUNDA (KasaBankaHesaplari uzerinden dogrulanabiliyorsa)
                -- yalnizca GERCEKTEN Tip=KrediKarti olan bir hesaba bagli VEYA hicbir KasaBankaHesap'a
                -- bagli OLMAYAN (ama yine de UstHesapId ile bu ana hesaba baglanmis) detay hesaplar
                -- guncellenir - bagli oldugu KasaBankaHesap BASKA bir tipteyse (ornegin yanlislikla
                -- buraya yerlestirilmis bir Banka hesabi) bu satir KASITLI olarak ATLANIR.
                UPDATE h
                SET h.[SeviyeNo] = @DetaySeviye
                FROM [muhasebe].[MuhasebeHesapPlanlari] h
                WHERE h.[IsDeleted] = 0
                  AND h.[UstHesapId] = @KrediKartlariId
                  AND h.[SeviyeNo] <> @DetaySeviye
                  AND (
                      EXISTS (
                          SELECT 1 FROM [muhasebe].[KasaBankaHesaplari] k
                          WHERE k.[MuhasebeHesapPlaniId] = h.[Id] AND k.[Tip] = N'KrediKarti' AND k.[IsDeleted] = 0
                      )
                      OR NOT EXISTS (
                          SELECT 1 FROM [muhasebe].[KasaBankaHesaplari] k
                          WHERE k.[MuhasebeHesapPlaniId] = h.[Id] AND k.[IsDeleted] = 0
                      )
                  );

                -- Ana hesabin kendisi: SeviyeNo yanlissa VEYA UstHesapId yanlissa (NULL DAHIL -
                -- "<>" operatoru SQL'de NULL ile karsilastirildiginda UNKNOWN dondurur ve satiri
                -- YANLISLIKLA atlar, bu yuzden IS NULL ayrica kontrol edilir) duzeltilir.
                UPDATE [muhasebe].[MuhasebeHesapPlanlari]
                SET [SeviyeNo] = @KrediKartlariYeniSeviye,
                    [UstHesapId] = @HazirDegerlerId
                WHERE [Id] = @KrediKartlariId
                  AND (
                      [SeviyeNo] <> @KrediKartlariYeniSeviye
                      OR [UstHesapId] IS NULL
                      OR [UstHesapId] <> @HazirDegerlerId
                  );
            END;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FixSql);
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
