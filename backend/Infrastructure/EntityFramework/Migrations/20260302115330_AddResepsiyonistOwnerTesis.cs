using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddResepsiyonistOwnerTesis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KullaniciTesisSahipliklari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TesisId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_KullaniciTesisSahipliklari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KullaniciTesisSahipliklari_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KullaniciTesisSahipliklari_TesisId",
                schema: "dbo",
                table: "KullaniciTesisSahipliklari",
                column: "TesisId");

            migrationBuilder.CreateIndex(
                name: "IX_KullaniciTesisSahipliklari_UserId",
                schema: "dbo",
                table: "KullaniciTesisSahipliklari",
                column: "UserId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.Sql("""
                INSERT INTO [dbo].[KullaniciTesisSahipliklari] ([UserId], [TesisId], [IsDeleted], [CreatedAt], [CreatedBy])
                SELECT
                    src.[UserId],
                    src.[TesisId],
                    0,
                    SYSUTCDATETIME(),
                    'migration'
                FROM (
                    SELECT
                        tr.[UserId],
                        MIN(tr.[TesisId]) AS [TesisId]
                    FROM [dbo].[TesisResepsiyonistleri] tr
                    WHERE tr.[IsDeleted] = 0
                    GROUP BY tr.[UserId]
                ) src
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[KullaniciTesisSahipliklari] ks
                    WHERE ks.[UserId] = src.[UserId]
                      AND ks.[IsDeleted] = 0
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KullaniciTesisSahipliklari",
                schema: "dbo");
        }
    }
}
