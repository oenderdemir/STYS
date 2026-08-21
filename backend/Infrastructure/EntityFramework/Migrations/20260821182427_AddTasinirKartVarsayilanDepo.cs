using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddTasinirKartVarsayilanDepo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VarsayilanDepoId",
                schema: "muhasebe",
                table: "TasinirKartlar",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TasinirKartlar_VarsayilanDepoId",
                schema: "muhasebe",
                table: "TasinirKartlar",
                column: "VarsayilanDepoId",
                filter: "[IsDeleted] = 0 AND [VarsayilanDepoId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_TasinirKartlar_Depolar_VarsayilanDepoId",
                schema: "muhasebe",
                table: "TasinirKartlar",
                column: "VarsayilanDepoId",
                principalSchema: "muhasebe",
                principalTable: "Depolar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TasinirKartlar_Depolar_VarsayilanDepoId",
                schema: "muhasebe",
                table: "TasinirKartlar");

            migrationBuilder.DropIndex(
                name: "IX_TasinirKartlar_VarsayilanDepoId",
                schema: "muhasebe",
                table: "TasinirKartlar");

            migrationBuilder.DropColumn(
                name: "VarsayilanDepoId",
                schema: "muhasebe",
                table: "TasinirKartlar");
        }
    }
}
