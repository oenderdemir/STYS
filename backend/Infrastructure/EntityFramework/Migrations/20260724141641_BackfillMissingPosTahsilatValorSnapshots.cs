using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <summary>
    /// RezervasyonOdemeMuhasebeService.TahsilatOlusturAsync, TahsilatOdemeBelgesi'ni
    /// TahsilatOdemeBelgesiService.CreateAsync UZERINDEN degil dogrudan DbContext ile
    /// olusturuyordu; bu yuzden CreateAsync'in cagirdigi IPosTahsilatValorSnapshotService.
    /// OlusturSnapshotAsync hic tetiklenmiyor, rezervasyon odeme ekranindan yapilan kredi karti
    /// tahsilatlari icin PosTahsilatValorleri hic kayit URETILMIYORDU (bkz. duzeltme commit'i:
    /// RezervasyonOdemeMuhasebeService'e ayni cagrinin eklenmesi). Bu migration, o duzeltmeden
    /// ONCE olusturulmus, kredi karti ile odenmis ama HICBIR valor kaydi olmayan mevcut
    /// TahsilatOdemeBelgesi satirlarini tespit edip PosTahsilatValorSnapshotService.
    /// OlusturSnapshotAsync ile TAM OLARAK AYNI mantikla (ayni valor tarihi hesaplama - takvim
    /// gunu/is gunu -, ayni komisyon/net hesaplama, ayni Durum secimi) geriye donuk olusturur.
    ///
    /// KAPSAM (guvenli, dar hedefleme): yalnizca
    ///   - TahsilatOdemeBelgesi.Durum = 'Aktif' (iptal edilmis odemeler ATLANIR - iptal edilmis
    ///     bir odemeyi simdi "ValorBekliyor" olarak canlandirmak yanlis olurdu),
    ///   - OdemeYontemi = 'KrediKarti',
    ///   - bagli KasaBankaHesap GERCEKTEN Tip='KrediKarti' ve soft-delete edilmemis,
    ///   - HENUZ hicbir (soft-delete edilmemis) PosTahsilatValorleri kaydi YOK (unique filtered
    ///     index IX_PosTahsilatValorleri_TahsilatOdemeBelgesiId ile ayni kosul) olan satirlar
    /// hedeflenir. Idempotent: migration ikinci kez calistirilsa (ornegin manuel olarak tekrar
    /// uygulanirsa) NOT EXISTS kosulu nedeniyle hicbir yeni satir uretilmez.
    ///
    /// Valor tarihi hesaplama, ValorTarihHesaplamaService.HesaplaValorTarihi ile BIREBIR ayni
    /// kurali izler: TakvimGunu icin dogrudan DATEADD; IsGunu icin gun gun ilerleyip hafta
    /// sonlarini (Cumartesi/Pazar) atlar - projede su an bir resmi tatil takvimi olmadigi icin
    /// (bkz. NoOpResmiTatilGunuProvider) yalnizca hafta sonu kontrolu yeterlidir. Hafta günü
    /// hesabi DATEFIRST/dil ayarindan ETKILENMEYEN bir yontemle yapilir: 1900-01-01 (gun 0) bilinen
    /// bir Pazartesi oldugu icin DATEDIFF(DAY, '19000101', tarih) % 7 degeri 5/6 ise (Cumartesi/
    /// Pazar) atlanir - DATEPART(WEEKDAY,...) KULLANILMAZ (oturum/sunucu dil ayarina bagli olurdu).
    /// </summary>
    public partial class BackfillMissingPosTahsilatValorSnapshots : Migration
    {
        /// <summary>
        /// Migration'in UYGULADIGI TAM SQL - testlerde (bkz.
        /// tests/STYS.Tests/BackfillMissingPosTahsilatValorSnapshotsMigrationTests.cs) migration
        /// gecmisini degistirmeden AYNI SQL'i gercek bir SQL Server transaction'i icinde
        /// calistirip dogrulayabilmek icin public olarak disari acilir.
        /// </summary>
        public const string BackfillSql =
            """
            SET NOCOUNT ON;

            DECLARE @Hedefler TABLE (
                BelgeId int PRIMARY KEY,
                TesisId int NOT NULL,
                BelgeTarihi date NOT NULL,
                Tutar decimal(18,2) NOT NULL,
                ParaBirimi nvarchar(3) NOT NULL,
                KrediKartiHesapId int NOT NULL,
                BagliBankaHesapId int NULL,
                KomisyonGiderHesapPlaniId int NULL,
                ValorGunSayisi int NOT NULL,
                ValorGunTuru nvarchar(16) NOT NULL,
                OtomatikAktarimMi bit NOT NULL,
                KomisyonOrani decimal(5,2) NULL
            );

            INSERT INTO @Hedefler (BelgeId, TesisId, BelgeTarihi, Tutar, ParaBirimi, KrediKartiHesapId,
                BagliBankaHesapId, KomisyonGiderHesapPlaniId, ValorGunSayisi, ValorGunTuru,
                OtomatikAktarimMi, KomisyonOrani)
            SELECT b.[Id], k.[TesisId], CAST(b.[BelgeTarihi] AS date), b.[Tutar], b.[ParaBirimi], k.[Id],
                k.[BagliBankaHesapId], k.[KomisyonGiderHesapPlaniId], k.[ValorGunSayisi], k.[ValorGunTuru],
                k.[ValorGunundeOtomatikHesabaAktarMi], k.[KomisyonOrani]
            FROM [muhasebe].[TahsilatOdemeBelgeleri] b
            INNER JOIN [muhasebe].[KasaBankaHesaplari] k ON k.[Id] = b.[KasaBankaHesapId] AND k.[IsDeleted] = 0
            WHERE b.[IsDeleted] = 0
              AND b.[Durum] = N'Aktif'
              AND b.[OdemeYontemi] = N'KrediKarti'
              AND k.[Tip] = N'KrediKarti'
              AND k.[TesisId] IS NOT NULL
              AND NOT EXISTS (
                  SELECT 1 FROM [muhasebe].[PosTahsilatValorleri] v
                  WHERE v.[TahsilatOdemeBelgesiId] = b.[Id] AND v.[IsDeleted] = 0
              );

            DECLARE @BelgeId int, @TesisId int, @BelgeTarihi date, @Tutar decimal(18,2), @ParaBirimi nvarchar(3),
                    @KrediKartiHesapId int, @BagliBankaHesapId int, @KomisyonGiderHesapPlaniId int,
                    @ValorGunSayisi int, @ValorGunTuru nvarchar(16), @OtomatikAktarimMi bit,
                    @KomisyonOrani decimal(5,2);

            DECLARE hedef_cursor CURSOR LOCAL FAST_FORWARD FOR
            SELECT BelgeId, TesisId, BelgeTarihi, Tutar, ParaBirimi, KrediKartiHesapId, BagliBankaHesapId,
                   KomisyonGiderHesapPlaniId, ValorGunSayisi, ValorGunTuru, OtomatikAktarimMi, KomisyonOrani
            FROM @Hedefler;

            OPEN hedef_cursor;
            FETCH NEXT FROM hedef_cursor INTO @BelgeId, @TesisId, @BelgeTarihi, @Tutar, @ParaBirimi,
                @KrediKartiHesapId, @BagliBankaHesapId, @KomisyonGiderHesapPlaniId, @ValorGunSayisi,
                @ValorGunTuru, @OtomatikAktarimMi, @KomisyonOrani;

            WHILE @@FETCH_STATUS = 0
            BEGIN
                DECLARE @ValorTarihi date = @BelgeTarihi;

                IF @ValorGunTuru = N'IsGunu'
                BEGIN
                    DECLARE @Kalan int = @ValorGunSayisi;
                    WHILE @Kalan > 0
                    BEGIN
                        SET @ValorTarihi = DATEADD(DAY, 1, @ValorTarihi);
                        -- DATEDIFF(DAY, '19000101', x) % 7: 1900-01-01 bilinen bir Pazartesi
                        -- oldugu icin 0=Pzt..4=Cuma, 5=Cmt, 6=Paz - DATEFIRST/dil ayarindan
                        -- BAGIMSIZ, DATEPART(WEEKDAY,...) YERINE bilerek bu kullanilir.
                        IF (DATEDIFF(DAY, '19000101', @ValorTarihi) % 7) NOT IN (5, 6)
                        BEGIN
                            SET @Kalan = @Kalan - 1;
                        END
                    END
                END
                ELSE
                BEGIN
                    SET @ValorTarihi = DATEADD(DAY, @ValorGunSayisi, @BelgeTarihi);
                END

                DECLARE @Komisyon decimal(18,2);
                DECLARE @Net decimal(18,2);
                DECLARE @Durum nvarchar(24);

                IF @KomisyonOrani IS NOT NULL
                BEGIN
                    -- ROUND(...,2) SQL Server'da varsayilan olarak "yariyi sifirdan uzaklastirarak"
                    -- yuvarlar - ParaTutarYuvarlamaHelper.Yuvarla (Math.Round ile
                    -- MidpointRounding.AwayFromZero) ile BIREBIR ayni davranis.
                    SET @Komisyon = ROUND(@Tutar * @KomisyonOrani / 100.0, 2);
                    SET @Net = @Tutar - @Komisyon;
                    SET @Durum = N'ValorBekliyor';
                END
                ELSE
                BEGIN
                    SET @Komisyon = 0;
                    SET @Net = @Tutar;
                    SET @Durum = CASE WHEN @OtomatikAktarimMi = 1 THEN N'MutabakatBekliyor' ELSE N'ValorBekliyor' END;
                END

                INSERT INTO [muhasebe].[PosTahsilatValorleri] (
                    [TesisId], [TahsilatOdemeBelgesiId], [KrediKartiHesapId], [BagliBankaHesapId],
                    [KomisyonGiderHesapPlaniId], [OdemeTarihi], [ValorGunSayisi], [ValorGunTuru],
                    [BeklenenValorTarihi], [OtomatikAktarimMi], [KomisyonOraniSnapshot], [BrutTutar],
                    [KomisyonTutari], [NetTutar], [ParaBirimi], [Durum], [DenemeSayisi], [Aciklama],
                    [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
                )
                VALUES (
                    @TesisId, @BelgeId, @KrediKartiHesapId, @BagliBankaHesapId,
                    @KomisyonGiderHesapPlaniId, @BelgeTarihi, @ValorGunSayisi, @ValorGunTuru,
                    @ValorTarihi, @OtomatikAktarimMi, @KomisyonOrani, @Tutar,
                    @Komisyon, @Net, @ParaBirimi, @Durum, 0,
                    N'Otomatik backfill: bu odeme icin valor kaydi olusturma adimi eksikti (bkz. migration BackfillMissingPosTahsilatValorSnapshots).',
                    0, SYSUTCDATETIME(), SYSUTCDATETIME(), N'system-backfill', N'system-backfill'
                );

                FETCH NEXT FROM hedef_cursor INTO @BelgeId, @TesisId, @BelgeTarihi, @Tutar, @ParaBirimi,
                    @KrediKartiHesapId, @BagliBankaHesapId, @KomisyonGiderHesapPlaniId, @ValorGunSayisi,
                    @ValorGunTuru, @OtomatikAktarimMi, @KomisyonOrani;
            END

            CLOSE hedef_cursor;
            DEALLOCATE hedef_cursor;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(BackfillSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Kasitli olarak no-op: bu migration yalnizca EKSIK olan veriyi TAMAMLAR (yeni bir
            // yapi/sema olusturmaz). Geri almak, gercek (ve artik dogru sekilde takip edilen) bir
            // kredi karti tahsilatinin valor kaydini KASITLI olarak yeniden SILMEK anlamina
            // gelirdi - bu asla istenen bir "geri alma" degildir.
        }
    }
}
