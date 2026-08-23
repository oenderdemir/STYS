using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddStockRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StokTalepler",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    TalepEdenDepoId = table.Column<int>(type: "int", nullable: false),
                    KarsilayanDepoId = table.Column<int>(type: "int", nullable: false),
                    TalepTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Durum = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    TalepEdenKullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_StokTalepler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StokTalepler_Depolar_KarsilayanDepoId",
                        column: x => x.KarsilayanDepoId,
                        principalSchema: "muhasebe",
                        principalTable: "Depolar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StokTalepler_Depolar_TalepEdenDepoId",
                        column: x => x.TalepEdenDepoId,
                        principalSchema: "muhasebe",
                        principalTable: "Depolar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StokTalepler_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StokTalepSatirlari",
                schema: "muhasebe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StokTalepId = table.Column<int>(type: "int", nullable: false),
                    TasinirKartId = table.Column<int>(type: "int", nullable: false),
                    StokLotId = table.Column<int>(type: "int", nullable: true),
                    StokSeriId = table.Column<int>(type: "int", nullable: true),
                    TakipTipi = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    StokKodu = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TasinirKartAd = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Birim = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    LotNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SonKullanmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SeriNo = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    TalepMiktari = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    OnaylananMiktar = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TeslimEdilenMiktar = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    TransferGrupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_StokTalepSatirlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StokTalepSatirlari_StokLotlar_StokLotId",
                        column: x => x.StokLotId,
                        principalSchema: "muhasebe",
                        principalTable: "StokLotlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StokTalepSatirlari_StokSeriler_StokSeriId",
                        column: x => x.StokSeriId,
                        principalSchema: "muhasebe",
                        principalTable: "StokSeriler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StokTalepSatirlari_StokTalepler_StokTalepId",
                        column: x => x.StokTalepId,
                        principalSchema: "muhasebe",
                        principalTable: "StokTalepler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StokTalepSatirlari_TasinirKartlar_TasinirKartId",
                        column: x => x.TasinirKartId,
                        principalSchema: "muhasebe",
                        principalTable: "TasinirKartlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StokTalepler_KarsilayanDepoId",
                schema: "muhasebe",
                table: "StokTalepler",
                column: "KarsilayanDepoId");

            migrationBuilder.CreateIndex(
                name: "IX_StokTalepler_TalepEdenDepoId",
                schema: "muhasebe",
                table: "StokTalepler",
                column: "TalepEdenDepoId");

            migrationBuilder.CreateIndex(
                name: "IX_StokTalepler_TesisId_TalepEdenDepoId_KarsilayanDepoId_TalepTarihi",
                schema: "muhasebe",
                table: "StokTalepler",
                columns: new[] { "TesisId", "TalepEdenDepoId", "KarsilayanDepoId", "TalepTarihi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_StokTalepSatirlari_StokLotId",
                schema: "muhasebe",
                table: "StokTalepSatirlari",
                column: "StokLotId");

            migrationBuilder.CreateIndex(
                name: "IX_StokTalepSatirlari_StokSeriId",
                schema: "muhasebe",
                table: "StokTalepSatirlari",
                column: "StokSeriId");

            migrationBuilder.CreateIndex(
                name: "IX_StokTalepSatirlari_StokTalepId_TasinirKartId_StokLotId_StokSeriId",
                schema: "muhasebe",
                table: "StokTalepSatirlari",
                columns: new[] { "StokTalepId", "TasinirKartId", "StokLotId", "StokSeriId" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_StokTalepSatirlari_TasinirKartId",
                schema: "muhasebe",
                table: "StokTalepSatirlari",
                column: "TasinirKartId");

            migrationBuilder.CreateIndex(
                name: "IX_StokTalepSatirlari_TransferGrupId",
                schema: "muhasebe",
                table: "StokTalepSatirlari",
                column: "TransferGrupId",
                filter: "[IsDeleted] = 0 AND [TransferGrupId] IS NOT NULL");

            migrationBuilder.Sql(
                """
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @StokHareketMenuRoleId uniqueidentifier;
                DECLARE @MuhasebeRootId uniqueidentifier;
                DECLARE @StokDepoYonetimiId uniqueidentifier;
                DECLARE @StokTalepleriMenuId uniqueidentifier;

                SELECT TOP (1) @StokHareketMenuRoleId = [Id]
                FROM [TODBase].[Roles]
                WHERE [Domain] = N'StokHareketYonetimi' AND [Name] = N'Menu';

                IF @StokHareketMenuRoleId IS NULL
                BEGIN
                    SET @StokHareketMenuRoleId = NEWID();
                    INSERT INTO [TODBase].[Roles] ([Id], [Name], [Domain], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@StokHareketMenuRoleId, N'Menu', N'StokHareketYonetimi', 0, @Now, @Now);
                END;

                SELECT TOP (1) @MuhasebeRootId = [Id]
                FROM [TODBase].[MenuItems]
                WHERE [Label] = N'Muhasebe' AND [ParentId] IS NULL AND [IsDeleted] = 0;

                SELECT TOP (1) @StokDepoYonetimiId = [Id]
                FROM [TODBase].[MenuItems]
                WHERE [Label] = N'Stok & Depo Yönetimi' AND [ParentId] = @MuhasebeRootId AND [IsDeleted] = 0;

                SELECT TOP (1) @StokTalepleriMenuId = [Id]
                FROM [TODBase].[MenuItems]
                WHERE [Route] = N'muhasebe/stok-talepleri';

                IF @StokTalepleriMenuId IS NULL
                BEGIN
                    SET @StokTalepleriMenuId = NEWID();
                    INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (@StokTalepleriMenuId, N'Stok Talepleri', N'pi pi-send', N'muhasebe/stok-talepleri', @StokDepoYonetimiId, 3, 0, @Now, @Now);
                END;
                ELSE
                BEGIN
                    UPDATE [TODBase].[MenuItems]
                    SET [Label] = N'Stok Talepleri',
                        [Icon] = N'pi pi-send',
                        [ParentId] = @StokDepoYonetimiId,
                        [MenuOrder] = 3,
                        [IsDeleted] = 0,
                        [DeletedAt] = NULL,
                        [UpdatedAt] = @Now
                    WHERE [Id] = @StokTalepleriMenuId;
                END;

                IF @StokDepoYonetimiId IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @StokDepoYonetimiId AND [RoleId] = @StokHareketMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @StokDepoYonetimiId, @StokHareketMenuRoleId, 0, @Now, @Now);

                IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE [MenuItemId] = @StokTalepleriMenuId AND [RoleId] = @StokHareketMenuRoleId)
                    INSERT INTO [TODBase].[MenuItemRoles] ([Id], [MenuItemId], [RoleId], [IsDeleted], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), @StokTalepleriMenuId, @StokHareketMenuRoleId, 0, @Now, @Now);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE mir
                FROM [TODBase].[MenuItemRoles] mir
                INNER JOIN [TODBase].[MenuItems] mi ON mi.[Id] = mir.[MenuItemId]
                WHERE mi.[Route] = N'muhasebe/stok-talepleri';

                DELETE FROM [TODBase].[MenuItems]
                WHERE [Route] = N'muhasebe/stok-talepleri';
                """);

            migrationBuilder.DropTable(
                name: "StokTalepSatirlari",
                schema: "muhasebe");

            migrationBuilder.DropTable(
                name: "StokTalepler",
                schema: "muhasebe");
        }
    }
}
