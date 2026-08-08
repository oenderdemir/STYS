using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Agentler",
                schema: "entegrasyon",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AgentKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    KurumId = table.Column<int>(type: "int", nullable: false),
                    Durum = table.Column<int>(type: "int", nullable: false),
                    AgentVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SonGorulmeTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CihazKimligi = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PublicKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_Agentler", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentCredentialler",
                schema: "entegrasyon",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgentId = table.Column<int>(type: "int", nullable: false),
                    KurumId = table.Column<int>(type: "int", nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ClientSecretHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_AgentCredentialler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentCredentialler_Agentler_AgentId",
                        column: x => x.AgentId,
                        principalSchema: "entegrasyon",
                        principalTable: "Agentler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AgentEnrollments",
                schema: "entegrasyon",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    KurumId = table.Column<int>(type: "int", nullable: false),
                    TesisIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AllowedScopes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KullanimSayisi = table.Column<int>(type: "int", nullable: false),
                    MaxKullanimSayisi = table.Column<int>(type: "int", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Durum = table.Column<int>(type: "int", nullable: false),
                    AgentId = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_AgentEnrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentEnrollments_Agentler_AgentId",
                        column: x => x.AgentId,
                        principalSchema: "entegrasyon",
                        principalTable: "Agentler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AgentTesisler",
                schema: "entegrasyon",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgentId = table.Column<int>(type: "int", nullable: false),
                    KurumId = table.Column<int>(type: "int", nullable: false),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_AgentTesisler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentTesisler_Agentler_AgentId",
                        column: x => x.AgentId,
                        principalSchema: "entegrasyon",
                        principalTable: "Agentler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentCredentialler_AgentId",
                schema: "entegrasyon",
                table: "AgentCredentialler",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentCredentialler_ClientId",
                schema: "entegrasyon",
                table: "AgentCredentialler",
                column: "ClientId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AgentEnrollments_AgentId",
                schema: "entegrasyon",
                table: "AgentEnrollments",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentEnrollments_Code",
                schema: "entegrasyon",
                table: "AgentEnrollments",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Agentler_KurumId_AgentKey",
                schema: "entegrasyon",
                table: "Agentler",
                columns: new[] { "KurumId", "AgentKey" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Agentler_UserId",
                schema: "entegrasyon",
                table: "Agentler",
                column: "UserId",
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AgentTesisler_AgentId_TesisId",
                schema: "entegrasyon",
                table: "AgentTesisler",
                columns: new[] { "AgentId", "TesisId" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentCredentialler",
                schema: "entegrasyon");

            migrationBuilder.DropTable(
                name: "AgentEnrollments",
                schema: "entegrasyon");

            migrationBuilder.DropTable(
                name: "AgentTesisler",
                schema: "entegrasyon");

            migrationBuilder.DropTable(
                name: "Agentler",
                schema: "entegrasyon");
        }
    }
}
