using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddCostMethodToStokMaliyetKatmanlari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MaliyetYontemi",
                schema: "muhasebe",
                table: "StokMaliyetKatmanlari",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE katman
                SET [MaliyetYontemi] = politika.[MaliyetYontemi]
                FROM [muhasebe].[StokMaliyetKatmanlari] AS katman
                INNER JOIN [muhasebe].[MuhasebeDonemler] AS donem
                    ON donem.[TesisId] = katman.[TesisId]
                   AND donem.[IsDeleted] = 0
                   AND donem.[BaslangicTarihi] <= katman.[GirisTarihi]
                   AND donem.[BitisTarihi] >= katman.[GirisTarihi]
                INNER JOIN [muhasebe].[StokMaliyetPolitikalari] AS politika
                    ON politika.[TesisId] = donem.[TesisId]
                   AND politika.[MaliYil] = donem.[MaliYil]
                   AND politika.[IsDeleted] = 0
                   AND politika.[MaliyetYontemi] IN (N'FIFO', N'LIFO')
                WHERE katman.[MaliyetYontemi] IS NULL
                   OR katman.[MaliyetYontemi] = N'';
                """);

            migrationBuilder.Sql("""
                -- LIFO desteği eklenmeden önce üretilen legacy maliyet katmanları yalnız FIFO idi.
                UPDATE [muhasebe].[StokMaliyetKatmanlari]
                SET [MaliyetYontemi] = N'FIFO'
                WHERE [MaliyetYontemi] IS NULL
                   OR [MaliyetYontemi] = N'';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "MaliyetYontemi",
                schema: "muhasebe",
                table: "StokMaliyetKatmanlari",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_StokMaliyetKatmanlari_MaliyetYontemi",
                schema: "muhasebe",
                table: "StokMaliyetKatmanlari",
                sql: "[MaliyetYontemi] IN (N'FIFO', N'LIFO')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StokMaliyetKatmanlari_MaliyetYontemi",
                schema: "muhasebe",
                table: "StokMaliyetKatmanlari");

            migrationBuilder.DropColumn(
                name: "MaliyetYontemi",
                schema: "muhasebe",
                table: "StokMaliyetKatmanlari");
        }
    }
}
