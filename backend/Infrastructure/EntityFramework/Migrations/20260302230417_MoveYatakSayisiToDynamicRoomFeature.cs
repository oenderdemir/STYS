using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class MoveYatakSayisiToDynamicRoomFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @CreatedBy nvarchar(64) = 'migration';

                IF NOT EXISTS (SELECT 1 FROM [dbo].[OdaOzellikleri] WHERE [Kod] = 'YATAK_SAYISI' AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[OdaOzellikleri] ([Kod], [Ad], [VeriTipi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES ('YATAK_SAYISI', N'Yatak Sayisi', 'number', 1, 0, @Now, @Now, @CreatedBy, @CreatedBy);

                DECLARE @YatakSayisiOzellikId int = (SELECT TOP 1 [Id] FROM [dbo].[OdaOzellikleri] WHERE [Kod] = 'YATAK_SAYISI' AND [IsDeleted] = 0 ORDER BY [Id]);

                INSERT INTO [dbo].[OdaOzellikDegerleri]
                    ([OdaId], [OdaOzellikId], [Deger], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                SELECT
                    o.[Id],
                    @YatakSayisiOzellikId,
                    CONVERT(varchar(64), o.[YatakSayisi]),
                    0, @Now, @Now, @CreatedBy, @CreatedBy
                FROM [dbo].[Odalar] o
                WHERE @YatakSayisiOzellikId IS NOT NULL
                  AND o.[YatakSayisi] IS NOT NULL
                  AND o.[YatakSayisi] > 0
                  AND NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[OdaOzellikDegerleri] d
                    WHERE d.[OdaId] = o.[Id]
                      AND d.[OdaOzellikId] = @YatakSayisiOzellikId
                      AND d.[IsDeleted] = 0);
                """);

            migrationBuilder.DropColumn(
                name: "YatakSayisi",
                schema: "dbo",
                table: "Odalar");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "YatakSayisi",
                schema: "dbo",
                table: "Odalar",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                DECLARE @YatakSayisiOzellikId int = (SELECT TOP 1 [Id] FROM [dbo].[OdaOzellikleri] WHERE [Kod] = 'YATAK_SAYISI' AND [IsDeleted] = 0 ORDER BY [Id]);

                UPDATE o
                SET [YatakSayisi] = TRY_CONVERT(int, TRY_CONVERT(decimal(18,2), d.[Deger]))
                FROM [dbo].[Odalar] o
                OUTER APPLY (
                    SELECT TOP 1 [Deger]
                    FROM [dbo].[OdaOzellikDegerleri]
                    WHERE [OdaId] = o.[Id]
                      AND [OdaOzellikId] = @YatakSayisiOzellikId
                      AND [IsDeleted] = 0
                    ORDER BY [Id] DESC
                ) d
                WHERE @YatakSayisiOzellikId IS NOT NULL;
                """);
        }
    }
}
