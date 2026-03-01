using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    [DbContext(typeof(StysAppDbContext))]
    [Migration("20260301205000_RemoveOdaGranularPermissions")]
    public partial class RemoveOdaGranularPermissions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE ugr
                FROM [TODBase].[UserGroupRoles] ugr
                INNER JOIN [TODBase].[Roles] r ON r.[Id] = ugr.[RoleId]
                WHERE r.[Domain] = 'OdaYonetimi'
                  AND r.[Name] IN ('Create', 'Delete');

                DELETE mir
                FROM [TODBase].[MenuItemRoles] mir
                INNER JOIN [TODBase].[Roles] r ON r.[Id] = mir.[RoleId]
                WHERE r.[Domain] = 'OdaYonetimi'
                  AND r.[Name] IN ('Create', 'Delete');

                DELETE FROM [TODBase].[Roles]
                WHERE [Domain] = 'OdaYonetimi'
                  AND [Name] IN ('Create', 'Delete')
                  AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [RoleId] = [TODBase].[Roles].[Id])
                  AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [RoleId] = [TODBase].[Roles].[Id]);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @Now datetime2 = SYSUTCDATETIME();

                DECLARE @OdaYonetimiCreate uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'OdaYonetimi' AND [Name] = 'Create');
                IF @OdaYonetimiCreate IS NULL
                BEGIN
                    SET @OdaYonetimiCreate = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@OdaYonetimiCreate, 'Create', 'OdaYonetimi', 0, @Now, @Now);
                END

                DECLARE @OdaYonetimiDelete uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'OdaYonetimi' AND [Name] = 'Delete');
                IF @OdaYonetimiDelete IS NULL
                BEGIN
                    SET @OdaYonetimiDelete = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@OdaYonetimiDelete, 'Delete', 'OdaYonetimi', 0, @Now, @Now);
                END

                DECLARE @BinaYoneticiGrupId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[UserGroups] WHERE [Name] = 'BinaYoneticiGrubu');
                IF @BinaYoneticiGrupId IS NOT NULL
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @BinaYoneticiGrupId AND [RoleId] = @OdaYonetimiCreate)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES (NEWID(), @BinaYoneticiGrupId, @OdaYonetimiCreate, 0, @Now, @Now);

                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @BinaYoneticiGrupId AND [RoleId] = @OdaYonetimiDelete)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES (NEWID(), @BinaYoneticiGrupId, @OdaYonetimiDelete, 0, @Now, @Now);
                END
                """);
        }
    }
}
