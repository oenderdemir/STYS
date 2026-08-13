using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class E2B1AgentReleaseCorrelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RuntimeIdentifier",
                schema: "entegrasyon",
                table: "Agentler",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReleaseId",
                schema: "entegrasyon",
                table: "AgentCommands",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AgentReleases",
                schema: "entegrasyon",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KurumId = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ContractVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RuntimeIdentifier = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Signature = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    PackageSize = table.Column<long>(type: "bigint", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    ReleaseNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PackagePath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
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
                    table.PrimaryKey("PK_AgentReleases", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentCommands_AgentId_CommandType_ReleaseId",
                schema: "entegrasyon",
                table: "AgentCommands",
                columns: new[] { "AgentId", "CommandType", "ReleaseId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [ReleaseId] IS NOT NULL AND [CommandType] = 'AgentStageUpgrade' AND [Status] IN (0,1,2,3)");

            migrationBuilder.CreateIndex(
                name: "IX_AgentReleases_KurumId_Enabled_PublishedAt",
                schema: "entegrasyon",
                table: "AgentReleases",
                columns: new[] { "KurumId", "Enabled", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentReleases_KurumId_RuntimeIdentifier_Version",
                schema: "entegrasyon",
                table: "AgentReleases",
                columns: new[] { "KurumId", "RuntimeIdentifier", "Version" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentReleases",
                schema: "entegrasyon");

            migrationBuilder.DropIndex(
                name: "IX_AgentCommands_AgentId_CommandType_ReleaseId",
                schema: "entegrasyon",
                table: "AgentCommands");

            migrationBuilder.DropColumn(
                name: "RuntimeIdentifier",
                schema: "entegrasyon",
                table: "Agentler");

            migrationBuilder.DropColumn(
                name: "ReleaseId",
                schema: "entegrasyon",
                table: "AgentCommands");
        }
    }
}
