using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TOD.Platform.Identity.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddUserKurumEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "UserKurums",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KurumId = table.Column<int>(type: "int", nullable: false),
                    VarsayilanMi = table.Column<bool>(type: "bit", nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsKurumAdmin = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_UserKurums", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserKurums_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "TODBase",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserKurums_KurumId",
                schema: "identity",
                table: "UserKurums",
                column: "KurumId",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_UserKurums_UserId_KurumId",
                schema: "identity",
                table: "UserKurums",
                columns: new[] { "UserId", "KurumId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_UserKurums_UserId_VarsayilanMi",
                schema: "identity",
                table: "UserKurums",
                columns: new[] { "UserId", "VarsayilanMi" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [VarsayilanMi] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserKurums",
                schema: "identity");
        }
    }
}
