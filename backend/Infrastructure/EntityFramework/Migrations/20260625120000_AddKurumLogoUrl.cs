using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddKurumLogoUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LogoDosyaAdi",
                schema: "dbo",
                table: "Kurumlar",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoOrijinalDosyaAdi",
                schema: "dbo",
                table: "Kurumlar",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoContentType",
                schema: "dbo",
                table: "Kurumlar",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LogoBoyut",
                schema: "dbo",
                table: "Kurumlar",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LogoYuklenmeTarihi",
                schema: "dbo",
                table: "Kurumlar",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LogoDosyaAdi",
                schema: "dbo",
                table: "Kurumlar");

            migrationBuilder.DropColumn(
                name: "LogoOrijinalDosyaAdi",
                schema: "dbo",
                table: "Kurumlar");

            migrationBuilder.DropColumn(
                name: "LogoContentType",
                schema: "dbo",
                table: "Kurumlar");

            migrationBuilder.DropColumn(
                name: "LogoBoyut",
                schema: "dbo",
                table: "Kurumlar");

            migrationBuilder.DropColumn(
                name: "LogoYuklenmeTarihi",
                schema: "dbo",
                table: "Kurumlar");
        }
    }
}
