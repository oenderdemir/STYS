using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class E2C1DurableCommandLease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAt",
                schema: "entegrasyon",
                table: "AgentCommands",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LeaseExpiresAt",
                schema: "entegrasyon",
                table: "AgentCommands",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeaseToken",
                schema: "entegrasyon",
                table: "AgentCommands",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                schema: "entegrasyon",
                table: "AgentCommands");

            migrationBuilder.DropColumn(
                name: "LeaseExpiresAt",
                schema: "entegrasyon",
                table: "AgentCommands");

            migrationBuilder.DropColumn(
                name: "LeaseToken",
                schema: "entegrasyon",
                table: "AgentCommands");
        }
    }
}
