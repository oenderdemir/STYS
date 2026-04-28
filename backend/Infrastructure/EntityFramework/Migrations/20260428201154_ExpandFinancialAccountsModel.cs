using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class ExpandFinancialAccountsModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MuhasebeHesapPlanlari_Kod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari");

            migrationBuilder.DropIndex(
                name: "IX_MuhasebeHesapPlanlari_TamKod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari");

            migrationBuilder.DropIndex(
                name: "IX_KasaBankaHesaplari_MuhasebeHesapPlaniId",
                schema: "muhasebe",
                table: "KasaBankaHesaplari");

            migrationBuilder.AddColumn<int>(
                name: "TesisId",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "MuhasebeHesapPlaniId",
                schema: "muhasebe",
                table: "KasaBankaHesaplari",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "AnaMuhasebeHesapKodu",
                schema: "muhasebe",
                table: "KasaBankaHesaplari",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BagliBankaHesapId",
                schema: "muhasebe",
                table: "KasaBankaHesaplari",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HesapKesimGunu",
                schema: "muhasebe",
                table: "KasaBankaHesaplari",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KartAdi",
                schema: "muhasebe",
                table: "KasaBankaHesaplari",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "KartLimiti",
                schema: "muhasebe",
                table: "KasaBankaHesaplari",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KartNoMaskeli",
                schema: "muhasebe",
                table: "KasaBankaHesaplari",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Lokasyon",
                schema: "muhasebe",
                table: "KasaBankaHesaplari",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MuhasebeHesapSiraNo",
                schema: "muhasebe",
                table: "KasaBankaHesaplari",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParaBirimi",
                schema: "muhasebe",
                table: "KasaBankaHesaplari",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SonOdemeGunu",
                schema: "muhasebe",
                table: "KasaBankaHesaplari",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SorumluKisi",
                schema: "muhasebe",
                table: "KasaBankaHesaplari",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ValorGunSayisi",
                schema: "muhasebe",
                table: "KasaBankaHesaplari",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeHesapPlanlari_Kod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0 AND [TesisId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeHesapPlanlari_TamKod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                column: "TamKod",
                unique: true,
                filter: "[IsDeleted] = 0 AND [TesisId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeHesapPlanlari_TesisId",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                column: "TesisId",
                filter: "[IsDeleted] = 0 AND [TesisId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeHesapPlanlari_TesisId_Kod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                columns: new[] { "TesisId", "Kod" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [TesisId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeHesapPlanlari_TesisId_TamKod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                columns: new[] { "TesisId", "TamKod" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [TesisId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_KasaBankaHesaplari_BagliBankaHesapId",
                schema: "muhasebe",
                table: "KasaBankaHesaplari",
                column: "BagliBankaHesapId",
                filter: "[IsDeleted] = 0 AND [BagliBankaHesapId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_KasaBankaHesaplari_MuhasebeHesapPlaniId",
                schema: "muhasebe",
                table: "KasaBankaHesaplari",
                column: "MuhasebeHesapPlaniId",
                filter: "[IsDeleted] = 0 AND [MuhasebeHesapPlaniId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_KasaBankaHesaplari_TesisId_AnaMuhasebeHesapKodu",
                schema: "muhasebe",
                table: "KasaBankaHesaplari",
                columns: new[] { "TesisId", "AnaMuhasebeHesapKodu" },
                filter: "[IsDeleted] = 0 AND [TesisId] IS NOT NULL AND [AnaMuhasebeHesapKodu] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_KasaBankaHesaplari_KasaBankaHesaplari_BagliBankaHesapId",
                schema: "muhasebe",
                table: "KasaBankaHesaplari",
                column: "BagliBankaHesapId",
                principalSchema: "muhasebe",
                principalTable: "KasaBankaHesaplari",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MuhasebeHesapPlanlari_Tesisler_TesisId",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                column: "TesisId",
                principalSchema: "dbo",
                principalTable: "Tesisler",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                SET NOCOUNT ON;
                DECLARE @Now datetime2 = SYSUTCDATETIME();

                DECLARE @Ana100Id int;
                DECLARE @Ana102Id int;
                DECLARE @Ana109Id int;

                SELECT TOP (1) @Ana100Id = [Id] FROM [muhasebe].[MuhasebeHesapPlanlari] WHERE [IsDeleted] = 0 AND [TesisId] IS NULL AND ([Kod] = N'1.10.100' OR [TamKod] = N'1.10.100') ORDER BY [Id];
                SELECT TOP (1) @Ana102Id = [Id] FROM [muhasebe].[MuhasebeHesapPlanlari] WHERE [IsDeleted] = 0 AND [TesisId] IS NULL AND ([Kod] = N'1.10.102' OR [TamKod] = N'1.10.102') ORDER BY [Id];
                SELECT TOP (1) @Ana109Id = [Id] FROM [muhasebe].[MuhasebeHesapPlanlari] WHERE [IsDeleted] = 0 AND [TesisId] IS NULL AND ([Kod] = N'1.10.109' OR [TamKod] = N'1.10.109') ORDER BY [Id];

                IF @Ana100Id IS NULL
                    THROW 51001, N'1.10.100 KASA ana hesabı bulunamadı.', 1;
                IF @Ana102Id IS NULL
                    THROW 51002, N'1.10.102 BANKALAR ana hesabı bulunamadı.', 1;

                IF @Ana109Id IS NULL
                BEGIN
                    INSERT INTO [muhasebe].[MuhasebeHesapPlanlari]
                    (
                        [Kod], [TamKod], [Ad], [SeviyeNo], [TesisId], [UstHesapId], [AktifMi], [Aciklama],
                        [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
                    )
                    VALUES
                    (
                        N'1.10.109', N'1.10.109', N'KREDI KARTLARI', 4, NULL, @Ana102Id, 1, N'Migration seed',
                        0, @Now, @Now, N'system', N'system'
                    );
                    SET @Ana109Id = SCOPE_IDENTITY();
                END;

                ;WITH Hedef AS (
                    SELECT
                        h.[Id],
                        h.[TesisId],
                        CASE
                            WHEN h.[Tip] = N'NakitKasa' THEN N'1.10.100'
                            WHEN h.[Tip] = N'Banka' THEN N'1.10.102'
                            WHEN h.[Tip] = N'DovizHesabi' THEN N'1.10.102'
                            WHEN h.[Tip] = N'KrediKarti' THEN N'1.10.109'
                            ELSE N'1.10.102'
                        END AS AnaKod
                    FROM [muhasebe].[KasaBankaHesaplari] h
                    WHERE h.[IsDeleted] = 0 AND h.[TesisId] IS NOT NULL
                ),
                Sirali AS (
                    SELECT
                        x.[Id],
                        x.[AnaKod],
                        ROW_NUMBER() OVER (PARTITION BY x.[TesisId], x.[AnaKod] ORDER BY x.[Id]) AS SiraNo
                    FROM Hedef x
                )
                UPDATE h
                SET
                    h.[AnaMuhasebeHesapKodu] = s.[AnaKod],
                    h.[MuhasebeHesapSiraNo] = s.[SiraNo],
                    h.[Kod] = CONCAT(s.[AnaKod], N'.', CONVERT(nvarchar(32), s.[SiraNo])),
                    h.[ParaBirimi] = COALESCE(NULLIF(LTRIM(RTRIM(h.[ParaBirimi])), N''), N'TRY'),
                    h.[ValorGunSayisi] = CASE
                        WHEN h.[Tip] = N'KrediKarti' AND ISNULL(h.[ValorGunSayisi], 0) = 0 THEN 1
                        WHEN h.[ValorGunSayisi] < 0 THEN 0
                        WHEN h.[ValorGunSayisi] > 365 THEN 365
                        ELSE ISNULL(h.[ValorGunSayisi], 0)
                    END
                FROM [muhasebe].[KasaBankaHesaplari] h
                INNER JOIN Sirali s ON s.[Id] = h.[Id]
                WHERE h.[IsDeleted] = 0
                  AND h.[TesisId] IS NOT NULL;

                DECLARE @HesapId int, @TesisId int, @Tip nvarchar(16), @Kod nvarchar(64), @Ad nvarchar(200), @UstHesapId int, @DetayId int;
                DECLARE HesapCursor CURSOR LOCAL FAST_FORWARD FOR
                    SELECT [Id], [TesisId], [Tip], [Kod], [Ad]
                    FROM [muhasebe].[KasaBankaHesaplari]
                    WHERE [IsDeleted] = 0 AND [TesisId] IS NOT NULL;

                OPEN HesapCursor;
                FETCH NEXT FROM HesapCursor INTO @HesapId, @TesisId, @Tip, @Kod, @Ad;

                WHILE @@FETCH_STATUS = 0
                BEGIN
                    SET @UstHesapId = CASE
                        WHEN @Tip = N'NakitKasa' THEN @Ana100Id
                        WHEN @Tip = N'KrediKarti' THEN @Ana109Id
                        ELSE @Ana102Id
                    END;

                    SELECT TOP (1) @DetayId = [Id]
                    FROM [muhasebe].[MuhasebeHesapPlanlari]
                    WHERE [IsDeleted] = 0
                      AND [TesisId] = @TesisId
                      AND ([Kod] = @Kod OR [TamKod] = @Kod)
                    ORDER BY [Id];

                    IF @DetayId IS NULL
                    BEGIN
                        INSERT INTO [muhasebe].[MuhasebeHesapPlanlari]
                        (
                            [Kod], [TamKod], [Ad], [SeviyeNo], [TesisId], [UstHesapId], [AktifMi], [Aciklama],
                            [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
                        )
                        VALUES
                        (
                            @Kod, @Kod, @Ad, 5, @TesisId, @UstHesapId, 1, N'Finansal hesap backfill',
                            0, @Now, @Now, N'system', N'system'
                        );
                        SET @DetayId = SCOPE_IDENTITY();
                    END
                    ELSE
                    BEGIN
                        UPDATE [muhasebe].[MuhasebeHesapPlanlari]
                        SET [Ad] = @Ad,
                            [UstHesapId] = @UstHesapId,
                            [AktifMi] = 1,
                            [UpdatedAt] = @Now,
                            [UpdatedBy] = N'system'
                        WHERE [Id] = @DetayId;
                    END;

                    UPDATE [muhasebe].[KasaBankaHesaplari]
                    SET [MuhasebeHesapPlaniId] = @DetayId
                    WHERE [Id] = @HesapId
                      AND [IsDeleted] = 0
                      AND ([MuhasebeHesapPlaniId] IS NULL OR [MuhasebeHesapPlaniId] = 0);

                    SET @DetayId = NULL;
                    FETCH NEXT FROM HesapCursor INTO @HesapId, @TesisId, @Tip, @Kod, @Ad;
                END;

                CLOSE HesapCursor;
                DEALLOCATE HesapCursor;

                ;WITH SayacKaynak AS (
                    SELECT [TesisId], [AnaMuhasebeHesapKodu] AS AnaHesapKodu, MAX([MuhasebeHesapSiraNo]) AS SonSiraNo
                    FROM [muhasebe].[KasaBankaHesaplari]
                    WHERE [IsDeleted] = 0
                      AND [TesisId] IS NOT NULL
                      AND [AnaMuhasebeHesapKodu] IS NOT NULL
                      AND [MuhasebeHesapSiraNo] IS NOT NULL
                    GROUP BY [TesisId], [AnaMuhasebeHesapKodu]
                )
                MERGE [muhasebe].[MuhasebeHesapKoduSayaclari] AS hedef
                USING SayacKaynak AS kaynak
                ON hedef.[IsDeleted] = 0
                   AND hedef.[TesisId] = kaynak.[TesisId]
                   AND hedef.[AnaHesapKodu] = kaynak.[AnaHesapKodu]
                WHEN MATCHED THEN
                    UPDATE SET hedef.[SonSiraNo] = CASE WHEN hedef.[SonSiraNo] > kaynak.[SonSiraNo] THEN hedef.[SonSiraNo] ELSE kaynak.[SonSiraNo] END,
                               hedef.[UpdatedAt] = @Now,
                               hedef.[UpdatedBy] = N'system'
                WHEN NOT MATCHED THEN
                    INSERT ([TesisId], [AnaHesapKodu], [SonSiraNo], [Aciklama], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (kaynak.[TesisId], kaynak.[AnaHesapKodu], kaynak.[SonSiraNo], N'Finansal hesap backfill sayaci', 0, @Now, @Now, N'system', N'system');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KasaBankaHesaplari_KasaBankaHesaplari_BagliBankaHesapId",
                schema: "muhasebe",
                table: "KasaBankaHesaplari");

            migrationBuilder.DropForeignKey(
                name: "FK_MuhasebeHesapPlanlari_Tesisler_TesisId",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari");

            migrationBuilder.DropIndex(
                name: "IX_MuhasebeHesapPlanlari_Kod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari");

            migrationBuilder.DropIndex(
                name: "IX_MuhasebeHesapPlanlari_TamKod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari");

            migrationBuilder.DropIndex(
                name: "IX_MuhasebeHesapPlanlari_TesisId",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari");

            migrationBuilder.DropIndex(
                name: "IX_MuhasebeHesapPlanlari_TesisId_Kod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari");

            migrationBuilder.DropIndex(
                name: "IX_MuhasebeHesapPlanlari_TesisId_TamKod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari");

            migrationBuilder.DropIndex(
                name: "IX_KasaBankaHesaplari_BagliBankaHesapId",
                schema: "muhasebe",
                table: "KasaBankaHesaplari");

            migrationBuilder.DropIndex(
                name: "IX_KasaBankaHesaplari_MuhasebeHesapPlaniId",
                schema: "muhasebe",
                table: "KasaBankaHesaplari");

            migrationBuilder.DropIndex(
                name: "IX_KasaBankaHesaplari_TesisId_AnaMuhasebeHesapKodu",
                schema: "muhasebe",
                table: "KasaBankaHesaplari");

            migrationBuilder.DropColumn(
                name: "TesisId",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari");

            migrationBuilder.DropColumn(
                name: "AnaMuhasebeHesapKodu",
                schema: "muhasebe",
                table: "KasaBankaHesaplari");

            migrationBuilder.DropColumn(
                name: "BagliBankaHesapId",
                schema: "muhasebe",
                table: "KasaBankaHesaplari");

            migrationBuilder.DropColumn(
                name: "HesapKesimGunu",
                schema: "muhasebe",
                table: "KasaBankaHesaplari");

            migrationBuilder.DropColumn(
                name: "KartAdi",
                schema: "muhasebe",
                table: "KasaBankaHesaplari");

            migrationBuilder.DropColumn(
                name: "KartLimiti",
                schema: "muhasebe",
                table: "KasaBankaHesaplari");

            migrationBuilder.DropColumn(
                name: "KartNoMaskeli",
                schema: "muhasebe",
                table: "KasaBankaHesaplari");

            migrationBuilder.DropColumn(
                name: "Lokasyon",
                schema: "muhasebe",
                table: "KasaBankaHesaplari");

            migrationBuilder.DropColumn(
                name: "MuhasebeHesapSiraNo",
                schema: "muhasebe",
                table: "KasaBankaHesaplari");

            migrationBuilder.DropColumn(
                name: "ParaBirimi",
                schema: "muhasebe",
                table: "KasaBankaHesaplari");

            migrationBuilder.DropColumn(
                name: "SonOdemeGunu",
                schema: "muhasebe",
                table: "KasaBankaHesaplari");

            migrationBuilder.DropColumn(
                name: "SorumluKisi",
                schema: "muhasebe",
                table: "KasaBankaHesaplari");

            migrationBuilder.DropColumn(
                name: "ValorGunSayisi",
                schema: "muhasebe",
                table: "KasaBankaHesaplari");

            migrationBuilder.AlterColumn<int>(
                name: "MuhasebeHesapPlaniId",
                schema: "muhasebe",
                table: "KasaBankaHesaplari",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeHesapPlanlari_Kod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MuhasebeHesapPlanlari_TamKod",
                schema: "muhasebe",
                table: "MuhasebeHesapPlanlari",
                column: "TamKod",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KasaBankaHesaplari_MuhasebeHesapPlaniId",
                schema: "muhasebe",
                table: "KasaBankaHesaplari",
                column: "MuhasebeHesapPlaniId",
                filter: "[IsDeleted] = 0");
        }
    }
}
