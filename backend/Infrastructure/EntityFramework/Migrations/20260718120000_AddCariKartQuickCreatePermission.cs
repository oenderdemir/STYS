using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260718120000_AddCariKartQuickCreatePermission")]
public partial class AddCariKartQuickCreatePermission : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;

            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @ResepsiyonistGroupId uniqueidentifier = (
                SELECT TOP (1) [Id]
                FROM [TODBase].[UserGroups]
                WHERE [Name] = N'ResepsiyonistGrubu' AND [IsDeleted] = 0
                ORDER BY [CreatedAt]
            );

            -- CariKartYonetimi.QuickCreate: genel CariKartYonetimi.Manage yetkisinden bagimsiz,
            -- sadece rezervasyon odeme ekranindaki hizli cari kart olusturma uc noktasi icin.
            IF NOT EXISTS (
                SELECT 1 FROM [TODBase].[Roles]
                WHERE [Domain] = N'CariKartYonetimi' AND [Name] = N'QuickCreate'
            )
                INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (NEWID(), N'QuickCreate', N'CariKartYonetimi', 0, @Now, @Now);

            DECLARE @QuickCreateRoleId uniqueidentifier = (
                SELECT TOP (1) [Id] FROM [TODBase].[Roles]
                WHERE [Domain] = N'CariKartYonetimi' AND [Name] = N'QuickCreate'
            );

            -- Sadece Resepsiyonist grubuna verilir. Admin/TesisYoneticisi/Muhasebe gruplari zaten
            -- CariKartYonetimi.Manage'a sahip; quick-create uc noktasi Manage'i de kabul eder
            -- (bkz. RezervasyonController.CariKartHizliOlustur permission tanimi), bu yuzden bu
            -- gruplara ayrica QuickCreate atamaya gerek yok.
            IF @ResepsiyonistGroupId IS NOT NULL AND NOT EXISTS (
                SELECT 1 FROM [TODBase].[UserGroupRoles]
                WHERE [UserGroupId] = @ResepsiyonistGroupId AND [RoleId] = @QuickCreateRoleId AND [IsDeleted] = 0
            )
                INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (NEWID(), @ResepsiyonistGroupId, @QuickCreateRoleId, 0, @Now, @Now);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;

            DECLARE @QuickCreateRoleId uniqueidentifier = (
                SELECT TOP (1) [Id] FROM [TODBase].[Roles]
                WHERE [Domain] = N'CariKartYonetimi' AND [Name] = N'QuickCreate'
            );

            UPDATE [TODBase].[UserGroupRoles]
            SET [IsDeleted] = 1, [UpdatedAt] = SYSUTCDATETIME()
            WHERE [RoleId] = @QuickCreateRoleId AND [IsDeleted] = 0;

            UPDATE [TODBase].[Roles]
            SET [IsDeleted] = 1, [UpdatedAt] = SYSUTCDATETIME()
            WHERE [Domain] = N'CariKartYonetimi' AND [Name] = N'QuickCreate' AND [IsDeleted] = 0;
            """);
    }
}
