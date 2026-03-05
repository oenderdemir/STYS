using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddRezervasyonYonetimi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Rezervasyonlar",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReferansNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OdaId = table.Column<int>(type: "int", nullable: false),
                    KisiSayisi = table.Column<int>(type: "int", nullable: false),
                    OdaNoSnapshot = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    OdaTipiAdiSnapshot = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    GirisTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CikisTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_Rezervasyonlar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rezervasyonlar_Odalar_OdaId",
                        column: x => x.OdaId,
                        principalSchema: "dbo",
                        principalTable: "Odalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Rezervasyonlar_OdaId_GirisTarihi_CikisTarihi",
                schema: "dbo",
                table: "Rezervasyonlar",
                columns: new[] { "OdaId", "GirisTarihi", "CikisTarihi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Rezervasyonlar_ReferansNo",
                schema: "dbo",
                table: "Rezervasyonlar",
                column: "ReferansNo",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.Sql("""
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @AdminGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222201';
                DECLARE @TesisManagerGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222202';
                DECLARE @ResepsiyonistGroupId uniqueidentifier = '22222222-2222-2222-2222-222222222204';
                DECLARE @MainMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666601';

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Id] = '6fa7deef-e2d8-4b55-9438-7f799baf3301')
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('6fa7deef-e2d8-4b55-9438-7f799baf3301', N'Menu', N'RezervasyonYonetimi', 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Id] = 'a6b12cef-2eeb-4504-b6a6-93f36b64ca02')
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('a6b12cef-2eeb-4504-b6a6-93f36b64ca02', N'View', N'RezervasyonYonetimi', 0, @Now, @Now);
                IF NOT EXISTS (SELECT 1 FROM [TODBase].[Roles] WHERE [Id] = 'f0ec9fd2-ab44-4027-8e55-96ef59a4fb03')
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('f0ec9fd2-ab44-4027-8e55-96ef59a4fb03', N'Manage', N'RezervasyonYonetimi', 0, @Now, @Now);

                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @AdminGroupId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = '6fa7deef-e2d8-4b55-9438-7f799baf3301')
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('1cf6ef70-f41d-44d8-a422-3f7675d8db11', @AdminGroupId, '6fa7deef-e2d8-4b55-9438-7f799baf3301', 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = 'a6b12cef-2eeb-4504-b6a6-93f36b64ca02')
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('2c5d1f6c-4b19-4e2d-9788-226df1ca2a12', @AdminGroupId, 'a6b12cef-2eeb-4504-b6a6-93f36b64ca02', 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @AdminGroupId AND [RoleId] = 'f0ec9fd2-ab44-4027-8e55-96ef59a4fb03')
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('fd588962-884f-4f88-8cd4-42a3318eb313', @AdminGroupId, 'f0ec9fd2-ab44-4027-8e55-96ef59a4fb03', 0, @Now, @Now);
                END

                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @TesisManagerGroupId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = '6fa7deef-e2d8-4b55-9438-7f799baf3301')
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('4c4d5a21-f1ee-43a6-b6d2-43e4c153f614', @TesisManagerGroupId, '6fa7deef-e2d8-4b55-9438-7f799baf3301', 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = 'a6b12cef-2eeb-4504-b6a6-93f36b64ca02')
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('5d6e7a2d-2251-4e87-ad3f-4473c6c3b715', @TesisManagerGroupId, 'a6b12cef-2eeb-4504-b6a6-93f36b64ca02', 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @TesisManagerGroupId AND [RoleId] = 'f0ec9fd2-ab44-4027-8e55-96ef59a4fb03')
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('89f1c4e9-f5ca-4d91-a3ef-99872799aa20', @TesisManagerGroupId, 'f0ec9fd2-ab44-4027-8e55-96ef59a4fb03', 0, @Now, @Now);
                END

                IF EXISTS (SELECT 1 FROM [TODBase].[UserGroups] WHERE [Id] = @ResepsiyonistGroupId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @ResepsiyonistGroupId AND [RoleId] = '6fa7deef-e2d8-4b55-9438-7f799baf3301')
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('6e7f8b31-52d1-4a59-9444-5b9b7e713816', @ResepsiyonistGroupId, '6fa7deef-e2d8-4b55-9438-7f799baf3301', 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @ResepsiyonistGroupId AND [RoleId] = 'a6b12cef-2eeb-4504-b6a6-93f36b64ca02')
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('7f8c9d42-3e5a-4b1d-9062-6cb6100d4917', @ResepsiyonistGroupId, 'a6b12cef-2eeb-4504-b6a6-93f36b64ca02', 0, @Now, @Now);
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[UserGroupRoles] WHERE [UserGroupId] = @ResepsiyonistGroupId AND [RoleId] = 'f0ec9fd2-ab44-4027-8e55-96ef59a4fb03')
                        INSERT INTO [TODBase].[UserGroupRoles] ([Id], [UserGroupId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt]) VALUES ('91808b68-9800-4f3c-83af-8f70af4d2b21', @ResepsiyonistGroupId, 'f0ec9fd2-ab44-4027-8e55-96ef59a4fb03', 0, @Now, @Now);
                END

                IF EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @MainMenuId)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = '0991feca-8df8-4e06-a253-941efe84a818')
                        INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                        VALUES ('0991feca-8df8-4e06-a253-941efe84a818', N'Rezervasyon Yonetimi', N'fa-solid fa-calendar-check', N'rezervasyon-yonetimi', NULL, @MainMenuId, 21, 0, @Now, @Now);
                END

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = '0991feca-8df8-4e06-a253-941efe84a818' AND [RoleId] = '6fa7deef-e2d8-4b55-9438-7f799baf3301')
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES ('8ea9be53-e1f1-4262-9e84-71014ac6ca19', '0991feca-8df8-4e06-a253-941efe84a818', '6fa7deef-e2d8-4b55-9438-7f799baf3301', 0, @Now, @Now);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM [TODBase].[MenuItemRoles] WHERE [Id] = '8ea9be53-e1f1-4262-9e84-71014ac6ca19';
                DELETE FROM [TODBase].[MenuItems] WHERE [Id] = '0991feca-8df8-4e06-a253-941efe84a818';

                DELETE FROM [TODBase].[UserGroupRoles] WHERE [Id] IN (
                    '1cf6ef70-f41d-44d8-a422-3f7675d8db11',
                    '2c5d1f6c-4b19-4e2d-9788-226df1ca2a12',
                    'fd588962-884f-4f88-8cd4-42a3318eb313',
                    '4c4d5a21-f1ee-43a6-b6d2-43e4c153f614',
                    '5d6e7a2d-2251-4e87-ad3f-4473c6c3b715',
                    '89f1c4e9-f5ca-4d91-a3ef-99872799aa20',
                    '6e7f8b31-52d1-4a59-9444-5b9b7e713816',
                    '7f8c9d42-3e5a-4b1d-9062-6cb6100d4917',
                    '91808b68-9800-4f3c-83af-8f70af4d2b21'
                );

                DELETE FROM [TODBase].[Roles] WHERE [Id] IN (
                    '6fa7deef-e2d8-4b55-9438-7f799baf3301',
                    'a6b12cef-2eeb-4504-b6a6-93f36b64ca02',
                    'f0ec9fd2-ab44-4027-8e55-96ef59a4fb03'
                );
                """);

            migrationBuilder.DropTable(
                name: "Rezervasyonlar",
                schema: "dbo");
        }
    }
}
