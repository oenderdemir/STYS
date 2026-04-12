using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260410235500_AddRestoranMenuKategoriTanimlariTable")]
public partial class AddRestoranMenuKategoriTanimlariTable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF NOT EXISTS (
                SELECT 1
                FROM sys.tables t
                INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
                WHERE s.name = N'restoran'
                  AND t.name = N'MenuKategoriTanimlari'
            )
            BEGIN
                CREATE TABLE [restoran].[MenuKategoriTanimlari]
                (
                    [Id] int NOT NULL IDENTITY(1,1),
                    [Ad] nvarchar(128) NOT NULL,
                    [SiraNo] int NOT NULL,
                    [AktifMi] bit NOT NULL,
                    [IsDeleted] bit NOT NULL CONSTRAINT [DF_MenuKategoriTanimlari_IsDeleted] DEFAULT(0),
                    [CreatedAt] datetime2 NULL,
                    [UpdatedAt] datetime2 NULL,
                    [DeletedAt] datetime2 NULL,
                    [CreatedBy] nvarchar(max) NULL,
                    [UpdatedBy] nvarchar(max) NULL,
                    [DeletedBy] nvarchar(max) NULL,
                    CONSTRAINT [PK_MenuKategoriTanimlari] PRIMARY KEY ([Id])
                );
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes i
                WHERE i.name = N'IX_MenuKategoriTanimlari_Ad'
                  AND i.object_id = OBJECT_ID(N'[restoran].[MenuKategoriTanimlari]')
            )
            BEGIN
                CREATE UNIQUE INDEX [IX_MenuKategoriTanimlari_Ad]
                ON [restoran].[MenuKategoriTanimlari]([Ad])
                WHERE [IsDeleted] = 0;
            END;

            INSERT INTO [restoran].[MenuKategoriTanimlari]
            ([Ad], [SiraNo], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
            SELECT
                k.[Ad],
                MIN(k.[SiraNo]) AS [SiraNo],
                CAST(MAX(CASE WHEN k.[AktifMi] = 1 THEN 1 ELSE 0 END) AS bit) AS [AktifMi],
                0,
                SYSUTCDATETIME(),
                SYSUTCDATETIME(),
                N'system',
                N'system'
            FROM [restoran].[RestoranMenuKategorileri] k
            WHERE k.[IsDeleted] = 0
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM [restoran].[MenuKategoriTanimlari] g
                  WHERE g.[Ad] = k.[Ad]
                    AND g.[IsDeleted] = 0
              )
            GROUP BY k.[Ad];
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF EXISTS (
                SELECT 1
                FROM sys.tables t
                INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
                WHERE s.name = N'restoran'
                  AND t.name = N'MenuKategoriTanimlari'
            )
            BEGIN
                DROP TABLE [restoran].[MenuKategoriTanimlari];
            END;
            """);
    }
}
