using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class D2PavoRuntimeHealth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastHealthCheckAt",
                schema: "entegrasyon",
                table: "PosCihazlari",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastHealthError",
                schema: "entegrasyon",
                table: "PosCihazlari",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastHealthStatus",
                schema: "entegrasyon",
                table: "PosCihazlari",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastHealthSuccessAt",
                schema: "entegrasyon",
                table: "PosCihazlari",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastHealthCheckAt",
                schema: "entegrasyon",
                table: "PosCihazlari");

            migrationBuilder.DropColumn(
                name: "LastHealthError",
                schema: "entegrasyon",
                table: "PosCihazlari");

            migrationBuilder.DropColumn(
                name: "LastHealthStatus",
                schema: "entegrasyon",
                table: "PosCihazlari");

            migrationBuilder.DropColumn(
                name: "LastHealthSuccessAt",
                schema: "entegrasyon",
                table: "PosCihazlari");
        }
    }
}
