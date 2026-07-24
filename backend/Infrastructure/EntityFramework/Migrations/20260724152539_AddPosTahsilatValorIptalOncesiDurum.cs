using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddPosTahsilatValorIptalOncesiDurum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IptalOncesiDurum",
                schema: "muhasebe",
                table: "PosTahsilatValorleri",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IptalOncesiDurum",
                schema: "muhasebe",
                table: "PosTahsilatValorleri");
        }
    }
}
