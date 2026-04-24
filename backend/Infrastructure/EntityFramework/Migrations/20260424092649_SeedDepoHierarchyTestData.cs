using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class SeedDepoHierarchyTestData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @TesisId int;

                SELECT TOP (1) @TesisId = [Id]
                FROM [dbo].[Tesisler]
                WHERE [IsDeleted] = 0 AND [AktifMi] = 1
                ORDER BY [Id];

                IF @TesisId IS NULL
                    RETURN;

                DECLARE @AnaDepoId int;
                DECLARE @AltDepo1Id int;
                DECLARE @AltDepo2Id int;
                DECLARE @IkinciAnaDepoId int;

                SELECT TOP (1) @AnaDepoId = [Id]
                FROM [muhasebe].[Depolar]
                WHERE [Kod] = N'TEST-DEPO-ANA-001' AND [IsDeleted] = 0;

                IF @AnaDepoId IS NULL
                BEGIN
                    INSERT INTO [muhasebe].[Depolar]
                    (
                        [TesisId], [UstDepoId], [MuhasebeHesapPlaniId],
                        [Kod], [Ad], [MalzemeKayitTipi], [SatisFiyatlariniGoster], [AvansGenel], [AktifMi], [Aciklama],
                        [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
                    )
                    VALUES
                    (
                        @TesisId, NULL, NULL,
                        N'TEST-DEPO-ANA-001', N'Test Ana Depo 1', N'MalzemeleriAyriKayittaTut', 1, 0, 1, N'Test seed: ana depo',
                        0, @Now, @Now, N'system', N'system'
                    );

                    SET @AnaDepoId = SCOPE_IDENTITY();
                END;

                SELECT TOP (1) @AltDepo1Id = [Id]
                FROM [muhasebe].[Depolar]
                WHERE [Kod] = N'TEST-DEPO-ALT-001' AND [IsDeleted] = 0;

                IF @AltDepo1Id IS NULL
                BEGIN
                    INSERT INTO [muhasebe].[Depolar]
                    (
                        [TesisId], [UstDepoId], [MuhasebeHesapPlaniId],
                        [Kod], [Ad], [MalzemeKayitTipi], [SatisFiyatlariniGoster], [AvansGenel], [AktifMi], [Aciklama],
                        [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
                    )
                    VALUES
                    (
                        @TesisId, @AnaDepoId, NULL,
                        N'TEST-DEPO-ALT-001', N'Test Alt Depo 1', N'FiyatFarkliMalzemeleriAyriKayittaTut', 0, 0, 1, N'Test seed: alt depo 1',
                        0, @Now, @Now, N'system', N'system'
                    );

                    SET @AltDepo1Id = SCOPE_IDENTITY();
                END;

                SELECT TOP (1) @AltDepo2Id = [Id]
                FROM [muhasebe].[Depolar]
                WHERE [Kod] = N'TEST-DEPO-ALT-002' AND [IsDeleted] = 0;

                IF @AltDepo2Id IS NULL
                BEGIN
                    INSERT INTO [muhasebe].[Depolar]
                    (
                        [TesisId], [UstDepoId], [MuhasebeHesapPlaniId],
                        [Kod], [Ad], [MalzemeKayitTipi], [SatisFiyatlariniGoster], [AvansGenel], [AktifMi], [Aciklama],
                        [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
                    )
                    VALUES
                    (
                        @TesisId, @AnaDepoId, NULL,
                        N'TEST-DEPO-ALT-002', N'Test Alt Depo 2', N'MalzemeleriAyniKayittaTut', 0, 1, 1, N'Test seed: alt depo 2',
                        0, @Now, @Now, N'system', N'system'
                    );

                    SET @AltDepo2Id = SCOPE_IDENTITY();
                END;

                SELECT TOP (1) @IkinciAnaDepoId = [Id]
                FROM [muhasebe].[Depolar]
                WHERE [Kod] = N'TEST-DEPO-ANA-002' AND [IsDeleted] = 0;

                IF @IkinciAnaDepoId IS NULL
                BEGIN
                    INSERT INTO [muhasebe].[Depolar]
                    (
                        [TesisId], [UstDepoId], [MuhasebeHesapPlaniId],
                        [Kod], [Ad], [MalzemeKayitTipi], [SatisFiyatlariniGoster], [AvansGenel], [AktifMi], [Aciklama],
                        [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
                    )
                    VALUES
                    (
                        @TesisId, NULL, NULL,
                        N'TEST-DEPO-ANA-002', N'Test Ana Depo 2', N'MalzemeleriAyriKayittaTut', 1, 0, 1, N'Test seed: ikinci ana depo',
                        0, @Now, @Now, N'system', N'system'
                    );

                    SET @IkinciAnaDepoId = SCOPE_IDENTITY();
                END;

                IF @AnaDepoId IS NOT NULL
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM [muhasebe].[DepoCikisGruplari]
                        WHERE [DepoId] = @AnaDepoId AND [CikisGrupAdi] = N'Perakende'
                          AND [IsDeleted] = 0)
                    BEGIN
                        INSERT INTO [muhasebe].[DepoCikisGruplari]
                        (
                            [DepoId], [CikisGrupAdi], [KarOrani], [LokasyonId],
                            [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
                        )
                        VALUES
                        (
                            @AnaDepoId, N'Perakende', 12.50, NULL,
                            0, @Now, @Now, N'system', N'system'
                        );
                    END;
                END;

                IF @AltDepo1Id IS NOT NULL
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM [muhasebe].[DepoCikisGruplari]
                        WHERE [DepoId] = @AltDepo1Id AND [CikisGrupAdi] = N'Toptan'
                          AND [IsDeleted] = 0)
                    BEGIN
                        INSERT INTO [muhasebe].[DepoCikisGruplari]
                        (
                            [DepoId], [CikisGrupAdi], [KarOrani], [LokasyonId],
                            [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]
                        )
                        VALUES
                        (
                            @AltDepo1Id, N'Toptan', 7.25, NULL,
                            0, @Now, @Now, N'system', N'system'
                        );
                    END;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE cg
                FROM [muhasebe].[DepoCikisGruplari] cg
                INNER JOIN [muhasebe].[Depolar] d ON d.[Id] = cg.[DepoId]
                WHERE d.[Kod] IN (N'TEST-DEPO-ANA-001', N'TEST-DEPO-ALT-001', N'TEST-DEPO-ALT-002', N'TEST-DEPO-ANA-002')
                  AND cg.[CreatedBy] = N'system';

                DELETE FROM [muhasebe].[Depolar]
                WHERE [Kod] IN (N'TEST-DEPO-ANA-001', N'TEST-DEPO-ALT-001', N'TEST-DEPO-ALT-002', N'TEST-DEPO-ANA-002')
                  AND [CreatedBy] = N'system';
                """);
        }
    }
}
