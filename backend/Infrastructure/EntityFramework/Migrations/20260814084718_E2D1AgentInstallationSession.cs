using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class E2D1AgentInstallationSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AgentInstallationSessionId",
                schema: "entegrasyon",
                table: "AgentEnrollments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AgentInstallationSessions",
                schema: "entegrasyon",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KurumId = table.Column<int>(type: "int", nullable: false),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    AgentDisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TargetRid = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Scopes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    EnrolledAgentId = table.Column<int>(type: "int", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_AgentInstallationSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentInstallationSessions_Agentler_EnrolledAgentId",
                        column: x => x.EnrolledAgentId,
                        principalSchema: "entegrasyon",
                        principalTable: "Agentler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentEnrollments_AgentInstallationSessionId",
                schema: "entegrasyon",
                table: "AgentEnrollments",
                column: "AgentInstallationSessionId",
                unique: true,
                filter: "[IsDeleted] = 0 AND [AgentInstallationSessionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AgentInstallationSessions_EnrolledAgentId",
                schema: "entegrasyon",
                table: "AgentInstallationSessions",
                column: "EnrolledAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentInstallationSessions_KurumId_TesisId_Status",
                schema: "entegrasyon",
                table: "AgentInstallationSessions",
                columns: new[] { "KurumId", "TesisId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_AgentEnrollments_AgentInstallationSessions_AgentInstallationSessionId",
                schema: "entegrasyon",
                table: "AgentEnrollments",
                column: "AgentInstallationSessionId",
                principalSchema: "entegrasyon",
                principalTable: "AgentInstallationSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgentEnrollments_AgentInstallationSessions_AgentInstallationSessionId",
                schema: "entegrasyon",
                table: "AgentEnrollments");

            migrationBuilder.DropTable(
                name: "AgentInstallationSessions",
                schema: "entegrasyon");

            migrationBuilder.DropIndex(
                name: "IX_AgentEnrollments_AgentInstallationSessionId",
                schema: "entegrasyon",
                table: "AgentEnrollments");

            migrationBuilder.DropColumn(
                name: "AgentInstallationSessionId",
                schema: "entegrasyon",
                table: "AgentEnrollments");
        }
    }
}
