using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class RenameKasaBankaMenuToFinansalHesaplar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE mi
                SET mi.[Label] = N'Finansal Hesaplar'
                FROM [TODBase].[MenuItems] mi
                WHERE mi.[Route] = N'muhasebe/kasa-banka-hesaplari';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE mi
                SET mi.[Label] = N'Kasa/Banka Hesaplari'
                FROM [TODBase].[MenuItems] mi
                WHERE mi.[Route] = N'muhasebe/kasa-banka-hesaplari';
                """);
        }
    }
}
