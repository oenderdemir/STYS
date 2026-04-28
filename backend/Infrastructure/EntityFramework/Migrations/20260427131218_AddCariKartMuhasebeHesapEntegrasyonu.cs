using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddCariKartMuhasebeHesapEntegrasyonu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MuhasebeHesapPlanlari_UstHesapId_Kod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari");

            migrationBuilder.AlterColumn<string>(
                name: "Kod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16);

            migrationBuilder.Sql(
                """
                UPDATE [muhasebe].[MuhasebeHesapPlanlari]
                SET [Kod] = [TamKod]
                WHERE [IsDeleted] = 0
                  AND ISNULL([Kod], N'') <> ISNULL([TamKod], N'');
                """);

            migrationBuilder.AddColumn<string>(
                name: "AnaMuhasebeHesapKodu",
                schema: "muhasebe",
                table: "CariKartlar",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MuhasebeHesapPlaniId",
                schema: "muhasebe",
                table: "CariKartlar",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MuhasebeHesapSiraNo",
                schema: "muhasebe",
                table: "CariKartlar",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TesisSegmenti",
                schema: "muhasebe",
                table: "CariKartlar",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MuhasebeHesapKoduSayaclari",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    AnaHesapKodu = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SonSiraNo = table.Column<int>(type: "int", nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MuhasebeHesapKoduSayaclari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MuhasebeHesapKoduSayaclari_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeHesapPlanlari_Kod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CariKartlar_MuhasebeHesapPlaniId",
                schema: "muhasebe",
                table: "CariKartlar",
                column: "MuhasebeHesapPlaniId",
                filter: "[IsDeleted] = 0 AND [MuhasebeHesapPlaniId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CariKartlar_TesisId_AnaMuhasebeHesapKodu",
                schema: "muhasebe",
                table: "CariKartlar",
                columns: new[] { "TesisId", "AnaMuhasebeHesapKodu" },
                filter: "[IsDeleted] = 0 AND [TesisId] IS NOT NULL AND [AnaMuhasebeHesapKodu] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeHesapKoduSayaclari_TesisId",
                schema: "muhasebe",
                table: "MuhasebeHesapKoduSayaclari",
                column: "TesisId",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeHesapKoduSayaclari_TesisId_AnaHesapKodu",
                schema: "muhasebe",
                table: "MuhasebeHesapKoduSayaclari",
                columns: new[] { "TesisId", "AnaHesapKodu" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_CariKartlar_MuhasebeHesapPlanlari_MuhasebeHesapPlaniId",
                schema: "muhasebe",
                table: "CariKartlar",
                column: "MuhasebeHesapPlaniId",
                principalSchema: "muhasebe",
                principalTable: "MuhasebeHesapPlanlari",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                SET NOCOUNT ON;

                DECLARE @FallbackTesisId int;
                SELECT TOP (1) @FallbackTesisId = [Id]
                FROM [dbo].[Tesisler]
                WHERE [IsDeleted] = 0 AND [AktifMi] = 1
                ORDER BY [Id];

                IF @FallbackTesisId IS NULL
                    THROW 50001, N'Aktif tesis bulunamadigi icin TesisId bos cari kartlar duzeltilemedi.', 1;

                UPDATE c
                SET c.[TesisId] = @FallbackTesisId
                FROM [muhasebe].[CariKartlar] c
                WHERE c.[IsDeleted] = 0
                  AND c.[CariTipi] IN (N'Tedarikci', N'Musteri', N'KurumsalMusteri')
                  AND (c.[TesisId] IS NULL OR c.[TesisId] <= 0);

                DECLARE @TedarikciAnaHesapId int;
                DECLARE @MusteriAnaHesapId int;

                SELECT TOP (1) @TedarikciAnaHesapId = [Id]
                FROM [muhasebe].[MuhasebeHesapPlanlari]
                WHERE [IsDeleted] = 0 AND [AktifMi] = 1 AND ([TamKod] = N'3.32.320' OR [Kod] = N'3.32.320')
                ORDER BY CASE WHEN [TamKod] = N'3.32.320' THEN 0 ELSE 1 END, [Id];

                SELECT TOP (1) @MusteriAnaHesapId = [Id]
                FROM [muhasebe].[MuhasebeHesapPlanlari]
                WHERE [IsDeleted] = 0 AND [AktifMi] = 1 AND ([TamKod] = N'1.12.120' OR [Kod] = N'1.12.120')
                ORDER BY CASE WHEN [TamKod] = N'1.12.120' THEN 0 ELSE 1 END, [Id];

                IF @TedarikciAnaHesapId IS NULL
                    THROW 50002, N'3.32.320 SATICILAR ana hesabı bulunamadı.', 1;

                IF @MusteriAnaHesapId IS NULL
                    THROW 50003, N'1.12.120 ALICILAR ana hesabı bulunamadı.', 1;

                ;WITH Hedef AS (
                    SELECT
                        c.[Id],
                        c.[TesisId],
                        c.[CariTipi],
                        CASE WHEN c.[CariTipi] = N'Tedarikci' THEN N'3.32.320' ELSE N'1.12.120' END AS AnaMuhasebeHesapKodu,
                        CASE
                            WHEN c.[TesisId] > 999 THEN CONVERT(nvarchar(16), c.[TesisId])
                            ELSE RIGHT(N'000' + CONVERT(nvarchar(16), c.[TesisId]), 3)
                        END AS TesisSegmenti
                    FROM [muhasebe].[CariKartlar] c
                    WHERE c.[IsDeleted] = 0
                      AND c.[CariTipi] IN (N'Tedarikci', N'Musteri', N'KurumsalMusteri')
                ),
                Sirali AS (
                    SELECT
                        h.[Id],
                        h.[AnaMuhasebeHesapKodu],
                        h.[TesisSegmenti],
                        ROW_NUMBER() OVER (PARTITION BY h.[TesisId], h.[AnaMuhasebeHesapKodu] ORDER BY h.[Id]) AS SiraNo
                    FROM Hedef h
                )
                UPDATE c
                SET
                    c.[AnaMuhasebeHesapKodu] = s.[AnaMuhasebeHesapKodu],
                    c.[TesisSegmenti] = s.[TesisSegmenti],
                    c.[MuhasebeHesapSiraNo] = s.[SiraNo],
                    c.[CariKodu] = CONCAT(s.[AnaMuhasebeHesapKodu], N'.', s.[TesisSegmenti], N'.', CONVERT(nvarchar(32), s.[SiraNo]))
                FROM [muhasebe].[CariKartlar] c
                INNER JOIN Sirali s ON s.[Id] = c.[Id]
                WHERE c.[IsDeleted] = 0
                  AND c.[CariTipi] IN (N'Tedarikci', N'Musteri', N'KurumsalMusteri')
                  AND (
                        c.[AnaMuhasebeHesapKodu] IS NULL
                        OR c.[TesisSegmenti] IS NULL
                        OR c.[MuhasebeHesapSiraNo] IS NULL
                        OR c.[CariKodu] IS NULL
                        OR LTRIM(RTRIM(c.[CariKodu])) = N''
                  );

                DECLARE @CariId int, @CariKodu nvarchar(64), @CariUnvan nvarchar(256), @CariTipi nvarchar(32), @CariHesapId int, @UstHesapId int, @UstSeviyeNo int;

                DECLARE CariCursor CURSOR LOCAL FAST_FORWARD FOR
                    SELECT c.[Id], c.[CariKodu], c.[UnvanAdSoyad], c.[CariTipi]
                    FROM [muhasebe].[CariKartlar] c
                    WHERE c.[IsDeleted] = 0
                      AND c.[CariTipi] IN (N'Tedarikci', N'Musteri', N'KurumsalMusteri');

                OPEN CariCursor;
                FETCH NEXT FROM CariCursor INTO @CariId, @CariKodu, @CariUnvan, @CariTipi;

                WHILE @@FETCH_STATUS = 0
                BEGIN
                    IF @CariTipi = N'Tedarikci' SET @UstHesapId = @TedarikciAnaHesapId;
                    ELSE SET @UstHesapId = @MusteriAnaHesapId;

                    SELECT TOP (1) @UstSeviyeNo = [SeviyeNo]
                    FROM [muhasebe].[MuhasebeHesapPlanlari]
                    WHERE [Id] = @UstHesapId;

                    SELECT TOP (1) @CariHesapId = h.[Id]
                    FROM [muhasebe].[MuhasebeHesapPlanlari] h
                    WHERE h.[IsDeleted] = 0
                      AND (h.[Kod] = @CariKodu OR h.[TamKod] = @CariKodu)
                    ORDER BY h.[Id];

                    IF @CariHesapId IS NOT NULL
                    BEGIN
                        IF EXISTS (
                            SELECT 1
                            FROM [muhasebe].[CariKartlar] c2
                            WHERE c2.[IsDeleted] = 0
                              AND c2.[Id] <> @CariId
                              AND c2.[MuhasebeHesapPlaniId] = @CariHesapId)
                        BEGIN
                            THROW 50004, N'Backfill sirasinda ayni muhasebe kodu baska bir cari kart ile iliskili bulundu.', 1;
                        END;

                        UPDATE [muhasebe].[MuhasebeHesapPlanlari]
                        SET [Kod] = @CariKodu,
                            [TamKod] = @CariKodu,
                            [Ad] = @CariUnvan,
                            [UstHesapId] = @UstHesapId,
                            [AktifMi] = 1,
                            [UpdatedAt] = SYSUTCDATETIME()
                        WHERE [Id] = @CariHesapId;
                    END
                    ELSE
                    BEGIN
                        INSERT INTO [muhasebe].[MuhasebeHesapPlanlari]
                        (
                            [Kod], [TamKod], [Ad], [SeviyeNo], [UstHesapId], [AktifMi], [Aciklama],
                            [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
                        )
                        VALUES
                        (
                            @CariKodu, @CariKodu, @CariUnvan,
                            ISNULL(@UstSeviyeNo, 3) + 1, @UstHesapId, 1, N'Cari kart backfill detay hesabi',
                            0, SYSUTCDATETIME(), SYSUTCDATETIME(), N'system', N'system'
                        );

                        SET @CariHesapId = SCOPE_IDENTITY();
                    END;

                    UPDATE [muhasebe].[CariKartlar]
                    SET [MuhasebeHesapPlaniId] = @CariHesapId
                    WHERE [Id] = @CariId
                      AND [IsDeleted] = 0
                      AND [MuhasebeHesapPlaniId] IS NULL;

                    SET @CariHesapId = NULL;
                    FETCH NEXT FROM CariCursor INTO @CariId, @CariKodu, @CariUnvan, @CariTipi;
                END;

                CLOSE CariCursor;
                DEALLOCATE CariCursor;

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
                    VALUES (kaynak.[TesisId], kaynak.[AnaHesapKodu], kaynak.[SonSiraNo], N'Cari kart backfill sayaci', 0, SYSUTCDATETIME(), SYSUTCDATETIME(), N'system', N'system');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CariKartlar_MuhasebeHesapPlanlari_MuhasebeHesapPlaniId",
                schema: "muhasebe",
                table: "CariKartlar");

            migrationBuilder.DropTable(
                name: "MuhasebeHesapKoduSayaclari",
                schema: "muhasebe");

            migrationBuilder.DropIndex(
                name: "IX_MuhasebeHesapPlanlari_Kod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari");

            migrationBuilder.DropIndex(
                name: "IX_CariKartlar_MuhasebeHesapPlaniId",
                schema: "muhasebe",
                table: "CariKartlar");

            migrationBuilder.DropIndex(
                name: "IX_CariKartlar_TesisId_AnaMuhasebeHesapKodu",
                schema: "muhasebe",
                table: "CariKartlar");

            migrationBuilder.DropColumn(
                name: "AnaMuhasebeHesapKodu",
                schema: "muhasebe",
                table: "CariKartlar");

            migrationBuilder.DropColumn(
                name: "MuhasebeHesapPlaniId",
                schema: "muhasebe",
                table: "CariKartlar");

            migrationBuilder.DropColumn(
                name: "MuhasebeHesapSiraNo",
                schema: "muhasebe",
                table: "CariKartlar");

            migrationBuilder.DropColumn(
                name: "TesisSegmenti",
                schema: "muhasebe",
                table: "CariKartlar");

            migrationBuilder.AlterColumn<string>(
                name: "Kod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeHesapPlanlari_UstHesapId_Kod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                columns: new[] { "UstHesapId", "Kod" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
