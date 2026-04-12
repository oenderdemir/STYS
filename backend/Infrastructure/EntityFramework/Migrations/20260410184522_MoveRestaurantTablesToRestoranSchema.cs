using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class MoveRestaurantTablesToRestoranSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "restoran");

            migrationBuilder.RenameTable(
                name: "RestoranSiparisleri",
                schema: "dbo",
                newName: "RestoranSiparisleri",
                newSchema: "restoran");

            migrationBuilder.RenameTable(
                name: "RestoranSiparisKalemleri",
                schema: "dbo",
                newName: "RestoranSiparisKalemleri",
                newSchema: "restoran");

            migrationBuilder.RenameTable(
                name: "RestoranOdemeleri",
                schema: "dbo",
                newName: "RestoranOdemeleri",
                newSchema: "restoran");

            migrationBuilder.RenameTable(
                name: "RestoranMenuUrunleri",
                schema: "dbo",
                newName: "RestoranMenuUrunleri",
                newSchema: "restoran");

            migrationBuilder.RenameTable(
                name: "RestoranMenuKategorileri",
                schema: "dbo",
                newName: "RestoranMenuKategorileri",
                newSchema: "restoran");

            migrationBuilder.RenameTable(
                name: "RestoranMasalari",
                schema: "dbo",
                newName: "RestoranMasalari",
                newSchema: "restoran");

            migrationBuilder.RenameTable(
                name: "Restoranlar",
                schema: "dbo",
                newName: "Restoranlar",
                newSchema: "restoran");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "RestoranSiparisleri",
                schema: "restoran",
                newName: "RestoranSiparisleri",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "RestoranSiparisKalemleri",
                schema: "restoran",
                newName: "RestoranSiparisKalemleri",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "RestoranOdemeleri",
                schema: "restoran",
                newName: "RestoranOdemeleri",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "RestoranMenuUrunleri",
                schema: "restoran",
                newName: "RestoranMenuUrunleri",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "RestoranMenuKategorileri",
                schema: "restoran",
                newName: "RestoranMenuKategorileri",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "RestoranMasalari",
                schema: "restoran",
                newName: "RestoranMasalari",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "Restoranlar",
                schema: "restoran",
                newName: "Restoranlar",
                newSchema: "dbo");
        }
    }
}
