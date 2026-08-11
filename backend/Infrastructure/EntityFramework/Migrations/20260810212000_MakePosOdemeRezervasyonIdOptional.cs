using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    public partial class MakePosOdemeRezervasyonIdOptional : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PosOdemeIslemleri_Rezervasyonlar_RezervasyonId",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri");

            migrationBuilder.AlterColumn<int>(
                name: "RezervasyonId",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_PosOdemeIslemleri_Rezervasyonlar_RezervasyonId",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                column: "RezervasyonId",
                principalSchema: "dbo",
                principalTable: "Rezervasyonlar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PosOdemeIslemleri_Rezervasyonlar_RezervasyonId",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri");

            migrationBuilder.AlterColumn<int>(
                name: "RezervasyonId",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PosOdemeIslemleri_Rezervasyonlar_RezervasyonId",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                column: "RezervasyonId",
                principalSchema: "dbo",
                principalTable: "Rezervasyonlar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
