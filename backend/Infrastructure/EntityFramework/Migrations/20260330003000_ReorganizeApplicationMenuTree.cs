using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260330003000_ReorganizeApplicationMenuTree")]
public partial class ReorganizeApplicationMenuTree : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();

            DECLARE @AnaMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666601';
            DECLARE @YetkilendirmeId uniqueidentifier = '66666666-6666-6666-6666-666666666603';

            DECLARE @TesisRootId uniqueidentifier = '3e0c5fb6-6d62-4f9e-93af-644f95ab2001';
            DECLARE @IsletmeRootId uniqueidentifier = '3e0c5fb6-6d62-4f9e-93af-644f95ab2002';
            DECLARE @SistemRootId uniqueidentifier = '3e0c5fb6-6d62-4f9e-93af-644f95ab2003';

            DECLARE @DashboardMenuId uniqueidentifier = '3e0c5fb6-6d62-4f9e-93af-644f95ab2010';
            DECLARE @EkHizmetParentId uniqueidentifier = '3e0c5fb6-6d62-4f9e-93af-644f95ab2020';
            DECLARE @KonaklamaTipiParentId uniqueidentifier = '3e0c5fb6-6d62-4f9e-93af-644f95ab2030';
            DECLARE @MisafirTipiParentId uniqueidentifier = '3e0c5fb6-6d62-4f9e-93af-644f95ab2040';

            DECLARE @RezervasyonYonetimiId uniqueidentifier = '0991feca-8df8-4e06-a253-941efe84a818';
            DECLARE @OdaBakimArizaId uniqueidentifier = '357a8f58-0ebe-4d8c-88eb-8f457f8c3124';
            DECLARE @OdaTemizlikId uniqueidentifier = '95c1f9f6-604f-4d82-8962-a8d682d99eef';

            DECLARE @BinaYonetimiId uniqueidentifier = '66666666-6666-6666-6666-666666666611';
            DECLARE @OdalarId uniqueidentifier = '66666666-6666-6666-6666-666666666614';
            DECLARE @OdaFiyatlariId uniqueidentifier = 'fb2d3f66-c4f4-4a88-a271-869f7f560f0d';

            DECLARE @TesisYonetimiId uniqueidentifier = '66666666-6666-6666-6666-666666666610';
            DECLARE @IsletmeAlanlariId uniqueidentifier = '66666666-6666-6666-6666-666666666612';
            DECLARE @OdaTipleriId uniqueidentifier = '66666666-6666-6666-6666-666666666613';
            DECLARE @OdaSiniflariId uniqueidentifier = '66666666-6666-6666-6666-666666666615';
            DECLARE @OdaOzellikleriId uniqueidentifier = '66666666-6666-6666-6666-666666666616';
            DECLARE @IndirimKurallariId uniqueidentifier = 'f6a88f07-1536-4de2-8a89-9a5b84a2a4f0';
            DECLARE @SezonKurallariId uniqueidentifier = '02d8c86c-4c43-41ca-9421-a6f67b57de04';

            DECLARE @UlkelerId uniqueidentifier = '66666666-6666-6666-6666-666666666602';
            DECLARE @IllerId uniqueidentifier = '66666666-6666-6666-6666-666666666609';
            DECLARE @ErisimTeshisId uniqueidentifier = '2f86f8f6-04ec-44e6-a41b-50d0f2e66004';

            DECLARE @EkHizmetTanimlariId uniqueidentifier = '0b84f211-1b01-4055-b25e-084e1f64b101';
            DECLARE @EkHizmetAtamalariId uniqueidentifier = '0b84f211-1b01-4055-b25e-084e1f64b201';
            DECLARE @EkHizmetTarifeleriId uniqueidentifier = '0b84f211-1b01-4055-b25e-084e1f64b301';

            DECLARE @KonaklamaTipiTanimlariId uniqueidentifier = 'f778fcb3-f238-43d3-9f2a-86ba9bc2ab31';
            DECLARE @KonaklamaTipiAtamalariId uniqueidentifier = '72fe4259-64f8-4415-9ebc-bf6d91276513';

            DECLARE @MisafirTipiTanimlariId uniqueidentifier = '9af5588f-5467-4808-b3c6-fdb3e2f10111';
            DECLARE @MisafirTipiAtamalariId uniqueidentifier = '9af5588f-5467-4808-b3c6-fdb3e2f10121';

            UPDATE [TODBase].[MenuItems]
            SET [Label] = N'ANA MENÜ',
                [MenuOrder] = 0,
                [UpdatedAt] = @Now
            WHERE [Id] = @AnaMenuId;

            UPDATE [TODBase].[MenuItems]
            SET [Label] = N'YETKİLENDİRME',
                [MenuOrder] = 4,
                [UpdatedAt] = @Now
            WHERE [Id] = @YetkilendirmeId;

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @TesisRootId)
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@TesisRootId, N'TESİS', NULL, N'', NULL, NULL, 1, 0, @Now, @Now);
            ELSE
                UPDATE [TODBase].[MenuItems]
                SET [Label] = N'TESİS', [Route] = N'', [ParentId] = NULL, [MenuOrder] = 1, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now
                WHERE [Id] = @TesisRootId;

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @IsletmeRootId)
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@IsletmeRootId, N'İŞLETME', NULL, N'', NULL, NULL, 2, 0, @Now, @Now);
            ELSE
                UPDATE [TODBase].[MenuItems]
                SET [Label] = N'İŞLETME', [Route] = N'', [ParentId] = NULL, [MenuOrder] = 2, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now
                WHERE [Id] = @IsletmeRootId;

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @SistemRootId)
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@SistemRootId, N'SİSTEM', NULL, N'', NULL, NULL, 3, 0, @Now, @Now);
            ELSE
                UPDATE [TODBase].[MenuItems]
                SET [Label] = N'SİSTEM', [Route] = N'', [ParentId] = NULL, [MenuOrder] = 3, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now
                WHERE [Id] = @SistemRootId;

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @DashboardMenuId)
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@DashboardMenuId, N'Dashboard', N'fa-solid fa-house', N'/', NULL, @AnaMenuId, 0, 0, @Now, @Now);
            ELSE
                UPDATE [TODBase].[MenuItems]
                SET [Label] = N'Dashboard', [Route] = N'/', [ParentId] = @AnaMenuId, [MenuOrder] = 0, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now
                WHERE [Id] = @DashboardMenuId;

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @EkHizmetParentId)
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@EkHizmetParentId, N'Ek Hizmetler', N'fa-solid fa-bell-concierge', N'', NULL, @TesisRootId, 3, 0, @Now, @Now);
            ELSE
                UPDATE [TODBase].[MenuItems]
                SET [Label] = N'Ek Hizmetler', [Route] = N'', [ParentId] = @TesisRootId, [MenuOrder] = 3, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now
                WHERE [Id] = @EkHizmetParentId;

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @KonaklamaTipiParentId)
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@KonaklamaTipiParentId, N'Konaklama Tipleri', N'fa-solid fa-bed', N'', NULL, @IsletmeRootId, 5, 0, @Now, @Now);
            ELSE
                UPDATE [TODBase].[MenuItems]
                SET [Label] = N'Konaklama Tipleri', [Route] = N'', [ParentId] = @IsletmeRootId, [MenuOrder] = 5, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now
                WHERE [Id] = @KonaklamaTipiParentId;

            IF NOT EXISTS (SELECT 1 FROM [TODBase].[MenuItems] WHERE [Id] = @MisafirTipiParentId)
                INSERT INTO [TODBase].[MenuItems] ([Id], [Label], [Icon], [Route], [QueryParams], [ParentId], [MenuOrder], [IsDeleted], [CreatedAt], [UpdatedAt])
                VALUES (@MisafirTipiParentId, N'Misafir Tipleri', N'fa-solid fa-user-group', N'', NULL, @IsletmeRootId, 6, 0, @Now, @Now);
            ELSE
                UPDATE [TODBase].[MenuItems]
                SET [Label] = N'Misafir Tipleri', [Route] = N'', [ParentId] = @IsletmeRootId, [MenuOrder] = 6, [IsDeleted] = 0, [DeletedAt] = NULL, [UpdatedAt] = @Now
                WHERE [Id] = @MisafirTipiParentId;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @AnaMenuId, [MenuOrder] = 1, [UpdatedAt] = @Now WHERE [Id] = @RezervasyonYonetimiId;
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @AnaMenuId, [MenuOrder] = 2, [UpdatedAt] = @Now WHERE [Id] = @OdaBakimArizaId;
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @AnaMenuId, [MenuOrder] = 3, [UpdatedAt] = @Now WHERE [Id] = @OdaTemizlikId;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @TesisRootId, [MenuOrder] = 0, [UpdatedAt] = @Now WHERE [Id] = @BinaYonetimiId;
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @TesisRootId, [MenuOrder] = 1, [UpdatedAt] = @Now WHERE [Id] = @OdalarId;
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @TesisRootId, [MenuOrder] = 2, [UpdatedAt] = @Now WHERE [Id] = @OdaFiyatlariId;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @EkHizmetParentId, [Label] = N'Global Tanımları', [MenuOrder] = 0, [UpdatedAt] = @Now WHERE [Id] = @EkHizmetTanimlariId;
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @EkHizmetParentId, [Label] = N'Tesis Atamaları', [MenuOrder] = 1, [UpdatedAt] = @Now WHERE [Id] = @EkHizmetAtamalariId;
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @EkHizmetParentId, [Label] = N'Tarifeler', [MenuOrder] = 2, [UpdatedAt] = @Now WHERE [Id] = @EkHizmetTarifeleriId;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @IsletmeRootId, [MenuOrder] = 0, [UpdatedAt] = @Now WHERE [Id] = @TesisYonetimiId;
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @IsletmeRootId, [MenuOrder] = 1, [UpdatedAt] = @Now WHERE [Id] = @IsletmeAlanlariId;
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @IsletmeRootId, [MenuOrder] = 2, [UpdatedAt] = @Now WHERE [Id] = @OdaTipleriId;
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @IsletmeRootId, [MenuOrder] = 3, [UpdatedAt] = @Now WHERE [Id] = @OdaSiniflariId;
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @IsletmeRootId, [MenuOrder] = 4, [UpdatedAt] = @Now WHERE [Id] = @OdaOzellikleriId;
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @KonaklamaTipiParentId, [Label] = N'Global Tanımları', [MenuOrder] = 0, [UpdatedAt] = @Now WHERE [Id] = @KonaklamaTipiTanimlariId;
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @KonaklamaTipiParentId, [Label] = N'Tesis Atamaları', [MenuOrder] = 1, [UpdatedAt] = @Now WHERE [Id] = @KonaklamaTipiAtamalariId;
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MisafirTipiParentId, [Label] = N'Global Tanımları', [MenuOrder] = 0, [UpdatedAt] = @Now WHERE [Id] = @MisafirTipiTanimlariId;
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @MisafirTipiParentId, [Label] = N'Tesis Atamaları', [MenuOrder] = 1, [UpdatedAt] = @Now WHERE [Id] = @MisafirTipiAtamalariId;
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @IsletmeRootId, [MenuOrder] = 7, [UpdatedAt] = @Now WHERE [Id] = @IndirimKurallariId;
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @IsletmeRootId, [MenuOrder] = 8, [UpdatedAt] = @Now WHERE [Id] = @SezonKurallariId;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @SistemRootId, [MenuOrder] = 0, [UpdatedAt] = @Now WHERE [Id] = @UlkelerId;
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @SistemRootId, [MenuOrder] = 1, [UpdatedAt] = @Now WHERE [Id] = @IllerId;
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @SistemRootId, [MenuOrder] = 2, [UpdatedAt] = @Now WHERE [Id] = @ErisimTeshisId;

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @YetkilendirmeId, [MenuOrder] = 0, [UpdatedAt] = @Now WHERE [Id] = '66666666-6666-6666-6666-666666666604';
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @YetkilendirmeId, [MenuOrder] = 1, [UpdatedAt] = @Now WHERE [Id] = '66666666-6666-6666-6666-666666666605';
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @YetkilendirmeId, [MenuOrder] = 2, [UpdatedAt] = @Now WHERE [Id] = '66666666-6666-6666-6666-666666666606';
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @YetkilendirmeId, [MenuOrder] = 3, [UpdatedAt] = @Now WHERE [Id] = '66666666-6666-6666-6666-666666666607';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @Now datetime2 = SYSUTCDATETIME();
            DECLARE @AnaMenuId uniqueidentifier = '66666666-6666-6666-6666-666666666601';
            DECLARE @YetkilendirmeId uniqueidentifier = '66666666-6666-6666-6666-666666666603';
            DECLARE @TesisRootId uniqueidentifier = '3e0c5fb6-6d62-4f9e-93af-644f95ab2001';
            DECLARE @IsletmeRootId uniqueidentifier = '3e0c5fb6-6d62-4f9e-93af-644f95ab2002';
            DECLARE @SistemRootId uniqueidentifier = '3e0c5fb6-6d62-4f9e-93af-644f95ab2003';
            DECLARE @DashboardMenuId uniqueidentifier = '3e0c5fb6-6d62-4f9e-93af-644f95ab2010';
            DECLARE @EkHizmetParentId uniqueidentifier = '3e0c5fb6-6d62-4f9e-93af-644f95ab2020';
            DECLARE @KonaklamaTipiParentId uniqueidentifier = '3e0c5fb6-6d62-4f9e-93af-644f95ab2030';
            DECLARE @MisafirTipiParentId uniqueidentifier = '3e0c5fb6-6d62-4f9e-93af-644f95ab2040';

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @AnaMenuId, [MenuOrder] = 0, [UpdatedAt] = @Now WHERE [Id] = '66666666-6666-6666-6666-666666666602';
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @AnaMenuId, [MenuOrder] = 1, [UpdatedAt] = @Now WHERE [Id] = '66666666-6666-6666-6666-666666666609';
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @AnaMenuId, [MenuOrder] = 2, [UpdatedAt] = @Now WHERE [Id] = '66666666-6666-6666-6666-666666666610';
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @AnaMenuId, [MenuOrder] = 3, [UpdatedAt] = @Now WHERE [Id] = '66666666-6666-6666-6666-666666666611';
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @AnaMenuId, [MenuOrder] = 4, [UpdatedAt] = @Now WHERE [Id] = '66666666-6666-6666-6666-666666666612';
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @AnaMenuId, [MenuOrder] = 5, [UpdatedAt] = @Now WHERE [Id] = '66666666-6666-6666-6666-666666666613';
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @AnaMenuId, [MenuOrder] = 6, [UpdatedAt] = @Now WHERE [Id] = '66666666-6666-6666-6666-666666666614';
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @AnaMenuId, [MenuOrder] = 7, [UpdatedAt] = @Now WHERE [Id] = '66666666-6666-6666-6666-666666666615';
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @AnaMenuId, [MenuOrder] = 8, [UpdatedAt] = @Now WHERE [Id] = '66666666-6666-6666-6666-666666666616';
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @AnaMenuId, [MenuOrder] = 17, [UpdatedAt] = @Now WHERE [Id] = 'fb2d3f66-c4f4-4a88-a271-869f7f560f0d';
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @AnaMenuId, [Label] = N'Konaklama Tipi Tanimlari', [MenuOrder] = 18, [UpdatedAt] = @Now WHERE [Id] = 'f778fcb3-f238-43d3-9f2a-86ba9bc2ab31';
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @AnaMenuId, [Label] = N'Konaklama Tipi Atamalari', [MenuOrder] = 19, [UpdatedAt] = @Now WHERE [Id] = '72fe4259-64f8-4415-9ebc-bf6d91276513';
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @AnaMenuId, [Label] = N'Misafir Tipi Tanimlari', [MenuOrder] = 20, [UpdatedAt] = @Now WHERE [Id] = '9af5588f-5467-4808-b3c6-fdb3e2f10111';
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @AnaMenuId, [Label] = N'Misafir Tipi Atamalari', [MenuOrder] = 21, [UpdatedAt] = @Now WHERE [Id] = '9af5588f-5467-4808-b3c6-fdb3e2f10121';
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @AnaMenuId, [MenuOrder] = 23, [UpdatedAt] = @Now WHERE [Id] = '02d8c86c-4c43-41ca-9421-a6f67b57de04';
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @AnaMenuId, [MenuOrder] = 24, [UpdatedAt] = @Now WHERE [Id] = '357a8f58-0ebe-4d8c-88eb-8f457f8c3124';
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @AnaMenuId, [Label] = N'Ek Hizmet Tanimlari', [MenuOrder] = 25, [UpdatedAt] = @Now WHERE [Id] = '0b84f211-1b01-4055-b25e-084e1f64b101';
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @AnaMenuId, [Label] = N'Ek Hizmet Atamalari', [MenuOrder] = 26, [UpdatedAt] = @Now WHERE [Id] = '0b84f211-1b01-4055-b25e-084e1f64b201';
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @AnaMenuId, [Label] = N'Ek Hizmet Tarifeleri', [MenuOrder] = 27, [UpdatedAt] = @Now WHERE [Id] = '0b84f211-1b01-4055-b25e-084e1f64b301';
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @AnaMenuId, [MenuOrder] = 28, [UpdatedAt] = @Now WHERE [Id] = '2f86f8f6-04ec-44e6-a41b-50d0f2e66004';
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @AnaMenuId, [MenuOrder] = 29, [UpdatedAt] = @Now WHERE [Id] = '95c1f9f6-604f-4d82-8962-a8d682d99eef';

            UPDATE [TODBase].[MenuItems] SET [ParentId] = @YetkilendirmeId, [MenuOrder] = 0, [UpdatedAt] = @Now WHERE [Id] = '66666666-6666-6666-6666-666666666604';
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @YetkilendirmeId, [MenuOrder] = 1, [UpdatedAt] = @Now WHERE [Id] = '66666666-6666-6666-6666-666666666605';
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @YetkilendirmeId, [MenuOrder] = 2, [UpdatedAt] = @Now WHERE [Id] = '66666666-6666-6666-6666-666666666606';
            UPDATE [TODBase].[MenuItems] SET [ParentId] = @YetkilendirmeId, [MenuOrder] = 3, [UpdatedAt] = @Now WHERE [Id] = '66666666-6666-6666-6666-666666666607';

            UPDATE [TODBase].[MenuItems] SET [Label] = N'Ana Menü', [MenuOrder] = 999, [UpdatedAt] = @Now WHERE [Id] = @AnaMenuId;
            UPDATE [TODBase].[MenuItems] SET [Label] = N'Yetkilendirme', [MenuOrder] = 999, [UpdatedAt] = @Now WHERE [Id] = @YetkilendirmeId;

            DELETE FROM [TODBase].[MenuItems] WHERE [Id] IN (@DashboardMenuId, @EkHizmetParentId, @KonaklamaTipiParentId, @MisafirTipiParentId, @TesisRootId, @IsletmeRootId, @SistemRootId);
            """);
    }
}
