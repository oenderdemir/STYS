using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddBildirimTercihleri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KaynakUserAdi",
                schema: "dbo",
                table: "Bildirimler",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "KaynakUserId",
                schema: "dbo",
                table: "Bildirimler",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BildirimTercihleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BildirimlerAktifMi = table.Column<bool>(type: "bit", nullable: false),
                    MinimumSeverity = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    IzinliTiplerJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IzinliKaynaklarJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_BildirimTercihleri", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BildirimTercihleri_UserId",
                schema: "dbo",
                table: "BildirimTercihleri",
                column: "UserId",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BildirimTercihleri",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "KaynakUserAdi",
                schema: "dbo",
                table: "Bildirimler");

            migrationBuilder.DropColumn(
                name: "KaynakUserId",
                schema: "dbo",
                table: "Bildirimler");
        }
    }
}
