using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class E2D41EnrollmentLifecycleClosure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AgentEnrollmentRequiresApproval",
                schema: "dbo",
                table: "Kurumlar",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                schema: "entegrasyon",
                table: "Agentler",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedBy",
                schema: "entegrasyon",
                table: "Agentler",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAt",
                schema: "entegrasyon",
                table: "Agentler",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectedBy",
                schema: "entegrasyon",
                table: "Agentler",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationNonceHash",
                schema: "entegrasyon",
                table: "AgentEnrollments",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgentEnrollmentRequiresApproval",
                schema: "dbo",
                table: "Kurumlar");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                schema: "entegrasyon",
                table: "Agentler");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                schema: "entegrasyon",
                table: "Agentler");

            migrationBuilder.DropColumn(
                name: "RejectedAt",
                schema: "entegrasyon",
                table: "Agentler");

            migrationBuilder.DropColumn(
                name: "RejectedBy",
                schema: "entegrasyon",
                table: "Agentler");

            migrationBuilder.DropColumn(
                name: "RegistrationNonceHash",
                schema: "entegrasyon",
                table: "AgentEnrollments");
        }
    }
}
