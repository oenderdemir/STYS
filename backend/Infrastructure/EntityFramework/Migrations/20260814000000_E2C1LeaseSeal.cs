using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

public partial class E2C1LeaseSeal : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_AgentCommands_AgentId_IdempotencyKey",
            schema: "entegrasyon",
            table: "AgentCommands");

        migrationBuilder.CreateIndex(
            name: "IX_AgentCommands_AgentId_IdempotencyKey",
            schema: "entegrasyon",
            table: "AgentCommands",
            columns: new[] { "AgentId", "IdempotencyKey" },
            unique: true,
            filter: "[IdempotencyKey] <> '' AND [IsDeleted] = 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_AgentCommands_AgentId_IdempotencyKey",
            schema: "entegrasyon",
            table: "AgentCommands");

        migrationBuilder.CreateIndex(
            name: "IX_AgentCommands_AgentId_IdempotencyKey",
            schema: "entegrasyon",
            table: "AgentCommands",
            columns: new[] { "AgentId", "IdempotencyKey" },
            filter: "[IdempotencyKey] <> '' AND [IsDeleted] = 0");
    }
}
