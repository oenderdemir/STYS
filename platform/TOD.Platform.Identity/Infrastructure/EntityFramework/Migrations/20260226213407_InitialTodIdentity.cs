using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TOD.Platform.Identity.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class InitialTodIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "TODBase");

            migrationBuilder.CreateTable(
                name: "MenuItems",
                schema: "TODBase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Icon = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Route = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QueryParams = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MenuOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_MenuItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuItems_MenuItems_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "TODBase",
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "TODBase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(450)", nullable: false, defaultValue: ""),
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
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserGroups",
                schema: "TODBase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
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
                    table.PrimaryKey("PK_UserGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "TODBase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NationalId = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AvatarPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MenuItemRoles",
                schema: "TODBase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MenuItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_MenuItemRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuItemRoles_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalSchema: "TODBase",
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MenuItemRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "TODBase",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserGroupRoles",
                schema: "TODBase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_UserGroupRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserGroupRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "TODBase",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserGroupRoles_UserGroups_UserGroupId",
                        column: x => x.UserGroupId,
                        principalSchema: "TODBase",
                        principalTable: "UserGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserUserGroups",
                schema: "TODBase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_UserUserGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserUserGroups_UserGroups_UserGroupId",
                        column: x => x.UserGroupId,
                        principalSchema: "TODBase",
                        principalTable: "UserGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserUserGroups_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "TODBase",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemRoles_MenuItemId_RoleId",
                schema: "TODBase",
                table: "MenuItemRoles",
                columns: new[] { "MenuItemId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemRoles_RoleId",
                schema: "TODBase",
                table: "MenuItemRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_ParentId",
                schema: "TODBase",
                table: "MenuItems",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Domain_Name",
                schema: "TODBase",
                table: "Roles",
                columns: new[] { "Domain", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserGroupRoles_RoleId",
                schema: "TODBase",
                table: "UserGroupRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserGroupRoles_UserGroupId_RoleId",
                schema: "TODBase",
                table: "UserGroupRoles",
                columns: new[] { "UserGroupId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserGroups_Name",
                schema: "TODBase",
                table: "UserGroups",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserName",
                schema: "TODBase",
                table: "Users",
                column: "UserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserUserGroups_UserGroupId",
                schema: "TODBase",
                table: "UserUserGroups",
                column: "UserGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_UserUserGroups_UserId_UserGroupId",
                schema: "TODBase",
                table: "UserUserGroups",
                columns: new[] { "UserId", "UserGroupId" },
                unique: true);

            migrationBuilder.Sql("""
                DECLARE @Now datetime2 = SYSUTCDATETIME();

                DECLARE @RoleManagementView uniqueidentifier = '11111111-1111-1111-1111-111111111101';
                DECLARE @RoleManagementManage uniqueidentifier = '11111111-1111-1111-1111-111111111102';
                DECLARE @UserManagementView uniqueidentifier = '11111111-1111-1111-1111-111111111103';
                DECLARE @UserManagementManage uniqueidentifier = '11111111-1111-1111-1111-111111111104';
                DECLARE @MenuManagementView uniqueidentifier = '11111111-1111-1111-1111-111111111105';
                DECLARE @MenuManagementManage uniqueidentifier = '11111111-1111-1111-1111-111111111106';
                DECLARE @CountryManagementView uniqueidentifier = '11111111-1111-1111-1111-111111111107';
                DECLARE @CountryManagementManage uniqueidentifier = '11111111-1111-1111-1111-111111111108';
                DECLARE @AdminRole uniqueidentifier = '11111111-1111-1111-1111-111111111109';
                DECLARE @UiUserRole uniqueidentifier = '11111111-1111-1111-1111-111111111110';
                DECLARE @ServiceUserRole uniqueidentifier = '11111111-1111-1111-1111-111111111111';

                IF EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = 'RoleManagement' AND [Name] = 'View')
                    SELECT @RoleManagementView = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'RoleManagement' AND [Name] = 'View';
                ELSE
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@RoleManagementView, 'View', 'RoleManagement', 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = 'RoleManagement' AND [Name] = 'Manage')
                    SELECT @RoleManagementManage = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'RoleManagement' AND [Name] = 'Manage';
                ELSE
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@RoleManagementManage, 'Manage', 'RoleManagement', 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = 'UserManagement' AND [Name] = 'View')
                    SELECT @UserManagementView = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'UserManagement' AND [Name] = 'View';
                ELSE
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@UserManagementView, 'View', 'UserManagement', 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = 'UserManagement' AND [Name] = 'Manage')
                    SELECT @UserManagementManage = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'UserManagement' AND [Name] = 'Manage';
                ELSE
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@UserManagementManage, 'Manage', 'UserManagement', 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = 'MenuManagement' AND [Name] = 'View')
                    SELECT @MenuManagementView = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'MenuManagement' AND [Name] = 'View';
                ELSE
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@MenuManagementView, 'View', 'MenuManagement', 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = 'MenuManagement' AND [Name] = 'Manage')
                    SELECT @MenuManagementManage = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'MenuManagement' AND [Name] = 'Manage';
                ELSE
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@MenuManagementManage, 'Manage', 'MenuManagement', 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = 'CountryManagement' AND [Name] = 'View')
                    SELECT @CountryManagementView = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'CountryManagement' AND [Name] = 'View';
                ELSE
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@CountryManagementView, 'View', 'CountryManagement', 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = 'CountryManagement' AND [Name] = 'Manage')
                    SELECT @CountryManagementManage = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'CountryManagement' AND [Name] = 'Manage';
                ELSE
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@CountryManagementManage, 'Manage', 'CountryManagement', 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = 'KullaniciTipi' AND [Name] = 'Admin')
                    SELECT @AdminRole = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'KullaniciTipi' AND [Name] = 'Admin';
                ELSE
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@AdminRole, 'Admin', 'KullaniciTipi', 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = 'KullaniciTipi' AND [Name] = 'UIUser')
                    SELECT @UiUserRole = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'KullaniciTipi' AND [Name] = 'UIUser';
                ELSE
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@UiUserRole, 'UIUser', 'KullaniciTipi', 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Domain] = 'KullaniciTipi' AND [Name] = 'ServiceUser')
                    SELECT @ServiceUserRole = [Id] FROM [TODBase].[Roles] WHERE [Domain] = 'KullaniciTipi' AND [Name] = 'ServiceUser';
                ELSE
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@ServiceUserRole, 'ServiceUser', 'KullaniciTipi', 0, @Now, @Now);

                DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Name] = N'Yönetici Grubu')
                    SELECT @AdminGroupId = [Id] FROM [TODBase].[UserGroups] WHERE [Name] = N'Yönetici Grubu';
                ELSE
                    INSERT INTO [TODBase].[UserGroups] ([Id], [Name], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@AdminGroupId, N'Yönetici Grubu', 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @RoleManagementView)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('33333333-3333-3333-3333-333333333301', @AdminGroupId, @RoleManagementView, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @RoleManagementManage)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('33333333-3333-3333-3333-333333333302', @AdminGroupId, @RoleManagementManage, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @UserManagementView)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('33333333-3333-3333-3333-333333333303', @AdminGroupId, @UserManagementView, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @UserManagementManage)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('33333333-3333-3333-3333-333333333304', @AdminGroupId, @UserManagementManage, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @MenuManagementView)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('33333333-3333-3333-3333-333333333305', @AdminGroupId, @MenuManagementView, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @MenuManagementManage)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('33333333-3333-3333-3333-333333333306', @AdminGroupId, @MenuManagementManage, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @CountryManagementView)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('33333333-3333-3333-3333-333333333307', @AdminGroupId, @CountryManagementView, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @CountryManagementManage)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('33333333-3333-3333-3333-333333333308', @AdminGroupId, @CountryManagementManage, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @AdminRole)
                    INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('33333333-3333-3333-3333-333333333309', @AdminGroupId, @AdminRole, 0, @Now, @Now);

                DECLARE @AdminUserId uniqueidentifier = '44444444-4444-4444-4444-444444444401';
                IF EXISTS (SELECT 1 FROM [TODBase].[Users] WHERE [UserName] = 'admin')
                    SELECT @AdminUserId = [Id] FROM [TODBase].[Users] WHERE [UserName] = 'admin';
                ELSE
                    INSERT INTO [TODBase].[Users] ([Id], [UserName], [FirstName], [LastName], [Email], [PasswordHash], [Status], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@AdminUserId, 'admin', N'Sistem', N'Yöneticisi', 'admin@example.com', 'PBKDF2$100000$auAMpF0+IQNI1gy6svNwCg==$qHX4GpYKCsDAZWAjYo97VyTijMdj5ZrZvArkAXFRi3E=', 1, 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserUserGroups] WHERE [UserId] = @AdminUserId AND [UserGroupId] = @AdminGroupId)
                    INSERT INTO [TODBase].[UserUserGroups] ([Id], [UserId], [UserGroupId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('55555555-5555-5555-5555-555555555501', @AdminUserId, @AdminGroupId, 0, @Now, @Now);

                DECLARE @MainMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666601';
                DECLARE @CountryMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666602';
                DECLARE @AuthMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666603';
                DECLARE @RoleMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666604';
                DECLARE @GroupMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666605';
                DECLARE @UserMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666606';
                DECLARE @MenuMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666607';

                IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Label] = N'Ana Menü' AND [ParentId] IS NULL)
                    SELECT @MainMenuId = [Id] FROM [TODBase].[MenuItems] WHERE [Label] = N'Ana Menü' AND [ParentId] IS NULL;
                ELSE
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [MenuOrder], [ParentId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@MainMenuId, N'Ana Menü', 'fa-solid fa-person-digging', '', NULL, 999, NULL, 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Label] = N'Ülke Yönetimi' AND [ParentId] = @MainMenuId)
                    SELECT @CountryMenuId = [Id] FROM [TODBase].[MenuItems] WHERE [Label] = N'Ülke Yönetimi' AND [ParentId] = @MainMenuId;
                ELSE
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [MenuOrder], [ParentId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@CountryMenuId, N'Ülke Yönetimi', 'fa-solid fa-globe', 'ulkeler', NULL, 0, @MainMenuId, 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Label] = N'Yetkilendirme' AND [ParentId] IS NULL)
                    SELECT @AuthMenuId = [Id] FROM [TODBase].[MenuItems] WHERE [Label] = N'Yetkilendirme' AND [ParentId] IS NULL;
                ELSE
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [MenuOrder], [ParentId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@AuthMenuId, N'Yetkilendirme', NULL, '', NULL, 999, NULL, 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Label] = N'Yetkiler' AND [ParentId] = @AuthMenuId)
                    SELECT @RoleMenuId = [Id] FROM [TODBase].[MenuItems] WHERE [Label] = N'Yetkiler' AND [ParentId] = @AuthMenuId;
                ELSE
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [MenuOrder], [ParentId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@RoleMenuId, N'Yetkiler', 'fa-solid fa-drum', 'yetkiler', NULL, 0, @AuthMenuId, 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Label] = N'Kullanıcı Gruplar' AND [ParentId] = @AuthMenuId)
                    SELECT @GroupMenuId = [Id] FROM [TODBase].[MenuItems] WHERE [Label] = N'Kullanıcı Gruplar' AND [ParentId] = @AuthMenuId;
                ELSE
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [MenuOrder], [ParentId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@GroupMenuId, N'Kullanıcı Gruplar', 'fa-solid fa-people-roof', 'kullanici-gruplar', NULL, 1, @AuthMenuId, 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Label] = N'Kullanıcılar' AND [ParentId] = @AuthMenuId)
                    SELECT @UserMenuId = [Id] FROM [TODBase].[MenuItems] WHERE [Label] = N'Kullanıcılar' AND [ParentId] = @AuthMenuId;
                ELSE
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [MenuOrder], [ParentId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@UserMenuId, N'Kullanıcılar', 'fa-solid fa-user-tie', 'kullanicilar', NULL, 2, @AuthMenuId, 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Label] = N'Menüler' AND [ParentId] = @AuthMenuId)
                    SELECT @MenuMenuId = [Id] FROM [TODBase].[MenuItems] WHERE [Label] = N'Menüler' AND [ParentId] = @AuthMenuId;
                ELSE
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [MenuOrder], [ParentId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@MenuMenuId, N'Menüler', 'fa-solid fa-bars', 'menuler', NULL, 3, @AuthMenuId, 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @RoleMenuId AND [RoleId] = @RoleManagementView)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('77777777-7777-7777-7777-777777777701', @RoleMenuId, @RoleManagementView, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @GroupMenuId AND [RoleId] = @UserManagementView)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('77777777-7777-7777-7777-777777777702', @GroupMenuId, @UserManagementView, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @UserMenuId AND [RoleId] = @UserManagementView)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('77777777-7777-7777-7777-777777777703', @UserMenuId, @UserManagementView, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @MenuMenuId AND [RoleId] = @MenuManagementView)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('77777777-7777-7777-7777-777777777704', @MenuMenuId, @MenuManagementView, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @CountryMenuId AND [RoleId] = @CountryManagementView)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('77777777-7777-7777-7777-777777777705', @CountryMenuId, @CountryManagementView, 0, @Now, @Now);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MenuItemRoles",
                schema: "TODBase");

            migrationBuilder.DropTable(
                name: "UserGroupRoles",
                schema: "TODBase");

            migrationBuilder.DropTable(
                name: "UserUserGroups",
                schema: "TODBase");

            migrationBuilder.DropTable(
                name: "MenuItems",
                schema: "TODBase");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "TODBase");

            migrationBuilder.DropTable(
                name: "UserGroups",
                schema: "TODBase");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "TODBase");
        }
    }
}
