using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(StysAppDbContext))]
    [Migration("20260406103000_AddKampBasvuruNoAndPublicTracking")]
    public partial class AddKampBasvuruNoAndPublicTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BasvuruNo",
                schema: "dbo",
                table: "KampBasvurulari",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE dbo.KampBasvurulari
SET BasvuruNo = CONCAT(N'KB', ISNULL(CAST(YEAR(COALESCE(CreatedAt, UpdatedAt, SYSUTCDATETIME())) AS nvarchar(4)), N'0000'), RIGHT(REPLACE(CONVERT(nvarchar(36), NEWID()), N'-', N''), 8))
WHERE BasvuruNo IS NULL;
");

            migrationBuilder.AlterColumn<string>(
                name: "BasvuruNo",
                schema: "dbo",
                table: "KampBasvurulari",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvurulari_BasvuruNo",
                schema: "dbo",
                table: "KampBasvurulari",
                column: "BasvuruNo",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KampBasvurulari_BasvuruNo",
                schema: "dbo",
                table: "KampBasvurulari");

            migrationBuilder.DropColumn(
                name: "BasvuruNo",
                schema: "dbo",
                table: "KampBasvurulari");
        }
    }
}
