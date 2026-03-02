using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddResepsiyonistScopeAndGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TesisResepsiyonistleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_TesisResepsiyonistleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TesisResepsiyonistleri_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TesisResepsiyonistleri_TesisId_UserId",
                schema: "dbo",
                table: "TesisResepsiyonistleri",
                columns: new[] { "TesisId", "UserId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.Sql("""
                DECLARE @Now datetime2 = SYSUTCDATETIME();

                DECLARE @UiUserRole uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'KullaniciTipi' AND [Name] = 'UIUser');
                DECLARE @UserManagementMenu uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'UserManagement' AND [Name] = 'Menu');
                DECLARE @UserManagementView uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'UserManagement' AND [Name] = 'View');
                DECLARE @UserManagementManage uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'UserManagement' AND [Name] = 'Manage');
                DECLARE @UserGroupManagementView uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'UserGroupManagement' AND [Name] = 'View');

                DECLARE @TesisYonetimiMenu uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'TesisYonetimi' AND [Name] = 'Menu');
                DECLARE @TesisYonetimiView uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'TesisYonetimi' AND [Name] = 'View');
                DECLARE @BinaYonetimiMenu uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'BinaYonetimi' AND [Name] = 'Menu');
                DECLARE @BinaYonetimiView uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'BinaYonetimi' AND [Name] = 'View');
                DECLARE @OdaYonetimiMenu uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'OdaYonetimi' AND [Name] = 'Menu');
                DECLARE @OdaYonetimiView uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'OdaYonetimi' AND [Name] = 'View');

                DECLARE @TesisYoneticiGrupId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[UserGroups] WHERE [Name] = 'TesisYoneticiGrubu');
                DECLARE @ResepsiyonistGrupId uniqueidentifier = '22222222-2222-2222-2222-222222222204';

                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Name] = 'ResepsiyonistGrubu')
                    SELECT @ResepsiyonistGrupId = [Id] FROM [TODBase].[UserGroups] WHERE [Name] = 'ResepsiyonistGrubu';
                ELSE
                    INSERT INTO [TODBase].[UserGroups] ([Id], [Name], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@ResepsiyonistGrupId, 'ResepsiyonistGrubu', 0, @Now, @Now);

                IF @TesisYoneticiGrupId IS NOT NULL AND @UserManagementMenu IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisYoneticiGrupId AND [RoleId] = @UserManagementMenu)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @TesisYoneticiGrupId, @UserManagementMenu, 0, @Now, @Now);

                IF @TesisYoneticiGrupId IS NOT NULL AND @UserManagementView IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisYoneticiGrupId AND [RoleId] = @UserManagementView)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @TesisYoneticiGrupId, @UserManagementView, 0, @Now, @Now);

                IF @TesisYoneticiGrupId IS NOT NULL AND @UserManagementManage IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisYoneticiGrupId AND [RoleId] = @UserManagementManage)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @TesisYoneticiGrupId, @UserManagementManage, 0, @Now, @Now);

                IF @TesisYoneticiGrupId IS NOT NULL AND @UserGroupManagementView IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisYoneticiGrupId AND [RoleId] = @UserGroupManagementView)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @TesisYoneticiGrupId, @UserGroupManagementView, 0, @Now, @Now);

                IF @ResepsiyonistGrupId IS NOT NULL AND @UiUserRole IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @ResepsiyonistGrupId AND [RoleId] = @UiUserRole)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @ResepsiyonistGrupId, @UiUserRole, 0, @Now, @Now);

                IF @ResepsiyonistGrupId IS NOT NULL AND @TesisYonetimiMenu IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @ResepsiyonistGrupId AND [RoleId] = @TesisYonetimiMenu)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @ResepsiyonistGrupId, @TesisYonetimiMenu, 0, @Now, @Now);

                IF @ResepsiyonistGrupId IS NOT NULL AND @TesisYonetimiView IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @ResepsiyonistGrupId AND [RoleId] = @TesisYonetimiView)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @ResepsiyonistGrupId, @TesisYonetimiView, 0, @Now, @Now);

                IF @ResepsiyonistGrupId IS NOT NULL AND @BinaYonetimiMenu IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @ResepsiyonistGrupId AND [RoleId] = @BinaYonetimiMenu)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @ResepsiyonistGrupId, @BinaYonetimiMenu, 0, @Now, @Now);

                IF @ResepsiyonistGrupId IS NOT NULL AND @BinaYonetimiView IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @ResepsiyonistGrupId AND [RoleId] = @BinaYonetimiView)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @ResepsiyonistGrupId, @BinaYonetimiView, 0, @Now, @Now);

                IF @ResepsiyonistGrupId IS NOT NULL AND @OdaYonetimiMenu IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @ResepsiyonistGrupId AND [RoleId] = @OdaYonetimiMenu)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @ResepsiyonistGrupId, @OdaYonetimiMenu, 0, @Now, @Now);

                IF @ResepsiyonistGrupId IS NOT NULL AND @OdaYonetimiView IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @ResepsiyonistGrupId AND [RoleId] = @OdaYonetimiView)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @ResepsiyonistGrupId, @OdaYonetimiView, 0, @Now, @Now);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @ResepsiyonistGrupId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[UserGroups] WHERE [Name] = 'ResepsiyonistGrubu');
                DECLARE @TesisYoneticiGrupId uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[UserGroups] WHERE [Name] = 'TesisYoneticiGrubu');

                DECLARE @UserManagementMenu uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'UserManagement' AND [Name] = 'Menu');
                DECLARE @UserManagementView uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'UserManagement' AND [Name] = 'View');
                DECLARE @UserManagementManage uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'UserManagement' AND [Name] = 'Manage');
                DECLARE @UserGroupManagementView uniqueidentifier = (SELECT TOP(1) [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'UserGroupManagement' AND [Name] = 'View');

                IF @ResepsiyonistGrupId IS NOT NULL
                BEGIN
                    DELETE FROM [TODBase].[UserUserGroups]
                    WHERE [UserGroupId] = @ResepsiyonistGrupId;

                    DELETE FROM [TODBase].[UserGroupRoles]
                    WHERE [UserGroupId] = @ResepsiyonistGrupId;

                    DELETE FROM [TODBase].[UserGroups]
                    WHERE [Id] = @ResepsiyonistGrupId;
                END

                IF @TesisYoneticiGrupId IS NOT NULL
                BEGIN
                    DELETE FROM [TODBase].[UserGroupRoles]
                    WHERE [UserGroupId] = @TesisYoneticiGrupId
                      AND [RoleId] IN (@UserManagementMenu, @UserManagementView, @UserManagementManage, @UserGroupManagementView);
                END
                """);

            migrationBuilder.DropTable(
                name: "TesisResepsiyonistleri",
                schema: "dbo");
        }
    }
}
