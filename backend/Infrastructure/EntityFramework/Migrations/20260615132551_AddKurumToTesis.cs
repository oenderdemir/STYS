using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddKurumToTesis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tesisler_IlId_Ad",
                schema: "dbo",
                table: "Tesisler");

            migrationBuilder.AddColumn<int>(
                name: "KurumId",
                schema: "dbo",
                table: "Tesisler",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [dbo].[Kurumlar] WHERE [Kod] = N'DEFAULT' AND [IsDeleted] = 0)
BEGIN
    INSERT INTO [dbo].[Kurumlar]
        ([Kod], [Ad], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [DeletedAt], [CreatedBy], [UpdatedBy], [DeletedBy])
    VALUES
        (N'DEFAULT', N'Varsayilan Kurum', 1, 0, SYSUTCDATETIME(), NULL, NULL, N'migration', NULL, NULL)
END
");

            migrationBuilder.Sql(@"
DECLARE @DefaultKurumId int;

SELECT TOP 1 @DefaultKurumId = [Id]
FROM [dbo].[Kurumlar]
WHERE [Kod] = N'DEFAULT' AND [IsDeleted] = 0
ORDER BY [Id];

UPDATE [dbo].[Tesisler]
SET [KurumId] = @DefaultKurumId
WHERE [KurumId] IS NULL;
");

            migrationBuilder.AlterColumn<int>(
                name: "KurumId",
                schema: "dbo",
                table: "Tesisler",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tesisler_IlId",
                schema: "dbo",
                table: "Tesisler",
                column: "IlId");

            migrationBuilder.CreateIndex(
                name: "IX_Tesisler_KurumId",
                schema: "dbo",
                table: "Tesisler",
                column: "KurumId");

            migrationBuilder.CreateIndex(
                name: "IX_Tesisler_KurumId_IlId_Ad",
                schema: "dbo",
                table: "Tesisler",
                columns: new[] { "KurumId", "IlId", "Ad" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_Tesisler_Kurumlar_KurumId",
                schema: "dbo",
                table: "Tesisler",
                column: "KurumId",
                principalSchema: "dbo",
                principalTable: "Kurumlar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tesisler_Kurumlar_KurumId",
                schema: "dbo",
                table: "Tesisler");

            migrationBuilder.DropIndex(
                name: "IX_Tesisler_IlId",
                schema: "dbo",
                table: "Tesisler");

            migrationBuilder.DropIndex(
                name: "IX_Tesisler_KurumId",
                schema: "dbo",
                table: "Tesisler");

            migrationBuilder.DropIndex(
                name: "IX_Tesisler_KurumId_IlId_Ad",
                schema: "dbo",
                table: "Tesisler");

            migrationBuilder.DropColumn(
                name: "KurumId",
                schema: "dbo",
                table: "Tesisler");

            migrationBuilder.CreateIndex(
                name: "IX_Tesisler_IlId_Ad",
                schema: "dbo",
                table: "Tesisler",
                columns: new[] { "IlId", "Ad" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");
        }
    }
}
