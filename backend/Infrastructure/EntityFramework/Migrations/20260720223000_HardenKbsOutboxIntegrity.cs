using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class HardenKbsOutboxIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "dbo",
                table: "RezervasyonKonaklayanlar",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "ProtectedPayload",
                schema: "dbo",
                table: "KbsBildirimler",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE [dbo].[KbsBildirimler]
                SET [Durum] = N'MudahaleGerekli',
                    [SonHataKodu] = N'LEGACY-PAYLOAD-MISSING',
                    [SonHataMesaji] = N'Bu kayit guvenli payload snapshot oncesinde olusturuldu; manuel inceleme gerekli.'
                WHERE [ProtectedPayload] = N'' AND [IsDeleted] = 0;

                DECLARE @Now datetime2=SYSUTCDATETIME(), @Admin uniqueidentifier='22222222-2222-2222-2222-222222222201', @Role uniqueidentifier='7b510000-0000-0000-0000-000000000017';
                IF NOT EXISTS(SELECT 1 FROM [TODBase].[Roles] WHERE Id=@Role)
                    INSERT [TODBase].[Roles](Id,Name,Domain,IsDeleted,CreatedAt,UpdatedAt) VALUES(@Role,N'Reconciliation',N'KbsYonetimi',0,@Now,@Now);
                IF EXISTS(SELECT 1 FROM [TODBase].[UserGroups] WHERE Id=@Admin)
                   AND NOT EXISTS(SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE UserGroupId=@Admin AND RoleId=@Role)
                    INSERT [TODBase].[UserGroupRoles](Id,UserGroupId,RoleId,IsDeleted,CreatedAt,UpdatedAt) VALUES(NEWID(),@Admin,@Role,0,@Now,@Now);
                """);

            migrationBuilder.CreateTable(
                name: "KbsDurumGecmisleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KurumId = table.Column<int>(type: "int", nullable: false),
                    KbsBildirimId = table.Column<long>(type: "bigint", nullable: false),
                    OncekiDurum = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    YeniDurum = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IslemTipi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    KurumReferansNo = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IslemYapanUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IslemYapanUserAdi = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IslemTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KbsDurumGecmisleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KbsDurumGecmisleri_KbsBildirimler_KbsBildirimId",
                        column: x => x.KbsBildirimId,
                        principalSchema: "dbo",
                        principalTable: "KbsBildirimler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KbsDurumGecmisleri_KbsBildirimId_IslemTarihi",
                schema: "dbo",
                table: "KbsDurumGecmisleri",
                columns: new[] { "KbsBildirimId", "IslemTarihi" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM [TODBase].[UserGroupRoles] WHERE RoleId='7b510000-0000-0000-0000-000000000017';
                DELETE FROM [TODBase].[Roles] WHERE Id='7b510000-0000-0000-0000-000000000017';
                """);
            migrationBuilder.DropTable(
                name: "KbsDurumGecmisleri",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "dbo",
                table: "RezervasyonKonaklayanlar");

            migrationBuilder.DropColumn(
                name: "ProtectedPayload",
                schema: "dbo",
                table: "KbsBildirimler");
        }
    }
}
