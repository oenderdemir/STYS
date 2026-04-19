using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddMuhasebeHesaplarAndBindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Hesaplar",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    MuhasebeHesapPlaniId = table.Column<int>(type: "int", nullable: false),
                    GenelHesapMi = table.Column<bool>(type: "bit", nullable: false),
                    MuhasebeFormu = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
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
                    table.PrimaryKey("PK_Hesaplar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Hesaplar_MuhasebeHesapPlanlari_MuhasebeHesapPlaniId",
                        column: x => x.MuhasebeHesapPlaniId,
                        principalSchema: "muhasebe",
                        principalTable: "MuhasebeHesapPlanlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HesapDepoBaglantilari",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HesapId = table.Column<int>(type: "int", nullable: false),
                    DepoId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_HesapDepoBaglantilari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HesapDepoBaglantilari_Depolar_DepoId",
                        column: x => x.DepoId,
                        principalSchema: "muhasebe",
                        principalTable: "Depolar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HesapDepoBaglantilari_Hesaplar_HesapId",
                        column: x => x.HesapId,
                        principalSchema: "muhasebe",
                        principalTable: "Hesaplar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HesapKasaBankaBaglantilari",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HesapId = table.Column<int>(type: "int", nullable: false),
                    KasaBankaHesapId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_HesapKasaBankaBaglantilari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HesapKasaBankaBaglantilari_Hesaplar_HesapId",
                        column: x => x.HesapId,
                        principalSchema: "muhasebe",
                        principalTable: "Hesaplar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HesapKasaBankaBaglantilari_KasaBankaHesaplari_KasaBankaHesapId",
                        column: x => x.KasaBankaHesapId,
                        principalSchema: "muhasebe",
                        principalTable: "KasaBankaHesaplari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HesapDepoBaglantilari_DepoId",
                schema: "muhasebe",
                table: "HesapDepoBaglantilari",
                column: "DepoId",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_HesapDepoBaglantilari_HesapId_DepoId",
                schema: "muhasebe",
                table: "HesapDepoBaglantilari",
                columns: new[] { "HesapId", "DepoId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_HesapKasaBankaBaglantilari_HesapId_KasaBankaHesapId",
                schema: "muhasebe",
                table: "HesapKasaBankaBaglantilari",
                columns: new[] { "HesapId", "KasaBankaHesapId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_HesapKasaBankaBaglantilari_KasaBankaHesapId",
                schema: "muhasebe",
                table: "HesapKasaBankaBaglantilari",
                column: "KasaBankaHesapId",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Hesaplar_Ad",
                schema: "muhasebe",
                table: "Hesaplar",
                column: "Ad",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Hesaplar_MuhasebeHesapPlaniId",
                schema: "muhasebe",
                table: "Hesaplar",
                column: "MuhasebeHesapPlaniId",
                filter: "[IsDeleted] = 0");

            migrationBuilder.Sql(
                """
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
                DECLARE @TesisManagerGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222202';
                DECLARE @MenuRoleId uniqueidentifier;
                DECLARE @ViewRoleId uniqueidentifier;
                DECLARE @ManageRoleId uniqueidentifier;
                DECLARE @MuhasebeRootId uniqueidentifier;
                DECLARE @MenuItemId uniqueidentifier;

                SELECT TOP (1) @MenuRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'HesapYonetimi' AND [Name] = N'Menu';
                IF @MenuRoleId IS NULL BEGIN SET @MenuRoleId = NEWID(); INSERT INTO [TODBase].[Roles] ([Id],[Name],[Domain],[IsDeleted],[CreatedAt],[UpdatedAt]) VALUES (@MenuRoleId,N'Menu',N'HesapYonetimi',0,@Now,@Now); END;
                SELECT TOP (1) @ViewRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'HesapYonetimi' AND [Name] = N'View';
                IF @ViewRoleId IS NULL BEGIN SET @ViewRoleId = NEWID(); INSERT INTO [TODBase].[Roles] ([Id],[Name],[Domain],[IsDeleted],[CreatedAt],[UpdatedAt]) VALUES (@ViewRoleId,N'View',N'HesapYonetimi',0,@Now,@Now); END;
                SELECT TOP (1) @ManageRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'HesapYonetimi' AND [Name] = N'Manage';
                IF @ManageRoleId IS NULL BEGIN SET @ManageRoleId = NEWID(); INSERT INTO [TODBase].[Roles] ([Id],[Name],[Domain],[IsDeleted],[CreatedAt],[UpdatedAt]) VALUES (@ManageRoleId,N'Manage',N'HesapYonetimi',0,@Now,@Now); END;

                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @AdminGroupId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId]=@AdminGroupId AND [RoleId]=@MenuRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id],[UserGroupId],[RoleId],[IsDeleted],[CreatedAt],[UpdatedAt]) VALUES (NEWID(),@AdminGroupId,@MenuRoleId,0,@Now,@Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId]=@AdminGroupId AND [RoleId]=@ViewRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id],[UserGroupId],[RoleId],[IsDeleted],[CreatedAt],[UpdatedAt]) VALUES (NEWID(),@AdminGroupId,@ViewRoleId,0,@Now,@Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId]=@AdminGroupId AND [RoleId]=@ManageRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id],[UserGroupId],[RoleId],[IsDeleted],[CreatedAt],[UpdatedAt]) VALUES (NEWID(),@AdminGroupId,@ManageRoleId,0,@Now,@Now);
                END;

                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @TesisManagerGroupId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId]=@TesisManagerGroupId AND [RoleId]=@MenuRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id],[UserGroupId],[RoleId],[IsDeleted],[CreatedAt],[UpdatedAt]) VALUES (NEWID(),@TesisManagerGroupId,@MenuRoleId,0,@Now,@Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId]=@TesisManagerGroupId AND [RoleId]=@ViewRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id],[UserGroupId],[RoleId],[IsDeleted],[CreatedAt],[UpdatedAt]) VALUES (NEWID(),@TesisManagerGroupId,@ViewRoleId,0,@Now,@Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId]=@TesisManagerGroupId AND [RoleId]=@ManageRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id],[UserGroupId],[RoleId],[IsDeleted],[CreatedAt],[UpdatedAt]) VALUES (NEWID(),@TesisManagerGroupId,@ManageRoleId,0,@Now,@Now);
                END;

                SELECT TOP (1) @MuhasebeRootId = [Id] FROM [TODBase].[MenuItems] WHERE [Label]=N'Muhasebe' AND [ParentId] IS NULL AND [IsDeleted]=0;
                IF @MuhasebeRootId IS NULL
                BEGIN
                    SET @MuhasebeRootId = NEWID();
                    INSERT INTO [TODBase].[MenuItems] ([Id],[Label],[Icon],[Route],[ParentId],[MenuOrder],[IsDeleted],[CreatedAt],[UpdatedAt]) VALUES (@MuhasebeRootId,N'Muhasebe',N'pi pi-wallet',N'',NULL,6,0,@Now,@Now);
                END;

                SELECT TOP (1) @MenuItemId = [Id] FROM [TODBase].[MenuItems] WHERE [Route]=N'muhasebe/hesaplar';
                IF @MenuItemId IS NULL
                BEGIN
                    SET @MenuItemId = NEWID();
                    INSERT INTO [TODBase].[MenuItems] ([Id],[Label],[Icon],[Route],[ParentId],[MenuOrder],[IsDeleted],[CreatedAt],[UpdatedAt]) VALUES (@MenuItemId,N'Hesaplar',N'pi pi-sitemap',N'muhasebe/hesaplar',@MuhasebeRootId,11,0,@Now,@Now);
                END
                ELSE
                BEGIN
                    UPDATE [TODBase].[MenuItems] SET [Label]=N'Hesaplar',[Icon]=N'pi pi-sitemap',[ParentId]=@MuhasebeRootId,[MenuOrder]=11,[IsDeleted]=0,[DeletedAt]=NULL,[UpdatedAt]=@Now WHERE [Id]=@MenuItemId;
                END;

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId]=@MuhasebeRootId AND [RoleId]=@MenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id],[MenuItemId],[RoleId],[IsDeleted],[CreatedAt],[UpdatedAt]) VALUES (NEWID(),@MuhasebeRootId,@MenuRoleId,0,@Now,@Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId]=@MenuItemId AND [RoleId]=@MenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id],[MenuItemId],[RoleId],[IsDeleted],[CreatedAt],[UpdatedAt]) VALUES (NEWID(),@MenuItemId,@MenuRoleId,0,@Now,@Now);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HesapDepoBaglantilari",
                schema: "muhasebe");

            migrationBuilder.DropTable(
                name: "HesapKasaBankaBaglantilari",
                schema: "muhasebe");

            migrationBuilder.DropTable(
                name: "Hesaplar",
                schema: "muhasebe");

            migrationBuilder.Sql(
                """
                DELETE mir
                FROM [TODBase].[MenuItemRoles] mir
                INNER JOIN [TODBase].[MenuItems] mi ON mi.[Id] = mir.[MenuItemId]
                WHERE mi.[Route] = N'muhasebe/hesaplar';

                DELETE FROM [TODBase].[MenuItems]
                WHERE [Route] = N'muhasebe/hesaplar';

                DELETE FROM [TODBase].[Roles]
                WHERE [Domain] = N'HesapYonetimi';
                """);
        }
    }
}
