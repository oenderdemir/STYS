using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentCredentialVersionAndHeartbeat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastHeartbeatAt",
                schema: "entegrasyon",
                table: "Agentler",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyToken",
                schema: "entegrasyon",
                table: "AgentEnrollments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "CredentialVersion",
                schema: "entegrasyon",
                table: "AgentCredentialler",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastHeartbeatAt",
                schema: "entegrasyon",
                table: "Agentler");

            migrationBuilder.DropColumn(
                name: "ConcurrencyToken",
                schema: "entegrasyon",
                table: "AgentEnrollments");

            migrationBuilder.DropColumn(
                name: "CredentialVersion",
                schema: "entegrasyon",
                table: "AgentCredentialler");
        }
    }
}
