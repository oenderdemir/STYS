using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    [DbContext(typeof(StysAppDbContext))]
    [Migration("20260305203000_SeedTestReservations")]
    public class SeedTestReservations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                DECLARE @SeedTag nvarchar(128) = N'migration_seed_rezervasyon_test';

                IF EXISTS (SELECT 1 FROM dbo.Rezervasyonlar WHERE CreatedBy = @SeedTag AND IsDeleted = 0)
                BEGIN
                    RETURN;
                END;

                DECLARE @Tesis1Id int = (
                    SELECT TOP (1) t.Id
                    FROM dbo.Tesisler t
                    WHERE t.AktifMi = 1 AND t.IsDeleted = 0
                    ORDER BY t.Id
                );

                DECLARE @Tesis2Id int = (
                    SELECT TOP (1) t.Id
                    FROM dbo.Tesisler t
                    WHERE t.AktifMi = 1 AND t.IsDeleted = 0
                      AND t.Id <> ISNULL(@Tesis1Id, -1)
                    ORDER BY t.Id
                );

                DECLARE @Oda11Id int = (
                    SELECT TOP (1) o.Id
                    FROM dbo.Odalar o
                    INNER JOIN dbo.Binalar b ON b.Id = o.BinaId
                    INNER JOIN dbo.TesisOdaTipleri ot ON ot.Id = o.TesisOdaTipiId
                    WHERE o.AktifMi = 1 AND o.IsDeleted = 0
                      AND b.AktifMi = 1 AND b.IsDeleted = 0
                      AND b.TesisId = @Tesis1Id
                      AND ot.AktifMi = 1 AND ot.IsDeleted = 0
                    ORDER BY CASE WHEN ot.Kapasite >= 2 THEN 0 ELSE 1 END, ot.Kapasite DESC, o.Id
                );

                DECLARE @Oda11Kapasite int = (
                    SELECT TOP (1) ot.Kapasite
                    FROM dbo.Odalar o
                    INNER JOIN dbo.TesisOdaTipleri ot ON ot.Id = o.TesisOdaTipiId
                    WHERE o.Id = @Oda11Id
                );

                DECLARE @Oda12Id int = (
                    SELECT TOP (1) o.Id
                    FROM dbo.Odalar o
                    INNER JOIN dbo.Binalar b ON b.Id = o.BinaId
                    INNER JOIN dbo.TesisOdaTipleri ot ON ot.Id = o.TesisOdaTipiId
                    WHERE o.AktifMi = 1 AND o.IsDeleted = 0
                      AND b.AktifMi = 1 AND b.IsDeleted = 0
                      AND b.TesisId = @Tesis1Id
                      AND o.Id <> ISNULL(@Oda11Id, -1)
                      AND ot.AktifMi = 1 AND ot.IsDeleted = 0
                    ORDER BY o.Id
                );

                DECLARE @Oda21Id int = (
                    SELECT TOP (1) o.Id
                    FROM dbo.Odalar o
                    INNER JOIN dbo.Binalar b ON b.Id = o.BinaId
                    INNER JOIN dbo.TesisOdaTipleri ot ON ot.Id = o.TesisOdaTipiId
                    WHERE o.AktifMi = 1 AND o.IsDeleted = 0
                      AND b.AktifMi = 1 AND b.IsDeleted = 0
                      AND b.TesisId = @Tesis2Id
                      AND ot.AktifMi = 1 AND ot.IsDeleted = 0
                    ORDER BY o.Id
                );

                IF @Tesis1Id IS NULL OR @Oda11Id IS NULL
                BEGIN
                    RETURN;
                END;

                DECLARE @R1Giris datetime2 = DATEADD(day, 2, CAST(GETUTCDATE() AS date));
                DECLARE @R1Cikis datetime2 = DATEADD(day, 5, CAST(GETUTCDATE() AS date));
                DECLARE @R2Giris datetime2 = DATEADD(day, 7, CAST(GETUTCDATE() AS date));
                DECLARE @R2Cikis datetime2 = DATEADD(day, 10, CAST(GETUTCDATE() AS date));
                DECLARE @R3Giris datetime2 = DATEADD(day, 12, CAST(GETUTCDATE() AS date));
                DECLARE @R3Cikis datetime2 = DATEADD(day, 15, CAST(GETUTCDATE() AS date));
                DECLARE @R1Kisi int = CASE WHEN ISNULL(@Oda11Kapasite, 1) >= 2 THEN 2 ELSE 1 END;
                DECLARE @R2Kisi int = CASE WHEN ISNULL(@Oda11Kapasite, 1) >= 2 THEN 2 ELSE 1 END;
                DECLARE @R3Kisi int = 1;

                DECLARE @InsertedReservations TABLE (ReservationId int, RefNo nvarchar(64));

                INSERT INTO dbo.Rezervasyonlar
                (
                    ReferansNo,
                    TesisId,
                    KisiSayisi,
                    GirisTarihi,
                    CikisTarihi,
                    MisafirAdiSoyadi,
                    MisafirTelefon,
                    MisafirEposta,
                    TcKimlikNo,
                    PasaportNo,
                    Notlar,
                    RezervasyonDurumu,
                    AktifMi,
                    IsDeleted,
                    CreatedAt,
                    UpdatedAt,
                    CreatedBy,
                    UpdatedBy
                )
                OUTPUT inserted.Id, inserted.ReferansNo INTO @InsertedReservations(ReservationId, RefNo)
                VALUES
                (
                    CONCAT(N'SEED-RZV-', FORMAT(@R1Giris, 'yyyyMMdd'), N'-A'),
                    @Tesis1Id,
                    @R1Kisi,
                    @R1Giris,
                    @R1Cikis,
                    N'Ahmet Yilmaz',
                    N'05000000001',
                    N'ahmet.yilmaz@example.com',
                    N'11111111111',
                    NULL,
                    N'Seed test rezervasyonu 1',
                    N'Onayli',
                    1,
                    0,
                    @Now,
                    @Now,
                    @SeedTag,
                    @SeedTag
                ),
                (
                    CONCAT(N'SEED-RZV-', FORMAT(@R2Giris, 'yyyyMMdd'), N'-B'),
                    @Tesis1Id,
                    @R2Kisi,
                    @R2Giris,
                    @R2Cikis,
                    N'Ayse Demir',
                    N'05000000002',
                    N'ayse.demir@example.com',
                    N'22222222222',
                    NULL,
                    N'Seed test rezervasyonu 2 (oda bolunmus)',
                    N'Onayli',
                    1,
                    0,
                    @Now,
                    @Now,
                    @SeedTag,
                    @SeedTag
                );

                IF @Tesis2Id IS NOT NULL AND @Oda21Id IS NOT NULL
                BEGIN
                    INSERT INTO dbo.Rezervasyonlar
                    (
                        ReferansNo,
                        TesisId,
                        KisiSayisi,
                        GirisTarihi,
                        CikisTarihi,
                        MisafirAdiSoyadi,
                        MisafirTelefon,
                        MisafirEposta,
                        TcKimlikNo,
                        PasaportNo,
                        Notlar,
                        RezervasyonDurumu,
                        AktifMi,
                        IsDeleted,
                        CreatedAt,
                        UpdatedAt,
                        CreatedBy,
                        UpdatedBy
                    )
                    OUTPUT inserted.Id, inserted.ReferansNo INTO @InsertedReservations(ReservationId, RefNo)
                    VALUES
                    (
                        CONCAT(N'SEED-RZV-', FORMAT(@R3Giris, 'yyyyMMdd'), N'-C'),
                        @Tesis2Id,
                        @R3Kisi,
                        @R3Giris,
                        @R3Cikis,
                        N'Mehmet Kaya',
                        N'05000000003',
                        N'mehmet.kaya@example.com',
                        N'33333333333',
                        N'P1234567',
                        N'Seed test rezervasyonu 3',
                        N'Onayli',
                        1,
                        0,
                        @Now,
                        @Now,
                        @SeedTag,
                        @SeedTag
                    );
                END;

                DECLARE @InsertedSegments TABLE (
                    SegmentId int,
                    ReservationId int,
                    SegmentSirasi int
                );

                INSERT INTO dbo.RezervasyonSegmentleri
                (
                    RezervasyonId,
                    SegmentSirasi,
                    BaslangicTarihi,
                    BitisTarihi,
                    IsDeleted,
                    CreatedAt,
                    UpdatedAt,
                    CreatedBy,
                    UpdatedBy
                )
                OUTPUT inserted.Id, inserted.RezervasyonId, inserted.SegmentSirasi
                    INTO @InsertedSegments(SegmentId, ReservationId, SegmentSirasi)
                SELECT
                    r.ReservationId,
                    1,
                    rz.GirisTarihi,
                    rz.CikisTarihi,
                    0,
                    @Now,
                    @Now,
                    @SeedTag,
                    @SeedTag
                FROM @InsertedReservations r
                INNER JOIN dbo.Rezervasyonlar rz ON rz.Id = r.ReservationId;

                INSERT INTO dbo.RezervasyonSegmentOdaAtamalari
                (
                    RezervasyonSegmentId,
                    OdaId,
                    AyrilanKisiSayisi,
                    OdaNoSnapshot,
                    BinaAdiSnapshot,
                    OdaTipiAdiSnapshot,
                    PaylasimliMiSnapshot,
                    KapasiteSnapshot,
                    IsDeleted,
                    CreatedAt,
                    UpdatedAt,
                    CreatedBy,
                    UpdatedBy
                )
                SELECT
                    s.SegmentId,
                    CASE
                        WHEN rz.ReferansNo LIKE N'%-B' AND rz.KisiSayisi > 1 AND @Oda12Id IS NOT NULL THEN @Oda12Id
                        WHEN rz.TesisId = @Tesis2Id AND @Oda21Id IS NOT NULL THEN @Oda21Id
                        ELSE @Oda11Id
                    END AS OdaId,
                    CASE
                        WHEN rz.ReferansNo LIKE N'%-B' AND rz.KisiSayisi > 1 AND @Oda12Id IS NOT NULL THEN 1
                        ELSE rz.KisiSayisi
                    END AS AyrilanKisiSayisi,
                    o.OdaNo,
                    b.Ad,
                    ot.Ad,
                    ot.PaylasimliMi,
                    ot.Kapasite,
                    0,
                    @Now,
                    @Now,
                    @SeedTag,
                    @SeedTag
                FROM @InsertedSegments s
                INNER JOIN dbo.Rezervasyonlar rz ON rz.Id = s.ReservationId
                CROSS APPLY (
                    SELECT TOP (1) o0.Id, o0.OdaNo, o0.BinaId, o0.TesisOdaTipiId
                    FROM dbo.Odalar o0
                    WHERE o0.Id = CASE
                        WHEN rz.ReferansNo LIKE N'%-B' AND rz.KisiSayisi > 1 AND @Oda12Id IS NOT NULL THEN @Oda12Id
                        WHEN rz.TesisId = @Tesis2Id AND @Oda21Id IS NOT NULL THEN @Oda21Id
                        ELSE @Oda11Id
                    END
                ) o
                INNER JOIN dbo.Binalar b ON b.Id = o.BinaId
                INNER JOIN dbo.TesisOdaTipleri ot ON ot.Id = o.TesisOdaTipiId;

                IF EXISTS (
                    SELECT 1
                    FROM @InsertedSegments s
                    INNER JOIN dbo.Rezervasyonlar rz ON rz.Id = s.ReservationId
                    WHERE rz.ReferansNo LIKE N'%-B'
                      AND rz.KisiSayisi > 1
                      AND @Oda12Id IS NOT NULL
                )
                BEGIN
                    INSERT INTO dbo.RezervasyonSegmentOdaAtamalari
                    (
                        RezervasyonSegmentId,
                        OdaId,
                        AyrilanKisiSayisi,
                        OdaNoSnapshot,
                        BinaAdiSnapshot,
                        OdaTipiAdiSnapshot,
                        PaylasimliMiSnapshot,
                        KapasiteSnapshot,
                        IsDeleted,
                        CreatedAt,
                        UpdatedAt,
                        CreatedBy,
                        UpdatedBy
                    )
                    SELECT
                        s.SegmentId,
                        @Oda11Id,
                        1,
                        o.OdaNo,
                        b.Ad,
                        ot.Ad,
                        ot.PaylasimliMi,
                        ot.Kapasite,
                        0,
                        @Now,
                        @Now,
                        @SeedTag,
                        @SeedTag
                    FROM @InsertedSegments s
                    INNER JOIN dbo.Rezervasyonlar rz ON rz.Id = s.ReservationId
                    INNER JOIN dbo.Odalar o ON o.Id = @Oda11Id
                    INNER JOIN dbo.Binalar b ON b.Id = o.BinaId
                    INNER JOIN dbo.TesisOdaTipleri ot ON ot.Id = o.TesisOdaTipiId
                    WHERE rz.ReferansNo LIKE N'%-B'
                      AND rz.KisiSayisi > 1;
                END;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @SeedTag nvarchar(128) = N'migration_seed_rezervasyon_test';

                DELETE rsa
                FROM dbo.RezervasyonSegmentOdaAtamalari rsa
                INNER JOIN dbo.RezervasyonSegmentleri rs ON rs.Id = rsa.RezervasyonSegmentId
                INNER JOIN dbo.Rezervasyonlar r ON r.Id = rs.RezervasyonId
                WHERE r.CreatedBy = @SeedTag;

                DELETE rs
                FROM dbo.RezervasyonSegmentleri rs
                INNER JOIN dbo.Rezervasyonlar r ON r.Id = rs.RezervasyonId
                WHERE r.CreatedBy = @SeedTag;

                DELETE FROM dbo.Rezervasyonlar
                WHERE CreatedBy = @SeedTag;
                """);
        }
    }
}
