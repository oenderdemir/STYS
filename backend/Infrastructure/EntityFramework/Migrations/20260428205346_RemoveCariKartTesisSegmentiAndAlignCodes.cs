using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCariKartTesisSegmentiAndAlignCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                SET NOCOUNT ON;

                ;WITH Hedef AS (
                    SELECT
                        c.[Id],
                        c.[TesisId],
                        c.[CariTipi],
                        c.[AnaMuhasebeHesapKodu],
                        c.[MuhasebeHesapSiraNo],
                        CASE
                            WHEN c.[CariTipi] = N'Tedarikci' THEN N'3.32.320'
                            WHEN c.[CariTipi] IN (N'Musteri', N'KurumsalMusteri') THEN N'1.12.120'
                            ELSE c.[AnaMuhasebeHesapKodu]
                        END AS AnaKod
                    FROM [muhasebe].[CariKartlar] c
                    WHERE c.[IsDeleted] = 0
                      AND c.[CariTipi] IN (N'Tedarikci', N'Musteri', N'KurumsalMusteri')
                      AND c.[TesisId] IS NOT NULL
                ),
                Sirali AS (
                    SELECT
                        h.[Id],
                        h.[AnaKod],
                        CASE
                            WHEN h.[MuhasebeHesapSiraNo] IS NOT NULL AND h.[MuhasebeHesapSiraNo] > 0 THEN h.[MuhasebeHesapSiraNo]
                            ELSE ROW_NUMBER() OVER (PARTITION BY h.[TesisId], h.[AnaKod] ORDER BY h.[Id])
                        END AS SiraNo
                    FROM Hedef h
                )
                UPDATE c
                SET
                    c.[AnaMuhasebeHesapKodu] = s.[AnaKod],
                    c.[MuhasebeHesapSiraNo] = s.[SiraNo],
                    c.[CariKodu] = CONCAT(s.[AnaKod], N'.', CONVERT(nvarchar(32), s.[SiraNo]))
                FROM [muhasebe].[CariKartlar] c
                INNER JOIN Sirali s ON s.[Id] = c.[Id]
                WHERE c.[IsDeleted] = 0;

                UPDATE hp
                SET
                    hp.[Kod] = c.[CariKodu],
                    hp.[TamKod] = c.[CariKodu],
                    hp.[Ad] = c.[UnvanAdSoyad],
                    hp.[TesisId] = c.[TesisId],
                    hp.[UpdatedAt] = SYSUTCDATETIME(),
                    hp.[UpdatedBy] = N'system'
                FROM [muhasebe].[MuhasebeHesapPlanlari] hp
                INNER JOIN [muhasebe].[CariKartlar] c ON c.[MuhasebeHesapPlaniId] = hp.[Id]
                WHERE c.[IsDeleted] = 0
                  AND c.[CariTipi] IN (N'Tedarikci', N'Musteri', N'KurumsalMusteri');

                ;WITH SayacKaynak AS (
                    SELECT
                        c.[TesisId],
                        c.[AnaMuhasebeHesapKodu] AS AnaHesapKodu,
                        MAX(c.[MuhasebeHesapSiraNo]) AS SonSiraNo
                    FROM [muhasebe].[CariKartlar] c
                    WHERE c.[IsDeleted] = 0
                      AND c.[CariTipi] IN (N'Tedarikci', N'Musteri', N'KurumsalMusteri')
                      AND c.[TesisId] IS NOT NULL
                      AND c.[AnaMuhasebeHesapKodu] IS NOT NULL
                      AND c.[MuhasebeHesapSiraNo] IS NOT NULL
                    GROUP BY c.[TesisId], c.[AnaMuhasebeHesapKodu]
                )
                MERGE [muhasebe].[MuhasebeHesapKoduSayaclari] AS hedef
                USING SayacKaynak AS kaynak
                ON hedef.[IsDeleted] = 0
                   AND hedef.[TesisId] = kaynak.[TesisId]
                   AND hedef.[AnaHesapKodu] = kaynak.[AnaHesapKodu]
                WHEN MATCHED THEN
                    UPDATE SET
                        hedef.[SonSiraNo] = CASE WHEN hedef.[SonSiraNo] > kaynak.[SonSiraNo] THEN hedef.[SonSiraNo] ELSE kaynak.[SonSiraNo] END,
                        hedef.[UpdatedAt] = SYSUTCDATETIME(),
                        hedef.[UpdatedBy] = N'system'
                WHEN NOT MATCHED THEN
                    INSERT ([TesisId], [AnaHesapKodu], [SonSiraNo], [Aciklama], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (kaynak.[TesisId], kaynak.[AnaHesapKodu], kaynak.[SonSiraNo], N'Cari kart kod sayaci', 0, SYSUTCDATETIME(), SYSUTCDATETIME(), N'system', N'system');
                """);

            migrationBuilder.DropColumn(
                name: "TesisSegmenti",
                schema: "muhasebe",
                table: "CariKartlar");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TesisSegmenti",
                schema: "muhasebe",
                table: "CariKartlar",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);
        }
    }
}
