using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(StysAppDbContext))]
    [Migration("20260429200000_SeedStandartDepolariTumTesislere")]
    public partial class SeedStandartDepolariTumTesislere : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
SET NOCOUNT ON;

DECLARE @SeedMarker nvarchar(64) = N'Seed: EskiDepoAktarimi';
DECLARE @DepoAnaHesapKodu nvarchar(64) = N'1.15.150';

DECLARE @AnaHesapId int;
DECLARE @AnaSeviyeNo int;

SELECT TOP (1)
    @AnaHesapId = mhp.Id,
    @AnaSeviyeNo = mhp.SeviyeNo
FROM [muhasebe].[MuhasebeHesapPlanlari] mhp
WHERE mhp.IsDeleted = 0
  AND mhp.TesisId IS NULL
  AND mhp.Kod = @DepoAnaHesapKodu;

IF @AnaHesapId IS NULL
BEGIN
    THROW 50001, N'Depo ana hesabı 1.15.150 bulunamadı. Depo seed işlemi yapılamaz.', 1;
END;

DECLARE @SeedDepolar TABLE
(
    Sira int NOT NULL,
    EskiId int NOT NULL,
    EskiAnaDepoId int NOT NULL,
    Ad nvarchar(200) NOT NULL,
    EskiMuhasebeKodu nvarchar(64) NOT NULL,
    CikisMuhasebeKodu nvarchar(64) NULL
);

INSERT INTO @SeedDepolar (Sira, EskiId, EskiAnaDepoId, Ad, EskiMuhasebeKodu, CikisMuhasebeKodu)
VALUES
(1, 4, 5, N'YEMEKHANE', N'630.01.01', NULL),
(3, 6, 5, N'ÇAY OCAĞI', N'630.02.02', NULL),
(4, 7, 5, N'OTOMAT KAPALI', N'630.02.07', NULL),
(2, 8, 5, N'KAFETERYA', N'630.02.01', NULL),
(5, 9, 5, N'ERKEK KUAFÖRÜ', N'630.02.03', NULL),
(6, 10, 5, N'BAYAN KUAFÖRÜ', N'630.02.04', NULL),
(7, 11, 5, N'SPOR TESİSİ', N'630.02.05', NULL),
(8, 12, 5, N'TEMSİL AĞIRLAMA', N'630.03.01', NULL),
(10, 14, 5, N'OTO YIKAMA', N'630.02.08', NULL),
(11, 15, 5, N'EĞİTİM TESİSİ KAFETERYA', N'630.07.02', NULL),
(12, 16, 5, N'OTO ŞARJ', N'630.05.01', NULL),
(13, 17, 5, N'EĞİTİM TESİSİ ORGANİZASYON', N'630.07.01', NULL),
(14, 18, 5, N'SOSYAL TESİS 3', N'630.08.01', NULL);

DECLARE @AktifTesisler TABLE (TesisId int NOT NULL PRIMARY KEY);
INSERT INTO @AktifTesisler (TesisId)
SELECT t.Id
FROM [dbo].[Tesisler] t
WHERE t.IsDeleted = 0
  AND t.AktifMi = 1;

DECLARE @TesisId int;
DECLARE @Sira int;
DECLARE @EskiId int;
DECLARE @EskiAnaDepoId int;
DECLARE @Ad nvarchar(200);
DECLARE @EskiMuhasebeKodu nvarchar(64);
DECLARE @CikisMuhasebeKodu nvarchar(64);

DECLARE @DepoId int;
DECLARE @Kod nvarchar(64);
DECLARE @MuhasebeHesapPlaniId int;
DECLARE @YeniSiraNo int;
DECLARE @Aciklama nvarchar(1024);
DECLARE @MhpAciklama nvarchar(1024);

DECLARE tesis_cursor CURSOR LOCAL FAST_FORWARD FOR
SELECT TesisId FROM @AktifTesisler ORDER BY TesisId;

OPEN tesis_cursor;
FETCH NEXT FROM tesis_cursor INTO @TesisId;

WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE depo_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT Sira, EskiId, EskiAnaDepoId, Ad, EskiMuhasebeKodu, CikisMuhasebeKodu
    FROM @SeedDepolar
    ORDER BY Sira, EskiId;

    OPEN depo_cursor;
    FETCH NEXT FROM depo_cursor INTO @Sira, @EskiId, @EskiAnaDepoId, @Ad, @EskiMuhasebeKodu, @CikisMuhasebeKodu;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @DepoId = NULL;
        SET @MuhasebeHesapPlaniId = NULL;

        SELECT TOP (1)
            @DepoId = d.Id,
            @MuhasebeHesapPlaniId = d.MuhasebeHesapPlaniId
        FROM [muhasebe].[Depolar] d
        WHERE d.IsDeleted = 0
          AND d.TesisId = @TesisId
          AND d.Ad = @Ad;

        IF @DepoId IS NULL
        BEGIN
            SET @YeniSiraNo = NULL;
            SELECT TOP (1) @YeniSiraNo = s.SonSiraNo
            FROM [muhasebe].[MuhasebeHesapKoduSayaclari] s
            WHERE s.IsDeleted = 0
              AND s.TesisId = @TesisId
              AND s.AnaHesapKodu = @DepoAnaHesapKodu;

            IF @YeniSiraNo IS NULL
            BEGIN
                INSERT INTO [muhasebe].[MuhasebeHesapKoduSayaclari]
                (
                    [TesisId], [AnaHesapKodu], [SonSiraNo], [Aciklama], [IsDeleted], [CreatedAt]
                )
                VALUES
                (
                    @TesisId, @DepoAnaHesapKodu, 0, N'Seed: EskiDepoAktarimi', 0, SYSUTCDATETIME()
                );

                SET @YeniSiraNo = 0;
            END;

            SET @YeniSiraNo = @YeniSiraNo + 1;

            UPDATE [muhasebe].[MuhasebeHesapKoduSayaclari]
            SET [SonSiraNo] = @YeniSiraNo,
                [UpdatedAt] = SYSUTCDATETIME()
            WHERE [IsDeleted] = 0
              AND [TesisId] = @TesisId
              AND [AnaHesapKodu] = @DepoAnaHesapKodu;

            SET @Kod = CONCAT(@DepoAnaHesapKodu, N'.', CAST(@YeniSiraNo as nvarchar(16)));
            SET @MhpAciklama = N'Depo seed otomatik detay hesabı | Seed: EskiDepoAktarimi';

            SELECT TOP (1) @MuhasebeHesapPlaniId = mhp.Id
            FROM [muhasebe].[MuhasebeHesapPlanlari] mhp
            WHERE mhp.IsDeleted = 0
              AND mhp.TesisId = @TesisId
              AND mhp.Kod = @Kod;

            IF @MuhasebeHesapPlaniId IS NULL
            BEGIN
                INSERT INTO [muhasebe].[MuhasebeHesapPlanlari]
                (
                    [TesisId], [Kod], [TamKod], [Ad], [SeviyeNo], [UstHesapId], [AktifMi], [Aciklama], [IsDeleted], [CreatedAt]
                )
                VALUES
                (
                    @TesisId, @Kod, @Kod, @Ad, @AnaSeviyeNo + 1, @AnaHesapId, 1, @MhpAciklama, 0, SYSUTCDATETIME()
                );

                SET @MuhasebeHesapPlaniId = CAST(SCOPE_IDENTITY() as int);
            END;

            SET @Aciklama = CONCAT(
                @SeedMarker,
                N' | Eski sistem depo ID: ', CAST(@EskiId as nvarchar(16)),
                N' | Eski ana depo ID: ', CAST(@EskiAnaDepoId as nvarchar(16)),
                N' | Eski muhasebe kodu: ', @EskiMuhasebeKodu,
                N' | Eski sıralama: ', CAST(@Sira as nvarchar(16)),
                N' | Çıkış muhasebe kodu: ', ISNULL(@CikisMuhasebeKodu, N'NULL')
            );

            INSERT INTO [muhasebe].[Depolar]
            (
                [TesisId], [UstDepoId], [Kod], [Ad], [MuhasebeHesapPlaniId], [AnaMuhasebeHesapKodu], [MuhasebeHesapSiraNo],
                [MalzemeKayitTipi], [SatisFiyatlariniGoster], [AvansGenel], [AktifMi], [Aciklama], [IsDeleted], [CreatedAt]
            )
            VALUES
            (
                @TesisId, NULL, @Kod, @Ad, @MuhasebeHesapPlaniId, @DepoAnaHesapKodu, @YeniSiraNo,
                0, 0, 0, 1, @Aciklama, 0, SYSUTCDATETIME()
            );
        END
        ELSE IF @MuhasebeHesapPlaniId IS NULL
        BEGIN
            SET @MhpAciklama = N'Depo seed otomatik detay hesabı | Seed: EskiDepoAktarimi';

            SELECT TOP (1) @MuhasebeHesapPlaniId = mhp.Id
            FROM [muhasebe].[MuhasebeHesapPlanlari] mhp
            WHERE mhp.IsDeleted = 0
              AND mhp.TesisId = @TesisId
              AND mhp.Ad = @Ad
              AND mhp.UstHesapId = @AnaHesapId;

            IF @MuhasebeHesapPlaniId IS NULL
            BEGIN
                SET @YeniSiraNo = NULL;
                SELECT TOP (1) @YeniSiraNo = s.SonSiraNo
                FROM [muhasebe].[MuhasebeHesapKoduSayaclari] s
                WHERE s.IsDeleted = 0
                  AND s.TesisId = @TesisId
                  AND s.AnaHesapKodu = @DepoAnaHesapKodu;

                IF @YeniSiraNo IS NULL
                BEGIN
                    INSERT INTO [muhasebe].[MuhasebeHesapKoduSayaclari]
                    (
                        [TesisId], [AnaHesapKodu], [SonSiraNo], [Aciklama], [IsDeleted], [CreatedAt]
                    )
                    VALUES
                    (
                        @TesisId, @DepoAnaHesapKodu, 0, N'Seed: EskiDepoAktarimi', 0, SYSUTCDATETIME()
                    );

                    SET @YeniSiraNo = 0;
                END;

                SET @YeniSiraNo = @YeniSiraNo + 1;

                UPDATE [muhasebe].[MuhasebeHesapKoduSayaclari]
                SET [SonSiraNo] = @YeniSiraNo,
                    [UpdatedAt] = SYSUTCDATETIME()
                WHERE [IsDeleted] = 0
                  AND [TesisId] = @TesisId
                  AND [AnaHesapKodu] = @DepoAnaHesapKodu;

                SET @Kod = CONCAT(@DepoAnaHesapKodu, N'.', CAST(@YeniSiraNo as nvarchar(16)));

                INSERT INTO [muhasebe].[MuhasebeHesapPlanlari]
                (
                    [TesisId], [Kod], [TamKod], [Ad], [SeviyeNo], [UstHesapId], [AktifMi], [Aciklama], [IsDeleted], [CreatedAt]
                )
                VALUES
                (
                    @TesisId, @Kod, @Kod, @Ad, @AnaSeviyeNo + 1, @AnaHesapId, 1, @MhpAciklama, 0, SYSUTCDATETIME()
                );

                SET @MuhasebeHesapPlaniId = CAST(SCOPE_IDENTITY() as int);
            END;

            UPDATE [muhasebe].[Depolar]
            SET [MuhasebeHesapPlaniId] = @MuhasebeHesapPlaniId,
                [AnaMuhasebeHesapKodu] = COALESCE([AnaMuhasebeHesapKodu], @DepoAnaHesapKodu),
                [MuhasebeHesapSiraNo] = COALESCE([MuhasebeHesapSiraNo], TRY_CAST(PARSENAME(REPLACE((SELECT TOP (1) mhp.Kod FROM [muhasebe].[MuhasebeHesapPlanlari] mhp WHERE mhp.Id = @MuhasebeHesapPlaniId), '.', '_'), 1) as int)),
                [UpdatedAt] = SYSUTCDATETIME()
            WHERE [Id] = @DepoId;
        END;

        FETCH NEXT FROM depo_cursor INTO @Sira, @EskiId, @EskiAnaDepoId, @Ad, @EskiMuhasebeKodu, @CikisMuhasebeKodu;
    END

    CLOSE depo_cursor;
    DEALLOCATE depo_cursor;

    FETCH NEXT FROM tesis_cursor INTO @TesisId;
END

CLOSE tesis_cursor;
DEALLOCATE tesis_cursor;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
SET NOCOUNT ON;

UPDATE d
SET d.AktifMi = 0,
    d.UpdatedAt = SYSUTCDATETIME()
FROM [muhasebe].[Depolar] d
WHERE d.IsDeleted = 0
  AND d.Aciklama LIKE N'Seed: EskiDepoAktarimi%';

UPDATE mhp
SET mhp.AktifMi = 0,
    mhp.UpdatedAt = SYSUTCDATETIME()
FROM [muhasebe].[MuhasebeHesapPlanlari] mhp
WHERE mhp.IsDeleted = 0
  AND mhp.Aciklama LIKE N'Depo seed otomatik detay hesabı | Seed: EskiDepoAktarimi%';
");
        }
    }
}


