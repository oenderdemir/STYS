using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddKurumTenantBranding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TenantKey",
                schema: "dbo",
                table: "Kurumlar",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LoginHost",
                schema: "dbo",
                table: "Kurumlar",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Kurumlar_TenantKey",
                schema: "dbo",
                table: "Kurumlar",
                column: "TenantKey",
                unique: true,
                filter: "[TenantKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Kurumlar_LoginHost",
                schema: "dbo",
                table: "Kurumlar",
                column: "LoginHost",
                unique: true,
                filter: "[LoginHost] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Kurumlar_TenantKey",
                schema: "dbo",
                table: "Kurumlar");

            migrationBuilder.DropIndex(
                name: "IX_Kurumlar_LoginHost",
                schema: "dbo",
                table: "Kurumlar");

            migrationBuilder.DropColumn(
                name: "TenantKey",
                schema: "dbo",
                table: "Kurumlar");

            migrationBuilder.DropColumn(
                name: "LoginHost",
                schema: "dbo",
                table: "Kurumlar");
        }
    }
}
