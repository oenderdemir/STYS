using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    [DbContext(typeof(StysAppDbContext))]
    [Migration("20260306123500_AddTesisSaatleri")]
    public partial class AddTesisSaatleri : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "CikisSaati",
                schema: "dbo",
                table: "Tesisler",
                type: "time(0)",
                nullable: false,
                defaultValue: new TimeSpan(10, 0, 0));

            migrationBuilder.AddColumn<TimeSpan>(
                name: "GirisSaati",
                schema: "dbo",
                table: "Tesisler",
                type: "time(0)",
                nullable: false,
                defaultValue: new TimeSpan(14, 0, 0));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CikisSaati",
                schema: "dbo",
                table: "Tesisler");

            migrationBuilder.DropColumn(
                name: "GirisSaati",
                schema: "dbo",
                table: "Tesisler");
        }
    }
}
