using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddKampProgramiIdToKampKonaklamaTarifeleri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KampKonaklamaTarifeleri_Kod",
                schema: "dbo",
                table: "KampKonaklamaTarifeleri");

            migrationBuilder.AddColumn<int>(
                name: "KampProgramiId",
                schema: "dbo",
                table: "KampKonaklamaTarifeleri",
                type: "int",
                nullable: true);

            // Get the first active program to assign existing tarifeleri
            migrationBuilder.Sql(@"
                UPDATE [dbo].[KampKonaklamaTarifeleri]
                SET [KampProgramiId] = (
                    SELECT TOP 1 [Id] FROM [dbo].[KampProgramlari]
                    WHERE [IsDeleted] = 0 AND [AktifMi] = 1
                    ORDER BY [Id]
                )
                WHERE [KampProgramiId] IS NULL;
            ");

            // For records that don't have a program yet, assign to first program (even if inactive)
            migrationBuilder.Sql(@"
                UPDATE [dbo].[KampKonaklamaTarifeleri]
                SET [KampProgramiId] = (
                    SELECT TOP 1 [Id] FROM [dbo].[KampProgramlari]
                    WHERE [IsDeleted] = 0
                    ORDER BY [Id]
                )
                WHERE [KampProgramiId] IS NULL;
            ");

            // Make the column NOT NULL
            migrationBuilder.AlterColumn<int>(
                name: "KampProgramiId",
                schema: "dbo",
                table: "KampKonaklamaTarifeleri",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_KampKonaklamaTarifeleri_KampProgramiId_Kod",
                schema: "dbo",
                table: "KampKonaklamaTarifeleri",
                columns: new[] { "KampProgramiId", "Kod" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_KampKonaklamaTarifeleri_KampProgramlari_KampProgramiId",
                schema: "dbo",
                table: "KampKonaklamaTarifeleri",
                column: "KampProgramiId",
                principalSchema: "dbo",
                principalTable: "KampProgramlari",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KampKonaklamaTarifeleri_KampProgramlari_KampProgramiId",
                schema: "dbo",
                table: "KampKonaklamaTarifeleri");

            migrationBuilder.DropIndex(
                name: "IX_KampKonaklamaTarifeleri_KampProgramiId_Kod",
                schema: "dbo",
                table: "KampKonaklamaTarifeleri");

            migrationBuilder.DropColumn(
                name: "KampProgramiId",
                schema: "dbo",
                table: "KampKonaklamaTarifeleri");

            migrationBuilder.CreateIndex(
                name: "IX_KampKonaklamaTarifeleri_Kod",
                schema: "dbo",
                table: "KampKonaklamaTarifeleri",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
