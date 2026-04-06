using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260406170000_MakeKampKuralSetleriProgramScoped")]
public partial class MakeKampKuralSetleriProgramScoped : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_KampKuralSetleri_KampYili",
            schema: "dbo",
            table: "KampKuralSetleri");

        migrationBuilder.AddColumn<int>(
            name: "KampProgramiId",
            schema: "dbo",
            table: "KampKuralSetleri",
            type: "int",
            nullable: true);

        migrationBuilder.Sql(
            """
            IF EXISTS (SELECT 1 FROM [dbo].[KampKuralSetleri] WHERE [KampProgramiId] IS NULL)
            BEGIN
                IF EXISTS (SELECT 1 FROM [dbo].[KampProgramlari] WHERE [IsDeleted] = 0 AND [AktifMi] = 1)
                BEGIN
                    INSERT INTO [dbo].[KampKuralSetleri]
                        ([KampProgramiId], [KampYili], [OncekiYilSayisi], [KatilimCezaPuani], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [DeletedAt], [CreatedBy], [UpdatedBy], [DeletedBy])
                    SELECT
                        p.[Id],
                        ks.[KampYili],
                        ks.[OncekiYilSayisi],
                        ks.[KatilimCezaPuani],
                        ks.[AktifMi],
                        ks.[IsDeleted],
                        ks.[CreatedAt],
                        ks.[UpdatedAt],
                        ks.[DeletedAt],
                        ks.[CreatedBy],
                        ks.[UpdatedBy],
                        ks.[DeletedBy]
                    FROM [dbo].[KampKuralSetleri] ks
                    CROSS JOIN [dbo].[KampProgramlari] p
                    WHERE ks.[KampProgramiId] IS NULL
                      AND p.[IsDeleted] = 0
                      AND p.[AktifMi] = 1;

                    DELETE FROM [dbo].[KampKuralSetleri]
                    WHERE [KampProgramiId] IS NULL;
                END
                ELSE
                BEGIN
                    DECLARE @FallbackProgramId INT = (SELECT TOP 1 [Id] FROM [dbo].[KampProgramlari] WHERE [IsDeleted] = 0 ORDER BY [Id]);
                    IF @FallbackProgramId IS NULL
                        THROW 50000, N'KampKuralSetleri donusumu icin kamp programi bulunamadi.', 1;

                    UPDATE [dbo].[KampKuralSetleri]
                    SET [KampProgramiId] = @FallbackProgramId
                    WHERE [KampProgramiId] IS NULL;
                END
            END
            """);

        migrationBuilder.AlterColumn<int>(
            name: "KampProgramiId",
            schema: "dbo",
            table: "KampKuralSetleri",
            type: "int",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "int",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_KampKuralSetleri_KampProgramiId_KampYili",
            schema: "dbo",
            table: "KampKuralSetleri",
            columns: new[] { "KampProgramiId", "KampYili" },
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.AddForeignKey(
            name: "FK_KampKuralSetleri_KampProgramlari_KampProgramiId",
            schema: "dbo",
            table: "KampKuralSetleri",
            column: "KampProgramiId",
            principalSchema: "dbo",
            principalTable: "KampProgramlari",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_KampKuralSetleri_KampProgramlari_KampProgramiId",
            schema: "dbo",
            table: "KampKuralSetleri");

        migrationBuilder.DropIndex(
            name: "IX_KampKuralSetleri_KampProgramiId_KampYili",
            schema: "dbo",
            table: "KampKuralSetleri");

        migrationBuilder.Sql(
            """
            WITH Tekil AS
            (
                SELECT
                    [KampYili],
                    MIN([Id]) AS [Id]
                FROM [dbo].[KampKuralSetleri]
                WHERE [IsDeleted] = 0
                GROUP BY [KampYili]
            )
            DELETE ks
            FROM [dbo].[KampKuralSetleri] ks
            LEFT JOIN Tekil t ON t.[Id] = ks.[Id]
            WHERE t.[Id] IS NULL;
            """);

        migrationBuilder.DropColumn(
            name: "KampProgramiId",
            schema: "dbo",
            table: "KampKuralSetleri");

        migrationBuilder.CreateIndex(
            name: "IX_KampKuralSetleri_KampYili",
            schema: "dbo",
            table: "KampKuralSetleri",
            column: "KampYili",
            unique: true,
            filter: "[IsDeleted] = 0");
    }
}
