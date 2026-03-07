using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(StysAppDbContext))]
    [Migration("20260307103000_AddReservationCustomDiscountPermission")]
    public partial class AddReservationCustomDiscountPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
                DECLARE @TesisManagerGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222202';
                DECLARE @ResepsiyonistGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222204';
                DECLARE @CustomDiscountRoleId uniqueidentifier = '3f72b865-1b6b-4db4-9d89-6d3e9f5e7b91';

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Id] = @CustomDiscountRoleId)
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@CustomDiscountRoleId, N'CustomIndirimGirebilir', N'RezervasyonYonetimi', 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @AdminGroupId)
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @CustomDiscountRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('c2b52a24-2e3e-4b2e-8bc8-1b8c0ca5c101', @AdminGroupId, @CustomDiscountRoleId, 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @TesisManagerGroupId)
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @CustomDiscountRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('9e6fdad4-0a89-4f65-b4e2-8f413f824102', @TesisManagerGroupId, @CustomDiscountRoleId, 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @ResepsiyonistGroupId)
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @ResepsiyonistGroupId AND [RoleId] = @CustomDiscountRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('56c2bc97-8eb5-4fa7-9806-e0b30aa2c103', @ResepsiyonistGroupId, @CustomDiscountRoleId, 0, @Now, @Now);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM [TODBase].[UserGroupRoles]
                WHERE [Id] IN (
                    'c2b52a24-2e3e-4b2e-8bc8-1b8c0ca5c101',
                    '9e6fdad4-0a89-4f65-b4e2-8f413f824102',
                    '56c2bc97-8eb5-4fa7-9806-e0b30aa2c103'
                );

                DELETE FROM [TODBase].[Roles]
                WHERE [Id] = '3f72b865-1b6b-4db4-9d89-6d3e9f5e7b91';
                """);
        }
    }
}
