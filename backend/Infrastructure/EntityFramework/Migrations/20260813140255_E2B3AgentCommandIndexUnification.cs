using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class E2B3AgentCommandIndexUnification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AgentCommands_AgentId_CommandType_ReleaseId_Apply",
                schema: "entegrasyon",
                table: "AgentCommands");

            migrationBuilder.CreateIndex(
                name: "IX_AgentCommands_AgentId_CommandType_ReleaseId_Apply",
                schema: "entegrasyon",
                table: "AgentCommands",
                columns: new[] { "AgentId", "CommandType", "ReleaseId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [ReleaseId] IS NOT NULL AND [Status] IN (0,1,2,3)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AgentCommands_AgentId_CommandType_ReleaseId_Apply",
                schema: "entegrasyon",
                table: "AgentCommands");

            migrationBuilder.CreateIndex(
                name: "IX_AgentCommands_AgentId_CommandType_ReleaseId_Apply",
                schema: "entegrasyon",
                table: "AgentCommands",
                columns: new[] { "AgentId", "CommandType", "ReleaseId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [ReleaseId] IS NOT NULL AND [CommandType] = 'AgentApplyUpgrade' AND [Status] IN (0,1,2,3)");
        }
    }
}
