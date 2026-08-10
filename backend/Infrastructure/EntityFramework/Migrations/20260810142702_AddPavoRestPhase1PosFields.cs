using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddPavoRestPhase1PosFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcquirerId",
                schema: "entegrasyon",
                table: "PosTerminaller",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcquirerName",
                schema: "entegrasyon",
                table: "PosTerminaller",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TransactionSequence",
                schema: "entegrasyon",
                table: "PosCihazlari",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcquirerId",
                schema: "entegrasyon",
                table: "PosTerminaller");

            migrationBuilder.DropColumn(
                name: "AcquirerName",
                schema: "entegrasyon",
                table: "PosTerminaller");

            migrationBuilder.DropColumn(
                name: "TransactionSequence",
                schema: "entegrasyon",
                table: "PosCihazlari");
        }
    }
}
