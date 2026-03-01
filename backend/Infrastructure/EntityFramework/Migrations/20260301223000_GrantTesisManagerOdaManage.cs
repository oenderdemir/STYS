using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    [DbContext(typeof(StysAppDbContext))]
    [Migration("20260301223000_GrantTesisManagerOdaManage")]
    public partial class GrantTesisManagerOdaManage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @Now datetime2 = SYSUTCDATETIME();

                DECLARE @TesisYoneticiGrupId uniqueidentifier =
                    (SELECT TOP(1) [Id] FROM [TODBase].[UserGroups] WHERE [Name] = 'TesisYoneticiGrubu');
                DECLARE @OdaYonetimiManageId uniqueidentifier =
                    (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'OdaYonetimi' AND [Name] = 'Manage');

                IF @TesisYoneticiGrupId IS NOT NULL
                   AND @OdaYonetimiManageId IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1
                       FROM [TODBase].[UserGroupRoles]
                       WHERE [UserGroupId] = @TesisYoneticiGrupId
                         AND [RoleId] = @OdaYonetimiManageId
                   )
                BEGIN
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @TesisYoneticiGrupId, @OdaYonetimiManageId, 0, @Now, @Now);
                END
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @TesisYoneticiGrupId uniqueidentifier =
                    (SELECT TOP(1) [Id] FROM [TODBase].[UserGroups] WHERE [Name] = 'TesisYoneticiGrubu');
                DECLARE @OdaYonetimiManageId uniqueidentifier =
                    (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'OdaYonetimi' AND [Name] = 'Manage');

                IF @TesisYoneticiGrupId IS NOT NULL
                   AND @OdaYonetimiManageId IS NOT NULL
                BEGIN
                    DELETE FROM [TODBase].[UserGroupRoles]
                    WHERE [UserGroupId] = @TesisYoneticiGrupId
                      AND [RoleId] = @OdaYonetimiManageId;
                END
                """);
        }
    }
}
