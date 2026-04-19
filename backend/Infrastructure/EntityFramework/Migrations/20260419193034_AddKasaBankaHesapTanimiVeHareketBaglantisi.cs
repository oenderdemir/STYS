using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddKasaBankaHesapTanimiVeHareketBaglantisi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "KasaBankaHesapId",
                schema: "muhasebe",
                table: "KasaHareketleri",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KasaBankaHesapId",
                schema: "muhasebe",
                table: "BankaHareketleri",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "KasaBankaHesaplari",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tip = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Kod = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MuhasebeHesapPlaniId = table.Column<int>(type: "int", nullable: false),
                    BankaAdi = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SubeAdi = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    HesapNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Iban = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: true),
                    MusteriNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    HesapTuru = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
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
                    table.PrimaryKey("PK_KasaBankaHesaplari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KasaBankaHesaplari_MuhasebeHesapPlanlari_MuhasebeHesapPlaniId",
                        column: x => x.MuhasebeHesapPlaniId,
                        principalSchema: "muhasebe",
                        principalTable: "MuhasebeHesapPlanlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KasaHareketleri_KasaBankaHesapId",
                schema: "muhasebe",
                table: "KasaHareketleri",
                column: "KasaBankaHesapId",
                filter: "[IsDeleted] = 0 AND [KasaBankaHesapId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BankaHareketleri_KasaBankaHesapId",
                schema: "muhasebe",
                table: "BankaHareketleri",
                column: "KasaBankaHesapId",
                filter: "[IsDeleted] = 0 AND [KasaBankaHesapId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_KasaBankaHesaplari_Kod",
                schema: "muhasebe",
                table: "KasaBankaHesaplari",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KasaBankaHesaplari_MuhasebeHesapPlaniId",
                schema: "muhasebe",
                table: "KasaBankaHesaplari",
                column: "MuhasebeHesapPlaniId",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KasaBankaHesaplari_Tip_AktifMi",
                schema: "muhasebe",
                table: "KasaBankaHesaplari",
                columns: new[] { "Tip", "AktifMi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_BankaHareketleri_KasaBankaHesaplari_KasaBankaHesapId",
                schema: "muhasebe",
                table: "BankaHareketleri",
                column: "KasaBankaHesapId",
                principalSchema: "muhasebe",
                principalTable: "KasaBankaHesaplari",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_KasaHareketleri_KasaBankaHesaplari_KasaBankaHesapId",
                schema: "muhasebe",
                table: "KasaHareketleri",
                column: "KasaBankaHesapId",
                principalSchema: "muhasebe",
                principalTable: "KasaBankaHesaplari",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
                DECLARE @TesisManagerGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222202';

                DECLARE @MenuRoleId uniqueidentifier;
                DECLARE @ViewRoleId uniqueidentifier;
                DECLARE @ManageRoleId uniqueidentifier;

                SELECT TOP (1) @MenuRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'KasaBankaHesapYonetimi' AND [Name] = N'Menu';
                IF @MenuRoleId IS NULL
                BEGIN
                    SET @MenuRoleId = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@MenuRoleId, N'Menu', N'KasaBankaHesapYonetimi', 0, @Now, @Now);
                END;

                SELECT TOP (1) @ViewRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'KasaBankaHesapYonetimi' AND [Name] = N'View';
                IF @ViewRoleId IS NULL
                BEGIN
                    SET @ViewRoleId = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@ViewRoleId, N'View', N'KasaBankaHesapYonetimi', 0, @Now, @Now);
                END;

                SELECT TOP (1) @ManageRoleId = [Id] FROM [TODBase].[Roles] WHERE [Domain] = N'KasaBankaHesapYonetimi' AND [Name] = N'Manage';
                IF @ManageRoleId IS NULL
                BEGIN
                    SET @ManageRoleId = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@ManageRoleId, N'Manage', N'KasaBankaHesapYonetimi', 0, @Now, @Now);
                END;

                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @AdminGroupId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @MenuRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @AdminGroupId, @MenuRoleId, 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @ViewRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @AdminGroupId, @ViewRoleId, 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = @ManageRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @AdminGroupId, @ManageRoleId, 0, @Now, @Now);
                END;

                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @TesisManagerGroupId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @MenuRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @TesisManagerGroupId, @MenuRoleId, 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @ViewRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @TesisManagerGroupId, @ViewRoleId, 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = @ManageRoleId)
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @TesisManagerGroupId, @ManageRoleId, 0, @Now, @Now);
                END;

                DECLARE @MuhasebeRootId uniqueidentifier;
                DECLARE @MenuItemId uniqueidentifier;

                SELECT TOP (1) @MuhasebeRootId = [Id]
                FROM [TODBase].[MenuItems]
                WHERE [Label] = N'Muhasebe' AND [ParentId] IS NULL AND [IsDeleted] = 0;

                IF @MuhasebeRootId IS NULL
                BEGIN
                    SET @MuhasebeRootId = NEWID();
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@MuhasebeRootId, N'Muhasebe', N'pi pi-wallet', N'', NULL, 6, 0, @Now, @Now);
                END;

                SELECT TOP (1) @MenuItemId = [Id] FROM [TODBase].[MenuItems] WHERE [Route] = N'muhasebe/kasa-banka-hesaplari';
                IF @MenuItemId IS NULL
                BEGIN
                    SET @MenuItemId = NEWID();
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@MenuItemId, N'Kasa/Banka Hesaplari', N'pi pi-credit-card', N'muhasebe/kasa-banka-hesaplari', @MuhasebeRootId, 2, 0, @Now, @Now);
                END
                ELSE
                BEGIN
                    UPDATE [TODBase].[MenuItems]
                    SET [Label] = N'Kasa/Banka Hesaplari',
                        [Icon] = N'pi pi-credit-card',
                        [ParentId] = @MuhasebeRootId,
                        [MenuOrder] = 2,
                        [IsDeleted] = 0,
                        [DeletedAt] = NULL,
                        [UpdatedAt] = @Now
                    WHERE [Id] = @MenuItemId;
                END;

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @MuhasebeRootId AND [RoleId] = @MenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @MuhasebeRootId, @MenuRoleId, 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @MenuItemId AND [RoleId] = @MenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES (NEWID(), @MenuItemId, @MenuRoleId, 0, @Now, @Now);

                DECLARE @KasaMuhasebeId int;
                DECLARE @BankaMuhasebeId int;
                SELECT TOP (1) @KasaMuhasebeId = [Id] FROM [muhasebe].[MuhasebeHesapPlanlari] WHERE [TamKod] = N'1.10.100' AND [IsDeleted] = 0;
                SELECT TOP (1) @BankaMuhasebeId = [Id] FROM [muhasebe].[MuhasebeHesapPlanlari] WHERE [TamKod] = N'1.10.102' AND [IsDeleted] = 0;

                IF @KasaMuhasebeId IS NOT NULL
                AND NOT EXISTS (SELECT 1 FROM [muhasebe].[KasaBankaHesaplari] WHERE [Kod] = N'KASA-MERKEZ' AND [IsDeleted] = 0)
                BEGIN
                    INSERT INTO [muhasebe].[KasaBankaHesaplari]
                    ([Tip], [Kod], [Ad], [MuhasebeHesapPlaniId], [BankaAdi], [SubeAdi], [HesapNo], [Iban], [MusteriNo], [HesapTuru], [AktifMi], [Aciklama], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES
                    (N'NakitKasa', N'KASA-MERKEZ', N'Merkez Kasa', @KasaMuhasebeId, NULL, NULL, NULL, NULL, NULL, NULL, 1, N'Varsayilan nakit kasa hesabi', 0, @Now, @Now, N'SEED', N'SEED');
                END;

                IF @BankaMuhasebeId IS NOT NULL
                AND NOT EXISTS (SELECT 1 FROM [muhasebe].[KasaBankaHesaplari] WHERE [Kod] = N'BNK-VARSAYILAN' AND [IsDeleted] = 0)
                BEGIN
                    INSERT INTO [muhasebe].[KasaBankaHesaplari]
                    ([Tip], [Kod], [Ad], [MuhasebeHesapPlaniId], [BankaAdi], [SubeAdi], [HesapNo], [Iban], [MusteriNo], [HesapTuru], [AktifMi], [Aciklama], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES
                    (N'Banka', N'BNK-VARSAYILAN', N'Varsayilan Banka Hesabi', @BankaMuhasebeId, N'Varsayilan Banka', N'Merkez', N'0000001', N'TR000000000000000000000001', NULL, N'Vadesiz', 1, N'Varsayilan banka hesabi', 0, @Now, @Now, N'SEED', N'SEED');
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BankaHareketleri_KasaBankaHesaplari_KasaBankaHesapId",
                schema: "muhasebe",
                table: "BankaHareketleri");

            migrationBuilder.DropForeignKey(
                name: "FK_KasaHareketleri_KasaBankaHesaplari_KasaBankaHesapId",
                schema: "muhasebe",
                table: "KasaHareketleri");

            migrationBuilder.DropTable(
                name: "KasaBankaHesaplari",
                schema: "muhasebe");

            migrationBuilder.DropIndex(
                name: "IX_KasaHareketleri_KasaBankaHesapId",
                schema: "muhasebe",
                table: "KasaHareketleri");

            migrationBuilder.DropIndex(
                name: "IX_BankaHareketleri_KasaBankaHesapId",
                schema: "muhasebe",
                table: "BankaHareketleri");

            migrationBuilder.DropColumn(
                name: "KasaBankaHesapId",
                schema: "muhasebe",
                table: "KasaHareketleri");

            migrationBuilder.DropColumn(
                name: "KasaBankaHesapId",
                schema: "muhasebe",
                table: "BankaHareketleri");

            migrationBuilder.Sql(
                """
                DELETE mir
                FROM [TODBase].[MenuItemRoles] mir
                INNER JOIN [TODBase].[MenuItems] mi ON mi.[Id] = mir.[MenuItemId]
                WHERE mi.[Route] = N'muhasebe/kasa-banka-hesaplari';

                DELETE FROM [TODBase].[MenuItems]
                WHERE [Route] = N'muhasebe/kasa-banka-hesaplari';

                DELETE FROM [TODBase].[Roles]
                WHERE [Domain] = N'KasaBankaHesapYonetimi';
                """);
        }
    }
}
