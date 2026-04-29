using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    [DbContext(typeof(StysAppDbContext))]
    [Migration("20260430004000_FixSeedStandartDepoKodSirasiPerTesis")]
    public partial class FixSeedStandartDepoKodSirasiPerTesis : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
SET NOCOUNT ON;

DECLARE @SeedMarker nvarchar(64) = N'Seed: EskiDepoAktarimi';
DECLARE @AnaKod nvarchar(64) = N'1.15.150';

DECLARE @Map TABLE (Ad nvarchar(200) NOT NULL, Sira int NOT NULL);
INSERT INTO @Map (Ad, Sira) VALUES
(N'YEMEKHANE', 1),
(N'KAFETERYA', 2),
(N'ÇAY OCAĞI', 3),
(N'OTOMAT KAPALI', 4),
(N'ERKEK KUAFÖRÜ', 5),
(N'BAYAN KUAFÖRÜ', 6),
(N'SPOR TESİSİ', 7),
(N'TEMSİL AĞIRLAMA', 8),
(N'OTO YIKAMA', 10),
(N'EĞİTİM TESİSİ KAFETERYA', 11),
(N'OTO ŞARJ', 12),
(N'EĞİTİM TESİSİ ORGANİZASYON', 13),
(N'SOSYAL TESİS 3', 14);

DECLARE @TesisId int;
DECLARE tesis_cursor CURSOR LOCAL FAST_FORWARD FOR
SELECT DISTINCT d.TesisId
FROM [muhasebe].[Depolar] d
WHERE d.IsDeleted = 0
  AND d.Aciklama LIKE N'Seed: EskiDepoAktarimi%'
  AND d.TesisId IS NOT NULL;

OPEN tesis_cursor;
FETCH NEXT FROM tesis_cursor INTO @TesisId;

WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID('tempdb..#Rows') IS NOT NULL DROP TABLE #Rows;

    ;WITH seeded AS
    (
        SELECT d.Id, d.TesisId, d.MuhasebeHesapPlaniId, m.Sira,
               ROW_NUMBER() OVER (ORDER BY m.Sira, d.Id) AS YeniSiraNo
        FROM [muhasebe].[Depolar] d
        INNER JOIN @Map m ON m.Ad = d.Ad
        WHERE d.IsDeleted = 0
          AND d.TesisId = @TesisId
          AND d.Aciklama LIKE N'Seed: EskiDepoAktarimi%'
    )
    SELECT * INTO #Rows FROM seeded;

    IF NOT EXISTS (SELECT 1 FROM #Rows)
    BEGIN
        FETCH NEXT FROM tesis_cursor INTO @TesisId;
        CONTINUE;
    END

    IF EXISTS
    (
        SELECT 1
        FROM [muhasebe].[Depolar] d
        INNER JOIN #Rows r ON r.TesisId = d.TesisId
        WHERE d.IsDeleted = 0
          AND d.TesisId = @TesisId
          AND d.Id NOT IN (SELECT Id FROM #Rows)
          AND d.Kod IN (SELECT CONCAT(@AnaKod, N'.', CAST(YeniSiraNo as nvarchar(16))) FROM #Rows)
    )
    BEGIN
        THROW 50021, N'Tesis içinde seed dışı depo kodları ile çakışma var. Otomatik düzeltme durduruldu.', 1;
    END

    UPDATE d
    SET d.Kod = CONCAT(N'TMP-', CAST(@TesisId as nvarchar(16)), N'-', CAST(r.YeniSiraNo as nvarchar(16))),
        d.UpdatedAt = SYSUTCDATETIME()
    FROM [muhasebe].[Depolar] d
    INNER JOIN #Rows r ON r.Id = d.Id;

    UPDATE mhp
    SET mhp.Kod = CONCAT(N'TMP-', CAST(@TesisId as nvarchar(16)), N'-', CAST(r.YeniSiraNo as nvarchar(16))),
        mhp.TamKod = CONCAT(N'TMP-', CAST(@TesisId as nvarchar(16)), N'-', CAST(r.YeniSiraNo as nvarchar(16))),
        mhp.UpdatedAt = SYSUTCDATETIME()
    FROM [muhasebe].[MuhasebeHesapPlanlari] mhp
    INNER JOIN #Rows r ON r.MuhasebeHesapPlaniId = mhp.Id
    WHERE mhp.IsDeleted = 0
      AND mhp.TesisId = @TesisId;

    UPDATE d
    SET d.Kod = CONCAT(@AnaKod, N'.', CAST(r.YeniSiraNo as nvarchar(16))),
        d.AnaMuhasebeHesapKodu = @AnaKod,
        d.MuhasebeHesapSiraNo = r.YeniSiraNo,
        d.UpdatedAt = SYSUTCDATETIME()
    FROM [muhasebe].[Depolar] d
    INNER JOIN #Rows r ON r.Id = d.Id;

    UPDATE mhp
    SET mhp.Kod = CONCAT(@AnaKod, N'.', CAST(r.YeniSiraNo as nvarchar(16))),
        mhp.TamKod = CONCAT(@AnaKod, N'.', CAST(r.YeniSiraNo as nvarchar(16))),
        mhp.UpdatedAt = SYSUTCDATETIME()
    FROM [muhasebe].[MuhasebeHesapPlanlari] mhp
    INNER JOIN #Rows r ON r.MuhasebeHesapPlaniId = mhp.Id
    WHERE mhp.IsDeleted = 0
      AND mhp.TesisId = @TesisId;

    MERGE [muhasebe].[MuhasebeHesapKoduSayaclari] AS tgt
    USING (SELECT @TesisId AS TesisId, @AnaKod AS AnaHesapKodu, (SELECT ISNULL(MAX(YeniSiraNo), 0) FROM #Rows) AS SonSiraNo) AS src
    ON tgt.IsDeleted = 0 AND tgt.TesisId = src.TesisId AND tgt.AnaHesapKodu = src.AnaHesapKodu
    WHEN MATCHED THEN
        UPDATE SET tgt.SonSiraNo = CASE WHEN tgt.SonSiraNo < src.SonSiraNo THEN src.SonSiraNo ELSE tgt.SonSiraNo END,
                   tgt.UpdatedAt = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT (TesisId, AnaHesapKodu, SonSiraNo, Aciklama, IsDeleted, CreatedAt)
        VALUES (src.TesisId, src.AnaHesapKodu, src.SonSiraNo, N'Seed fix: EskiDepoAktarimi', 0, SYSUTCDATETIME());

    FETCH NEXT FROM tesis_cursor INTO @TesisId;
END

CLOSE tesis_cursor;
DEALLOCATE tesis_cursor;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // no-op
        }
    }
}
