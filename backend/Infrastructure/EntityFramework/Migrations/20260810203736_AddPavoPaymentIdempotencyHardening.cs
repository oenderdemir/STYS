using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddPavoPaymentIdempotencyHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PosOdemeIslemleri_KurumId_SaleReference",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri");

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PosOdemeIslemleri_IdempotencyKey",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PosOdemeIslemleri_SaleReference",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                column: "SaleReference",
                unique: true,
                filter: "[SaleReference] IS NOT NULL AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PosOdemeIslemleri_IdempotencyKey",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri");

            migrationBuilder.DropIndex(
                name: "IX_PosOdemeIslemleri_SaleReference",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri");

            migrationBuilder.CreateIndex(
                name: "IX_PosOdemeIslemleri_KurumId_SaleReference",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                columns: new[] { "KurumId", "SaleReference" },
                unique: true,
                filter: "[SaleReference] IS NOT NULL");
        }
    }
}
