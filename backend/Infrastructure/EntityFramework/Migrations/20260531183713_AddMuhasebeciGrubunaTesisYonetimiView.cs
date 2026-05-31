using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddMuhasebeciGrubunaTesisYonetimiView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                SET NOCOUNT ON;

                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @MuhasebeciGroupId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[UserGroups] WHERE [Name] = N'MuhasebeciGrubu' AND [IsDeleted] = 0 ORDER BY [CreatedAt]);
                DECLARE @RoleId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'TesisYonetimi' AND [Name] = N'View' AND [IsDeleted] = 0 ORDER BY [CreatedAt]);

                IF @MuhasebeciGroupId IS NOT NULL AND @RoleId IS NOT NULL
                   AND NOT EXISTS
                   (
                       SELECT 1
                       FROM [TODBase].[UserGroupRoles] ugr
                       WHERE ugr.[UserGroupId] = @MuhasebeciGroupId
                         AND ugr.[RoleId] = @RoleId
                         AND ugr.[IsDeleted] = 0
                   )
                BEGIN
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (NEWID(), @MuhasebeciGroupId, @RoleId, 0, @Now, @Now, N'migration_muhasebeci_tesis_view', N'migration_muhasebeci_tesis_view');
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                SET NOCOUNT ON;

                DECLARE @MuhasebeciGroupId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[UserGroups] WHERE [Name] = N'MuhasebeciGrubu' AND [IsDeleted] = 0 ORDER BY [CreatedAt]);
                DECLARE @RoleId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'TesisYonetimi' AND [Name] = N'View' AND [IsDeleted] = 0 ORDER BY [CreatedAt]);

                IF @MuhasebeciGroupId IS NOT NULL AND @RoleId IS NOT NULL
                BEGIN
                    DELETE FROM [TODBase].[UserGroupRoles]
                    WHERE [UserGroupId] = @MuhasebeciGroupId
                      AND [RoleId] = @RoleId;
                END;
                """);
        }
    }
}
