using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class SeedDomainTestData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @SeedUser nvarchar(128) = N'migration_seed_domain_test_data';

                IF NOT EXISTS (SELECT 1 FROM [dbo].[Countries] WHERE [Code] = 'TR')
                    INSERT INTO [dbo].[Countries] ([Id], [Name], [Code], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES ('9f8fd4a5-5685-4c80-8a64-4f636cce1001', N'Turkiye', 'TR', 0, @Now, @Now, @SeedUser, @SeedUser);

                IF NOT EXISTS (SELECT 1 FROM [dbo].[Countries] WHERE [Code] = 'DE')
                    INSERT INTO [dbo].[Countries] ([Id], [Name], [Code], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES ('9f8fd4a5-5685-4c80-8a64-4f636cce1002', N'Almanya', 'DE', 0, @Now, @Now, @SeedUser, @SeedUser);

                IF NOT EXISTS (SELECT 1 FROM [dbo].[Countries] WHERE [Code] = 'GB')
                    INSERT INTO [dbo].[Countries] ([Id], [Name], [Code], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES ('9f8fd4a5-5685-4c80-8a64-4f636cce1003', N'Birlesik Krallik', 'GB', 0, @Now, @Now, @SeedUser, @SeedUser);

                IF NOT EXISTS (SELECT 1 FROM [dbo].[Countries] WHERE [Code] = 'FR')
                    INSERT INTO [dbo].[Countries] ([Id], [Name], [Code], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES ('9f8fd4a5-5685-4c80-8a64-4f636cce1004', N'Fransa', 'FR', 0, @Now, @Now, @SeedUser, @SeedUser);

                IF NOT EXISTS (SELECT 1 FROM [dbo].[Countries] WHERE [Code] = 'NL')
                    INSERT INTO [dbo].[Countries] ([Id], [Name], [Code], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES ('9f8fd4a5-5685-4c80-8a64-4f636cce1005', N'Hollanda', 'NL', 0, @Now, @Now, @SeedUser, @SeedUser);

                IF NOT EXISTS (SELECT 1 FROM [dbo].[Iller] WHERE [Ad] = N'Istanbul' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[Iller] ([Ad], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'Istanbul', 1, 0, @Now, @Now, @SeedUser, @SeedUser);

                IF NOT EXISTS (SELECT 1 FROM [dbo].[Iller] WHERE [Ad] = N'Ankara' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[Iller] ([Ad], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'Ankara', 1, 0, @Now, @Now, @SeedUser, @SeedUser);

                IF NOT EXISTS (SELECT 1 FROM [dbo].[Iller] WHERE [Ad] = N'Izmir' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[Iller] ([Ad], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'Izmir', 1, 0, @Now, @Now, @SeedUser, @SeedUser);

                IF NOT EXISTS (SELECT 1 FROM [dbo].[Iller] WHERE [Ad] = N'Antalya' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[Iller] ([Ad], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'Antalya', 1, 0, @Now, @Now, @SeedUser, @SeedUser);

                DECLARE @IlIstanbulId int = (SELECT TOP(1) [Id] FROM [dbo].[Iller] WHERE [Ad] = N'Istanbul' AND [AktifMi] = 1 AND [IsDeleted] = 0);
                DECLARE @IlAnkaraId int = (SELECT TOP(1) [Id] FROM [dbo].[Iller] WHERE [Ad] = N'Ankara' AND [AktifMi] = 1 AND [IsDeleted] = 0);
                DECLARE @IlIzmirId int = (SELECT TOP(1) [Id] FROM [dbo].[Iller] WHERE [Ad] = N'Izmir' AND [AktifMi] = 1 AND [IsDeleted] = 0);
                DECLARE @IlAntalyaId int = (SELECT TOP(1) [Id] FROM [dbo].[Iller] WHERE [Ad] = N'Antalya' AND [AktifMi] = 1 AND [IsDeleted] = 0);

                IF @IlIstanbulId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Tesisler] WHERE [IlId] = @IlIstanbulId AND [Ad] = N'Marmara Yasam Kampusu' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[Tesisler] ([Ad], [IlId], [Telefon], [Adres], [Eposta], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'Marmara Yasam Kampusu', @IlIstanbulId, N'+90 212 555 01 01', N'Atakoy Mahallesi 12. Sokak No:7 Bakirkoy/Istanbul', N'info@marmarayasam.test', 1, 0, @Now, @Now, @SeedUser, @SeedUser);

                IF @IlAnkaraId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Tesisler] WHERE [IlId] = @IlAnkaraId AND [Ad] = N'Anadolu Konukevi' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[Tesisler] ([Ad], [IlId], [Telefon], [Adres], [Eposta], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'Anadolu Konukevi', @IlAnkaraId, N'+90 312 555 02 02', N'Cankaya Cevre Yolu Caddesi No:15 Cankaya/Ankara', N'rezervasyon@anadolukonukevi.test', 1, 0, @Now, @Now, @SeedUser, @SeedUser);

                IF @IlIzmirId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Tesisler] WHERE [IlId] = @IlIzmirId AND [Ad] = N'Ege Egitim Tesisi' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[Tesisler] ([Ad], [IlId], [Telefon], [Adres], [Eposta], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'Ege Egitim Tesisi', @IlIzmirId, N'+90 232 555 03 03', N'Kordonboyu Bulvari No:24 Konak/Izmir', N'iletisim@egeegitim.test', 1, 0, @Now, @Now, @SeedUser, @SeedUser);

                IF @IlAntalyaId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Tesisler] WHERE [IlId] = @IlAntalyaId AND [Ad] = N'Akdeniz Dinlenme Merkezi' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[Tesisler] ([Ad], [IlId], [Telefon], [Adres], [Eposta], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'Akdeniz Dinlenme Merkezi', @IlAntalyaId, N'+90 242 555 04 04', N'Lara Turizm Yolu No:38 Muratpasa/Antalya', N'info@akdenizdinlenme.test', 1, 0, @Now, @Now, @SeedUser, @SeedUser);

                DECLARE @TesisMarmaraId int = (SELECT TOP(1) [Id] FROM [dbo].[Tesisler] WHERE [IlId] = @IlIstanbulId AND [Ad] = N'Marmara Yasam Kampusu' AND [AktifMi] = 1 AND [IsDeleted] = 0);
                DECLARE @TesisAnadoluId int = (SELECT TOP(1) [Id] FROM [dbo].[Tesisler] WHERE [IlId] = @IlAnkaraId AND [Ad] = N'Anadolu Konukevi' AND [AktifMi] = 1 AND [IsDeleted] = 0);
                DECLARE @TesisEgeId int = (SELECT TOP(1) [Id] FROM [dbo].[Tesisler] WHERE [IlId] = @IlIzmirId AND [Ad] = N'Ege Egitim Tesisi' AND [AktifMi] = 1 AND [IsDeleted] = 0);
                DECLARE @TesisAkdenizId int = (SELECT TOP(1) [Id] FROM [dbo].[Tesisler] WHERE [IlId] = @IlAntalyaId AND [Ad] = N'Akdeniz Dinlenme Merkezi' AND [AktifMi] = 1 AND [IsDeleted] = 0);

                IF @TesisMarmaraId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Binalar] WHERE [TesisId] = @TesisMarmaraId AND [Ad] = N'A Blok' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[Binalar] ([Ad], [TesisId], [KatSayisi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'A Blok', @TesisMarmaraId, 6, 1, 0, @Now, @Now, @SeedUser, @SeedUser);

                IF @TesisMarmaraId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Binalar] WHERE [TesisId] = @TesisMarmaraId AND [Ad] = N'B Blok' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[Binalar] ([Ad], [TesisId], [KatSayisi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'B Blok', @TesisMarmaraId, 4, 1, 0, @Now, @Now, @SeedUser, @SeedUser);

                IF @TesisAnadoluId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Binalar] WHERE [TesisId] = @TesisAnadoluId AND [Ad] = N'Merkez Blok' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[Binalar] ([Ad], [TesisId], [KatSayisi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'Merkez Blok', @TesisAnadoluId, 5, 1, 0, @Now, @Now, @SeedUser, @SeedUser);

                IF @TesisEgeId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Binalar] WHERE [TesisId] = @TesisEgeId AND [Ad] = N'Deniz Blok' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[Binalar] ([Ad], [TesisId], [KatSayisi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'Deniz Blok', @TesisEgeId, 3, 1, 0, @Now, @Now, @SeedUser, @SeedUser);

                IF @TesisAkdenizId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Binalar] WHERE [TesisId] = @TesisAkdenizId AND [Ad] = N'Palmiye Blok' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[Binalar] ([Ad], [TesisId], [KatSayisi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'Palmiye Blok', @TesisAkdenizId, 4, 1, 0, @Now, @Now, @SeedUser, @SeedUser);

                DECLARE @BinaABlokId int = (SELECT TOP(1) [Id] FROM [dbo].[Binalar] WHERE [TesisId] = @TesisMarmaraId AND [Ad] = N'A Blok' AND [AktifMi] = 1 AND [IsDeleted] = 0);
                DECLARE @BinaBBlokId int = (SELECT TOP(1) [Id] FROM [dbo].[Binalar] WHERE [TesisId] = @TesisMarmaraId AND [Ad] = N'B Blok' AND [AktifMi] = 1 AND [IsDeleted] = 0);
                DECLARE @BinaMerkezId int = (SELECT TOP(1) [Id] FROM [dbo].[Binalar] WHERE [TesisId] = @TesisAnadoluId AND [Ad] = N'Merkez Blok' AND [AktifMi] = 1 AND [IsDeleted] = 0);
                DECLARE @BinaDenizId int = (SELECT TOP(1) [Id] FROM [dbo].[Binalar] WHERE [TesisId] = @TesisEgeId AND [Ad] = N'Deniz Blok' AND [AktifMi] = 1 AND [IsDeleted] = 0);
                DECLARE @BinaPalmiyeId int = (SELECT TOP(1) [Id] FROM [dbo].[Binalar] WHERE [TesisId] = @TesisAkdenizId AND [Ad] = N'Palmiye Blok' AND [AktifMi] = 1 AND [IsDeleted] = 0);

                IF @BinaABlokId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[IsletmeAlanlari] WHERE [BinaId] = @BinaABlokId AND [Ad] = N'Resepsiyon' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[IsletmeAlanlari] ([Ad], [BinaId], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'Resepsiyon', @BinaABlokId, 1, 0, @Now, @Now, @SeedUser, @SeedUser);

                IF @BinaABlokId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[IsletmeAlanlari] WHERE [BinaId] = @BinaABlokId AND [Ad] = N'Toplanti Salonu' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[IsletmeAlanlari] ([Ad], [BinaId], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'Toplanti Salonu', @BinaABlokId, 1, 0, @Now, @Now, @SeedUser, @SeedUser);

                IF @BinaBBlokId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[IsletmeAlanlari] WHERE [BinaId] = @BinaBBlokId AND [Ad] = N'Camasirhane' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[IsletmeAlanlari] ([Ad], [BinaId], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'Camasirhane', @BinaBBlokId, 1, 0, @Now, @Now, @SeedUser, @SeedUser);

                IF @BinaMerkezId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[IsletmeAlanlari] WHERE [BinaId] = @BinaMerkezId AND [Ad] = N'Yemekhane' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[IsletmeAlanlari] ([Ad], [BinaId], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'Yemekhane', @BinaMerkezId, 1, 0, @Now, @Now, @SeedUser, @SeedUser);

                IF @BinaDenizId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[IsletmeAlanlari] WHERE [BinaId] = @BinaDenizId AND [Ad] = N'Calisma Alani' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[IsletmeAlanlari] ([Ad], [BinaId], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'Calisma Alani', @BinaDenizId, 1, 0, @Now, @Now, @SeedUser, @SeedUser);

                IF @BinaPalmiyeId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[IsletmeAlanlari] WHERE [BinaId] = @BinaPalmiyeId AND [Ad] = N'Fitness Salonu' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[IsletmeAlanlari] ([Ad], [BinaId], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'Fitness Salonu', @BinaPalmiyeId, 1, 0, @Now, @Now, @SeedUser, @SeedUser);

                IF NOT EXISTS (SELECT 1 FROM [dbo].[OdaTipleri] WHERE [Ad] = N'Standart Tek Kisilik' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[OdaTipleri] ([Ad], [PaylasimliMi], [Kapasite], [BalkonVarMi], [KlimaVarMi], [Metrekare], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'Standart Tek Kisilik', 0, 1, 0, 1, 18.50, 1, 0, @Now, @Now, @SeedUser, @SeedUser);

                IF NOT EXISTS (SELECT 1 FROM [dbo].[OdaTipleri] WHERE [Ad] = N'Standart Cift Kisilik' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[OdaTipleri] ([Ad], [PaylasimliMi], [Kapasite], [BalkonVarMi], [KlimaVarMi], [Metrekare], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'Standart Cift Kisilik', 0, 2, 0, 1, 24.00, 1, 0, @Now, @Now, @SeedUser, @SeedUser);

                IF NOT EXISTS (SELECT 1 FROM [dbo].[OdaTipleri] WHERE [Ad] = N'Aile Odasi' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[OdaTipleri] ([Ad], [PaylasimliMi], [Kapasite], [BalkonVarMi], [KlimaVarMi], [Metrekare], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'Aile Odasi', 0, 4, 1, 1, 38.00, 1, 0, @Now, @Now, @SeedUser, @SeedUser);

                IF NOT EXISTS (SELECT 1 FROM [dbo].[OdaTipleri] WHERE [Ad] = N'Deluxe Suite' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[OdaTipleri] ([Ad], [PaylasimliMi], [Kapasite], [BalkonVarMi], [KlimaVarMi], [Metrekare], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'Deluxe Suite', 0, 3, 1, 1, 42.50, 1, 0, @Now, @Now, @SeedUser, @SeedUser);

                IF NOT EXISTS (SELECT 1 FROM [dbo].[OdaTipleri] WHERE [Ad] = N'Paylasimli Oda' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[OdaTipleri] ([Ad], [PaylasimliMi], [Kapasite], [BalkonVarMi], [KlimaVarMi], [Metrekare], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'Paylasimli Oda', 1, 6, 0, 1, 46.00, 1, 0, @Now, @Now, @SeedUser, @SeedUser);

                DECLARE @OdaTipTekId int = (SELECT TOP(1) [Id] FROM [dbo].[OdaTipleri] WHERE [Ad] = N'Standart Tek Kisilik' AND [AktifMi] = 1 AND [IsDeleted] = 0);
                DECLARE @OdaTipCiftId int = (SELECT TOP(1) [Id] FROM [dbo].[OdaTipleri] WHERE [Ad] = N'Standart Cift Kisilik' AND [AktifMi] = 1 AND [IsDeleted] = 0);
                DECLARE @OdaTipAileId int = (SELECT TOP(1) [Id] FROM [dbo].[OdaTipleri] WHERE [Ad] = N'Aile Odasi' AND [AktifMi] = 1 AND [IsDeleted] = 0);
                DECLARE @OdaTipDeluxeId int = (SELECT TOP(1) [Id] FROM [dbo].[OdaTipleri] WHERE [Ad] = N'Deluxe Suite' AND [AktifMi] = 1 AND [IsDeleted] = 0);
                DECLARE @OdaTipPaylasimliId int = (SELECT TOP(1) [Id] FROM [dbo].[OdaTipleri] WHERE [Ad] = N'Paylasimli Oda' AND [AktifMi] = 1 AND [IsDeleted] = 0);

                IF @BinaABlokId IS NOT NULL AND @OdaTipTekId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Odalar] WHERE [BinaId] = @BinaABlokId AND [OdaNo] = N'101' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[Odalar] ([OdaNo], [BinaId], [OdaTipiId], [KatNo], [YatakSayisi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'101', @BinaABlokId, @OdaTipTekId, 1, 1, 1, 0, @Now, @Now, @SeedUser, @SeedUser);

                IF @BinaABlokId IS NOT NULL AND @OdaTipCiftId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Odalar] WHERE [BinaId] = @BinaABlokId AND [OdaNo] = N'102' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[Odalar] ([OdaNo], [BinaId], [OdaTipiId], [KatNo], [YatakSayisi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'102', @BinaABlokId, @OdaTipCiftId, 1, 2, 1, 0, @Now, @Now, @SeedUser, @SeedUser);

                IF @BinaABlokId IS NOT NULL AND @OdaTipDeluxeId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Odalar] WHERE [BinaId] = @BinaABlokId AND [OdaNo] = N'201' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[Odalar] ([OdaNo], [BinaId], [OdaTipiId], [KatNo], [YatakSayisi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'201', @BinaABlokId, @OdaTipDeluxeId, 2, 3, 1, 0, @Now, @Now, @SeedUser, @SeedUser);

                IF @BinaBBlokId IS NOT NULL AND @OdaTipPaylasimliId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Odalar] WHERE [BinaId] = @BinaBBlokId AND [OdaNo] = N'001' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[Odalar] ([OdaNo], [BinaId], [OdaTipiId], [KatNo], [YatakSayisi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'001', @BinaBBlokId, @OdaTipPaylasimliId, 0, 6, 1, 0, @Now, @Now, @SeedUser, @SeedUser);

                IF @BinaBBlokId IS NOT NULL AND @OdaTipCiftId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Odalar] WHERE [BinaId] = @BinaBBlokId AND [OdaNo] = N'103' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[Odalar] ([OdaNo], [BinaId], [OdaTipiId], [KatNo], [YatakSayisi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'103', @BinaBBlokId, @OdaTipCiftId, 1, 2, 1, 0, @Now, @Now, @SeedUser, @SeedUser);

                IF @BinaMerkezId IS NOT NULL AND @OdaTipAileId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Odalar] WHERE [BinaId] = @BinaMerkezId AND [OdaNo] = N'301' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[Odalar] ([OdaNo], [BinaId], [OdaTipiId], [KatNo], [YatakSayisi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'301', @BinaMerkezId, @OdaTipAileId, 3, 4, 1, 0, @Now, @Now, @SeedUser, @SeedUser);

                IF @BinaMerkezId IS NOT NULL AND @OdaTipTekId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Odalar] WHERE [BinaId] = @BinaMerkezId AND [OdaNo] = N'302' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[Odalar] ([OdaNo], [BinaId], [OdaTipiId], [KatNo], [YatakSayisi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'302', @BinaMerkezId, @OdaTipTekId, 3, 1, 1, 0, @Now, @Now, @SeedUser, @SeedUser);

                IF @BinaDenizId IS NOT NULL AND @OdaTipCiftId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Odalar] WHERE [BinaId] = @BinaDenizId AND [OdaNo] = N'201' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[Odalar] ([OdaNo], [BinaId], [OdaTipiId], [KatNo], [YatakSayisi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'201', @BinaDenizId, @OdaTipCiftId, 2, 2, 1, 0, @Now, @Now, @SeedUser, @SeedUser);

                IF @BinaDenizId IS NOT NULL AND @OdaTipAileId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Odalar] WHERE [BinaId] = @BinaDenizId AND [OdaNo] = N'202' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[Odalar] ([OdaNo], [BinaId], [OdaTipiId], [KatNo], [YatakSayisi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'202', @BinaDenizId, @OdaTipAileId, 2, 4, 1, 0, @Now, @Now, @SeedUser, @SeedUser);

                IF @BinaPalmiyeId IS NOT NULL AND @OdaTipDeluxeId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Odalar] WHERE [BinaId] = @BinaPalmiyeId AND [OdaNo] = N'401' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[Odalar] ([OdaNo], [BinaId], [OdaTipiId], [KatNo], [YatakSayisi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'401', @BinaPalmiyeId, @OdaTipDeluxeId, 4, 3, 1, 0, @Now, @Now, @SeedUser, @SeedUser);

                IF @BinaPalmiyeId IS NOT NULL AND @OdaTipCiftId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Odalar] WHERE [BinaId] = @BinaPalmiyeId AND [OdaNo] = N'402' AND [AktifMi] = 1 AND [IsDeleted] = 0)
                    INSERT INTO [dbo].[Odalar] ([OdaNo], [BinaId], [OdaTipiId], [KatNo], [YatakSayisi], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                    VALUES (N'402', @BinaPalmiyeId, @OdaTipCiftId, 4, 2, 1, 0, @Now, @Now, @SeedUser, @SeedUser);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @SeedUser nvarchar(128) = N'migration_seed_domain_test_data';

                DELETE FROM [dbo].[Odalar] WHERE [CreatedBy] = @SeedUser;
                DELETE FROM [dbo].[IsletmeAlanlari] WHERE [CreatedBy] = @SeedUser;
                DELETE FROM [dbo].[Binalar] WHERE [CreatedBy] = @SeedUser;
                DELETE FROM [dbo].[Tesisler] WHERE [CreatedBy] = @SeedUser;
                DELETE FROM [dbo].[Iller] WHERE [CreatedBy] = @SeedUser;
                DELETE FROM [dbo].[OdaTipleri] WHERE [CreatedBy] = @SeedUser;
                DELETE FROM [dbo].[Countries] WHERE [CreatedBy] = @SeedUser;
                """);
        }
    }
}
