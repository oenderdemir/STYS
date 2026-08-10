using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddPavoRestPhase2PaymentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcquirerId",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AgentCommandId",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BaslatilmaTarihi",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MerchantId",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PavoMessage",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PavoResultCode",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PosCihaziId",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SaleReference",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                type: "nvarchar(96)",
                maxLength: 96,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TerminalId",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PosOdemeIslemleri_KurumId_SaleReference",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                columns: new[] { "KurumId", "SaleReference" },
                unique: true,
                filter: "[SaleReference] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PosOdemeIslemleri_PosCihaziId",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                column: "PosCihaziId");

            migrationBuilder.AddForeignKey(
                name: "FK_PosOdemeIslemleri_PosCihazlari_PosCihaziId",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                column: "PosCihaziId",
                principalSchema: "entegrasyon",
                principalTable: "PosCihazlari",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PosOdemeIslemleri_PosCihazlari_PosCihaziId",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri");

            migrationBuilder.DropIndex(
                name: "IX_PosOdemeIslemleri_KurumId_SaleReference",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri");

            migrationBuilder.DropIndex(
                name: "IX_PosOdemeIslemleri_PosCihaziId",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri");

            migrationBuilder.DropColumn(
                name: "AcquirerId",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri");

            migrationBuilder.DropColumn(
                name: "AgentCommandId",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri");

            migrationBuilder.DropColumn(
                name: "BaslatilmaTarihi",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri");

            migrationBuilder.DropColumn(
                name: "MerchantId",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri");

            migrationBuilder.DropColumn(
                name: "PavoMessage",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri");

            migrationBuilder.DropColumn(
                name: "PavoResultCode",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri");

            migrationBuilder.DropColumn(
                name: "PosCihaziId",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri");

            migrationBuilder.DropColumn(
                name: "SaleReference",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri");

            migrationBuilder.DropColumn(
                name: "TerminalId",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri");
        }
    }
}
