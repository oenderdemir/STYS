using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddRezervasyonKonaklayanPlani : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RezervasyonKonaklayanlar",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RezervasyonId = table.Column<int>(type: "int", nullable: false),
                    SiraNo = table.Column<int>(type: "int", nullable: false),
                    AdSoyad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TcKimlikNo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    PasaportNo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
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
                    table.PrimaryKey("PK_RezervasyonKonaklayanlar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RezervasyonKonaklayanlar_Rezervasyonlar_RezervasyonId",
                        column: x => x.RezervasyonId,
                        principalSchema: "dbo",
                        principalTable: "Rezervasyonlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RezervasyonKonaklayanSegmentAtamalari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RezervasyonKonaklayanId = table.Column<int>(type: "int", nullable: false),
                    RezervasyonSegmentId = table.Column<int>(type: "int", nullable: false),
                    OdaId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_RezervasyonKonaklayanSegmentAtamalari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RezervasyonKonaklayanSegmentAtamalari_Odalar_OdaId",
                        column: x => x.OdaId,
                        principalSchema: "dbo",
                        principalTable: "Odalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RezervasyonKonaklayanSegmentAtamalari_RezervasyonKonaklayanlar_RezervasyonKonaklayanId",
                        column: x => x.RezervasyonKonaklayanId,
                        principalSchema: "dbo",
                        principalTable: "RezervasyonKonaklayanlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RezervasyonKonaklayanSegmentAtamalari_RezervasyonSegmentleri_RezervasyonSegmentId",
                        column: x => x.RezervasyonSegmentId,
                        principalSchema: "dbo",
                        principalTable: "RezervasyonSegmentleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonKonaklayanlar_RezervasyonId_SiraNo",
                schema: "dbo",
                table: "RezervasyonKonaklayanlar",
                columns: new[] { "RezervasyonId", "SiraNo" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonKonaklayanSegmentAtamalari_OdaId",
                schema: "dbo",
                table: "RezervasyonKonaklayanSegmentAtamalari",
                column: "OdaId");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonKonaklayanSegmentAtamalari_RezervasyonKonaklayanId_RezervasyonSegmentId",
                schema: "dbo",
                table: "RezervasyonKonaklayanSegmentAtamalari",
                columns: new[] { "RezervasyonKonaklayanId", "RezervasyonSegmentId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonKonaklayanSegmentAtamalari_RezervasyonSegmentId_OdaId",
                schema: "dbo",
                table: "RezervasyonKonaklayanSegmentAtamalari",
                columns: new[] { "RezervasyonSegmentId", "OdaId" },
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RezervasyonKonaklayanSegmentAtamalari",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RezervasyonKonaklayanlar",
                schema: "dbo");
        }
    }
}
