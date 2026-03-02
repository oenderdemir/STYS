using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class MakeKullaniciTesisSahiplikTesisIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "TesisId",
                schema: "dbo",
                table: "KullaniciTesisSahiplikleri",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.Sql("""
                UPDATE ks
                SET ks.[TesisId] = NULL
                FROM [dbo].[KullaniciTesisSahiplikleri] ks
                WHERE EXISTS (
                    SELECT 1
                    FROM [dbo].[TesisResepsiyonistleri] tr
                    WHERE tr.[IsDeleted] = 0
                      AND tr.[UserId] = ks.[UserId]
                    GROUP BY tr.[UserId]
                    HAVING COUNT(DISTINCT tr.[TesisId]) > 1
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "TesisId",
                schema: "dbo",
                table: "KullaniciTesisSahiplikleri",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
