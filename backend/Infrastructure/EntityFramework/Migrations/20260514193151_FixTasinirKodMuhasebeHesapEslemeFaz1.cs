using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class FixTasinirKodMuhasebeHesapEslemeFaz1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Index may not exist on fresh databases; drop conditionally
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.indexes i
                    JOIN sys.tables t ON i.object_id = t.object_id
                    JOIN sys.schemas s ON t.schema_id = s.schema_id
                    WHERE s.name = 'muhasebe'
                      AND t.name = 'TasinirKodMuhasebeHesapEslemeleri'
                      AND i.name = 'IX_TasinirKodMuhasebeHesapEslemeleri_TasinirKodId_MuhasebeHesapPlaniId_IslemTuru'
                )
                DROP INDEX [IX_TasinirKodMuhasebeHesapEslemeleri_TasinirKodId_MuhasebeHesapPlaniId_IslemTuru]
                    ON [muhasebe].[TasinirKodMuhasebeHesapEslemeleri];
            ");

            migrationBuilder.AlterColumn<string>(
                name: "MalzemeTipi",
                schema: "muhasebe",
                table: "TasinirKodMuhasebeHesapEslemeleri",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "IslemTuru",
                schema: "muhasebe",
                table: "TasinirKodMuhasebeHesapEslemeleri",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "HareketTipi",
                schema: "muhasebe",
                table: "TasinirKodMuhasebeHesapEslemeleri",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            // Index may already exist from 20260514090929; create conditionally
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes i
                    JOIN sys.tables t ON i.object_id = t.object_id
                    JOIN sys.schemas s ON t.schema_id = s.schema_id
                    WHERE s.name = 'muhasebe'
                      AND t.name = 'TasinirKodMuhasebeHesapEslemeleri'
                      AND i.name = 'IX_TasinirKodMuhasebeHesapEslemeleri_TasinirKodId'
                )
                CREATE INDEX [IX_TasinirKodMuhasebeHesapEslemeleri_TasinirKodId]
                    ON [muhasebe].[TasinirKodMuhasebeHesapEslemeleri] ([TasinirKodId]);
            ");

            // Index may already exist from 20260514090929; create conditionally
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes i
                    JOIN sys.tables t ON i.object_id = t.object_id
                    JOIN sys.schemas s ON t.schema_id = s.schema_id
                    WHERE s.name = 'muhasebe'
                      AND t.name = 'TasinirKodMuhasebeHesapEslemeleri'
                      AND i.name = 'IX_TasinirKodMuhasebeHesapEslemeleri_TasinirKodId_MalzemeTipi_HareketTipi'
                )
                CREATE UNIQUE INDEX [IX_TasinirKodMuhasebeHesapEslemeleri_TasinirKodId_MalzemeTipi_HareketTipi]
                    ON [muhasebe].[TasinirKodMuhasebeHesapEslemeleri] ([TasinirKodId], [MalzemeTipi], [HareketTipi])
                    WHERE [IsDeleted] = 0 AND [AktifMi] = 1 AND [VarsayilanMi] = 1;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Index may have been created by 20260514090929; drop conditionally
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.indexes i
                    JOIN sys.tables t ON i.object_id = t.object_id
                    JOIN sys.schemas s ON t.schema_id = s.schema_id
                    WHERE s.name = 'muhasebe'
                      AND t.name = 'TasinirKodMuhasebeHesapEslemeleri'
                      AND i.name = 'IX_TasinirKodMuhasebeHesapEslemeleri_TasinirKodId'
                )
                DROP INDEX [IX_TasinirKodMuhasebeHesapEslemeleri_TasinirKodId]
                    ON [muhasebe].[TasinirKodMuhasebeHesapEslemeleri];
            ");

            // Index may have been created by 20260514090929; drop conditionally
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.indexes i
                    JOIN sys.tables t ON i.object_id = t.object_id
                    JOIN sys.schemas s ON t.schema_id = s.schema_id
                    WHERE s.name = 'muhasebe'
                      AND t.name = 'TasinirKodMuhasebeHesapEslemeleri'
                      AND i.name = 'IX_TasinirKodMuhasebeHesapEslemeleri_TasinirKodId_MalzemeTipi_HareketTipi'
                )
                DROP INDEX [IX_TasinirKodMuhasebeHesapEslemeleri_TasinirKodId_MalzemeTipi_HareketTipi]
                    ON [muhasebe].[TasinirKodMuhasebeHesapEslemeleri];
            ");

            migrationBuilder.AlterColumn<string>(
                name: "MalzemeTipi",
                schema: "muhasebe",
                table: "TasinirKodMuhasebeHesapEslemeleri",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "IslemTuru",
                schema: "muhasebe",
                table: "TasinirKodMuhasebeHesapEslemeleri",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "HareketTipi",
                schema: "muhasebe",
                table: "TasinirKodMuhasebeHesapEslemeleri",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.CreateIndex(
                name: "IX_TasinirKodMuhasebeHesapEslemeleri_TasinirKodId_MuhasebeHesapPlaniId_IslemTuru",
                schema: "muhasebe",
                table: "TasinirKodMuhasebeHesapEslemeleri",
                columns: new[] { "TasinirKodId", "MuhasebeHesapPlaniId", "IslemTuru" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
