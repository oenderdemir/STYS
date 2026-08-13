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
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes i
                    INNER JOIN sys.objects o ON o.object_id = i.object_id
                    INNER JOIN sys.schemas s ON s.schema_id = o.schema_id
                    WHERE s.name = N'entegrasyon'
                      AND o.name = N'AgentCommands'
                      AND i.name = N'IX_AgentCommands_AgentId_CommandType_ReleaseId_Apply'
                )
                BEGIN
                    DROP INDEX [IX_AgentCommands_AgentId_CommandType_ReleaseId_Apply] ON [entegrasyon].[AgentCommands];
                END
                """);

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
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes i
                    INNER JOIN sys.objects o ON o.object_id = i.object_id
                    INNER JOIN sys.schemas s ON s.schema_id = o.schema_id
                    WHERE s.name = N'entegrasyon'
                      AND o.name = N'AgentCommands'
                      AND i.name = N'IX_AgentCommands_AgentId_CommandType_ReleaseId_Apply'
                )
                BEGIN
                    DROP INDEX [IX_AgentCommands_AgentId_CommandType_ReleaseId_Apply] ON [entegrasyon].[AgentCommands];
                END
                """);

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
