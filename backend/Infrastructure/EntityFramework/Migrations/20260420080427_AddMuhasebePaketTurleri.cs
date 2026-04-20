using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddMuhasebePaketTurleri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaketTurleri",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    KisaAd = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_PaketTurleri", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaketTurleri_Ad",
                schema: "muhasebe",
                table: "PaketTurleri",
                column: "Ad",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PaketTurleri_AktifMi",
                schema: "muhasebe",
                table: "PaketTurleri",
                column: "AktifMi",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PaketTurleri_KisaAd",
                schema: "muhasebe",
                table: "PaketTurleri",
                column: "KisaAd",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.Sql(
                """
                DECLARE @Now datetime2 = SYSUTCDATETIME();

                INSERT INTO [muhasebe].[PaketTurleri] ([Ad], [KisaAd], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                SELECT v.[Ad], v.[KisaAd], 1, 0, @Now, @Now, N'system', N'system'
                FROM (VALUES
                    (N'Adet', N'Ad.'),
                    (N'Kilogram', N'Kg.'),
                    (N'Cuval', N'Cuv.'),
                    (N'Kasa', N'Kas.'),
                    (N'Koli', N'Kol.'),
                    (N'Teneke', N'Ten.'),
                    (N'Kova', N'Kov.'),
                    (N'Paket', N'Pk.'),
                    (N'Litre', N'L.'),
                    (N'Demet', N'Dm.')
                ) v([Ad], [KisaAd])
                WHERE NOT EXISTS (SELECT 1 FROM [muhasebe].[PaketTurleri] p WHERE p.[Ad] = v.[Ad] AND p.[IsDeleted] = 0);

                DECLARE @AdminGroupId uniqueidentifier;
                DECLARE @TesisManagerGroupId uniqueidentifier;
                DECLARE @MuhasebeciGroupId uniqueidentifier;
                SELECT TOP (1) @AdminGroupId = [Id] FROM [TODBase].[UserGroups] WHERE [Name] IN (N'YoneticiGrubu', N'Yönetici Grubu') AND [IsDeleted] = 0;
                SELECT TOP (1) @TesisManagerGroupId = [Id] FROM [TODBase].[UserGroups] WHERE [Name] = N'TesisYoneticiGrubu' AND [IsDeleted] = 0;
                SELECT TOP (1) @MuhasebeciGroupId = [Id] FROM [TODBase].[UserGroups] WHERE [Name] = N'MuhasebeciGrubu' AND [IsDeleted] = 0;

                DECLARE @MenuRoleId uniqueidentifier;
                DECLARE @ViewRoleId uniqueidentifier;
                DECLARE @ManageRoleId uniqueidentifier;

                SELECT TOP (1) @MenuRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'PaketTuruYonetimi' AND [Name] = N'Menu' ORDER BY [IsDeleted], [CreatedAt];
                IF @MenuRoleId IS NULL
                BEGIN
                    SET @MenuRoleId = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@MenuRoleId, N'Menu', N'PaketTuruYonetimi', 0, @Now, @Now);
                END
                ELSE
                BEGIN
                    UPDATE [TODBase].[Roles] SET [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now WHERE [Id] = @MenuRoleId;
                END;

                SELECT TOP (1) @ViewRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'PaketTuruYonetimi' AND [Name] = N'View' ORDER BY [IsDeleted], [CreatedAt];
                IF @ViewRoleId IS NULL
                BEGIN
                    SET @ViewRoleId = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@ViewRoleId, N'View', N'PaketTuruYonetimi', 0, @Now, @Now);
                END
                ELSE
                BEGIN
                    UPDATE [TODBase].[Roles] SET [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now WHERE [Id] = @ViewRoleId;
                END;

                SELECT TOP (1) @ManageRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'PaketTuruYonetimi' AND [Name] = N'Manage' ORDER BY [IsDeleted], [CreatedAt];
                IF @ManageRoleId IS NULL
                BEGIN
                    SET @ManageRoleId = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (@ManageRoleId, N'Manage', N'PaketTuruYonetimi', 0, @Now, @Now);
                END
                ELSE
                BEGIN
                    UPDATE [TODBase].[Roles] SET [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now WHERE [Id] = @ManageRoleId;
                END;

                IF @AdminGroupId IS NOT NULL
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @MenuRoleId AND [IsDeleted] = 0)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @AdminGroupId, @MenuRoleId, 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @ViewRoleId AND [IsDeleted] = 0)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @AdminGroupId, @ViewRoleId, 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @ManageRoleId AND [IsDeleted] = 0)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @AdminGroupId, @ManageRoleId, 0, @Now, @Now);
                END;

                IF @TesisManagerGroupId IS NOT NULL
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @MenuRoleId AND [IsDeleted] = 0)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @TesisManagerGroupId, @MenuRoleId, 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @ViewRoleId AND [IsDeleted] = 0)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @TesisManagerGroupId, @ViewRoleId, 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @ManageRoleId AND [IsDeleted] = 0)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @TesisManagerGroupId, @ManageRoleId, 0, @Now, @Now);
                END;

                IF @MuhasebeciGroupId IS NOT NULL
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @MuhasebeciGroupId AND [RoleId] = @MenuRoleId AND [IsDeleted] = 0)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @MuhasebeciGroupId, @MenuRoleId, 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @MuhasebeciGroupId AND [RoleId] = @ViewRoleId AND [IsDeleted] = 0)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @MuhasebeciGroupId, @ViewRoleId, 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @MuhasebeciGroupId AND [RoleId] = @ManageRoleId AND [IsDeleted] = 0)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @MuhasebeciGroupId, @ManageRoleId, 0, @Now, @Now);
                END;

                DECLARE @MuhasebeRootId uniqueidentifier;
                DECLARE @PaketTurleriMenuItemId uniqueidentifier;

                SELECT TOP (1) @MuhasebeRootId = [Id]
                FROM [TODBase].[MenuItems]
                WHERE [Label] = N'Muhasebe' AND [ParentId] IS NULL
                ORDER BY [IsDeleted], [CreatedAt];

                IF @MuhasebeRootId IS NULL
                BEGIN
                    SET @MuhasebeRootId = NEWID();
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@MuhasebeRootId, N'Muhasebe', N'pi pi-wallet', N'', NULL, 6, 0, @Now, @Now);
                END
                ELSE
                BEGIN
                    UPDATE [TODBase].[MenuItems]
                    SET [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now
                    WHERE [Id] = @MuhasebeRootId;
                END;

                SELECT TOP (1) @PaketTurleriMenuItemId = [Id]
                FROM [TODBase].[MenuItems]
                WHERE [Route] = N'muhasebe/paket-turleri'
                ORDER BY [IsDeleted], [CreatedAt];

                IF @PaketTurleriMenuItemId IS NULL
                BEGIN
                    SET @PaketTurleriMenuItemId = NEWID();
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@PaketTurleriMenuItemId, N'Paket Turleri', N'pi pi-clone', N'muhasebe/paket-turleri', @MuhasebeRootId, 10, 0, @Now, @Now);
                END
                ELSE
                BEGIN
                    UPDATE [TODBase].[MenuItems]
                    SET [Label] = N'Paket Turleri',
                        [Icon] = N'pi pi-clone',
                        [Route] = N'muhasebe/paket-turleri',
                        [ParentId] = @MuhasebeRootId,
                        [MenuOrder] = 10,
                        [IsDeleted] = 0,
                        [DeletedAt] = NULL,
                        [UpdatedAt] = @Now
                    WHERE [Id] = @PaketTurleriMenuItemId;
                END;

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @MuhasebeRootId AND [RoleId] = @MenuRoleId AND [IsDeleted] = 0)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @MuhasebeRootId, @MenuRoleId, 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @PaketTurleriMenuItemId AND [RoleId] = @MenuRoleId AND [IsDeleted] = 0)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @PaketTurleriMenuItemId, @MenuRoleId, 0, @Now, @Now);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM [TODBase].[MenuItemRoles]
                WHERE [MenuItemId] IN (SELECT [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'muhasebe/paket-turleri');

                DELETE FROM [TODBase].[MenuItems]
                WHERE [Route] = N'muhasebe/paket-turleri';

                DELETE FROM [TODBase].[Roles]
                WHERE [Domain] = N'PaketTuruYonetimi';
                """);

            migrationBuilder.DropTable(
                name: "PaketTurleri",
                schema: "muhasebe");
        }
    }
}
