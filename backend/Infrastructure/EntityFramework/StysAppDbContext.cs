using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using STYS.Binalar.Entities;
using STYS.Countries.Entities;
using STYS.Fiyatlandirma.Entities;
using STYS.Iller.Entities;
using STYS.IsletmeAlanlari.Entities;
using STYS.KonaklamaTipleri.Entities;
using STYS.Kullanicilar.Entities;
using STYS.MisafirTipleri.Entities;
using STYS.OdaKullanimBloklari.Entities;
using STYS.OdaOzellikleri.Entities;
using STYS.OdaSiniflari.Entities;
using STYS.Odalar;
using STYS.Odalar.Entities;
using STYS.OdaTipleri.Entities;
using STYS.Rezervasyonlar.Entities;
using STYS.SezonKurallari.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;
using TOD.Platform.Security.Auth.Services;

namespace STYS.Infrastructure.EntityFramework;

public class StysAppDbContext : DbContext
{
    private readonly ICurrentUserAccessor? _currentUserAccessor;

    public StysAppDbContext(DbContextOptions<StysAppDbContext> options, ICurrentUserAccessor? currentUserAccessor = null)
        : base(options)
    {
        _currentUserAccessor = currentUserAccessor;
    }

    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Il> Iller => Set<Il>();
    public DbSet<Tesis> Tesisler => Set<Tesis>();
    public DbSet<TesisYonetici> TesisYoneticileri => Set<TesisYonetici>();
    public DbSet<TesisResepsiyonist> TesisResepsiyonistleri => Set<TesisResepsiyonist>();
    public DbSet<KullaniciTesisSahiplik> KullaniciTesisSahiplikleri => Set<KullaniciTesisSahiplik>();
    public DbSet<Bina> Binalar => Set<Bina>();
    public DbSet<BinaYonetici> BinaYoneticileri => Set<BinaYonetici>();
    public DbSet<IsletmeAlaniSinifi> IsletmeAlaniSiniflari => Set<IsletmeAlaniSinifi>();
    public DbSet<IsletmeAlani> IsletmeAlanlari => Set<IsletmeAlani>();
    public DbSet<OdaSinifi> OdaSiniflari => Set<OdaSinifi>();
    public DbSet<OdaOzellik> OdaOzellikleri => Set<OdaOzellik>();
    public DbSet<OdaOzellikDeger> OdaOzellikDegerleri => Set<OdaOzellikDeger>();
    public DbSet<OdaTipi> OdaTipleri => Set<OdaTipi>();
    public DbSet<TesisOdaTipiOzellikDeger> TesisOdaTipiOzellikDegerleri => Set<TesisOdaTipiOzellikDeger>();
    public DbSet<Oda> Odalar => Set<Oda>();
    public DbSet<KonaklamaTipi> KonaklamaTipleri => Set<KonaklamaTipi>();
    public DbSet<MisafirTipi> MisafirTipleri => Set<MisafirTipi>();
    public DbSet<OdaFiyat> OdaFiyatlari => Set<OdaFiyat>();
    public DbSet<IndirimKurali> IndirimKurallari => Set<IndirimKurali>();
    public DbSet<IndirimKuraliMisafirTipi> IndirimKuraliMisafirTipleri => Set<IndirimKuraliMisafirTipi>();
    public DbSet<IndirimKuraliKonaklamaTipi> IndirimKuraliKonaklamaTipleri => Set<IndirimKuraliKonaklamaTipi>();
    public DbSet<SezonKurali> SezonKurallari => Set<SezonKurali>();
    public DbSet<OdaKullanimBlok> OdaKullanimBloklari => Set<OdaKullanimBlok>();
    public DbSet<Rezervasyon> Rezervasyonlar => Set<Rezervasyon>();
    public DbSet<RezervasyonSegment> RezervasyonSegmentleri => Set<RezervasyonSegment>();
    public DbSet<RezervasyonSegmentOdaAtama> RezervasyonSegmentOdaAtamalari => Set<RezervasyonSegmentOdaAtama>();
    public DbSet<RezervasyonDegisiklikGecmisi> RezervasyonDegisiklikGecmisleri => Set<RezervasyonDegisiklikGecmisi>();
    public DbSet<RezervasyonKonaklayan> RezervasyonKonaklayanlar => Set<RezervasyonKonaklayan>();
    public DbSet<RezervasyonKonaklayanSegmentAtama> RezervasyonKonaklayanSegmentAtamalari => Set<RezervasyonKonaklayanSegmentAtama>();
    public DbSet<RezervasyonOdeme> RezervasyonOdemeler => Set<RezervasyonOdeme>();

    public override int SaveChanges()
    {
        ApplyAuditInfo();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInfo();
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("dbo");

        modelBuilder.Entity<Country>(entity =>
        {
            entity.ToTable("Countries", "dbo");
            entity.Property(x => x.Name).HasMaxLength(128);
            entity.Property(x => x.Code).HasMaxLength(16);
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<Il>(entity =>
        {
            entity.ToTable("Iller", "dbo");
            entity.Property(x => x.Ad).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => x.Ad)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [AktifMi] = 1");
        });

        modelBuilder.Entity<Tesis>(entity =>
        {
            entity.ToTable("Tesisler", "dbo");
            entity.Property(x => x.Ad).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Telefon).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Adres).HasMaxLength(512).IsRequired();
            entity.Property(x => x.Eposta).HasMaxLength(256);
            entity.Property(x => x.GirisSaati).HasColumnType("time(0)").HasDefaultValue(new TimeSpan(14, 0, 0));
            entity.Property(x => x.CikisSaati).HasColumnType("time(0)").HasDefaultValue(new TimeSpan(10, 0, 0));

            entity.HasIndex(x => new { x.IlId, x.Ad })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [AktifMi] = 1");

            entity.HasOne(x => x.Il)
                .WithMany(x => x.Tesisler)
                .HasForeignKey(x => x.IlId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TesisYonetici>(entity =>
        {
            entity.ToTable("TesisYoneticileri", "dbo");
            entity.HasIndex(x => new { x.TesisId, x.UserId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.Tesis)
                .WithMany(x => x.Yoneticiler)
                .HasForeignKey(x => x.TesisId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TesisResepsiyonist>(entity =>
        {
            entity.ToTable("TesisResepsiyonistleri", "dbo");
            entity.HasIndex(x => new { x.TesisId, x.UserId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.Tesis)
                .WithMany(x => x.Resepsiyonistler)
                .HasForeignKey(x => x.TesisId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<KullaniciTesisSahiplik>(entity =>
        {
            entity.ToTable("KullaniciTesisSahiplikleri", "dbo");
            entity.HasIndex(x => x.UserId)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.TesisId);

            entity.HasOne(x => x.Tesis)
                .WithMany()
                .HasForeignKey(x => x.TesisId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Bina>(entity =>
        {
            entity.ToTable("Binalar", "dbo");
            entity.Property(x => x.Ad).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => new { x.TesisId, x.Ad })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [AktifMi] = 1");

            entity.HasOne(x => x.Tesis)
                .WithMany(x => x.Binalar)
                .HasForeignKey(x => x.TesisId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BinaYonetici>(entity =>
        {
            entity.ToTable("BinaYoneticileri", "dbo");
            entity.HasIndex(x => new { x.BinaId, x.UserId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.Bina)
                .WithMany(x => x.Yoneticiler)
                .HasForeignKey(x => x.BinaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<IsletmeAlani>(entity =>
        {
            entity.ToTable("IsletmeAlanlari", "dbo");
            entity.Property(x => x.OzelAd).HasMaxLength(200);
            entity.HasIndex(x => new { x.BinaId, x.IsletmeAlaniSinifiId })
                .HasFilter("[IsDeleted] = 0 AND [AktifMi] = 1");

            entity.HasOne(x => x.Bina)
                .WithMany(x => x.IsletmeAlanlari)
                .HasForeignKey(x => x.BinaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.IsletmeAlaniSinifi)
                .WithMany(x => x.IsletmeAlanlari)
                .HasForeignKey(x => x.IsletmeAlaniSinifiId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<IsletmeAlaniSinifi>(entity =>
        {
            entity.ToTable("IsletmeAlaniSiniflari", "dbo");
            entity.Property(x => x.Kod).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Ad).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => x.Kod)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.Ad)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
        });

        modelBuilder.Entity<OdaSinifi>(entity =>
        {
            entity.ToTable("OdaSiniflari", "dbo");
            entity.Property(x => x.Kod).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Ad).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => x.Kod)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [AktifMi] = 1");
            entity.HasIndex(x => x.Ad)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [AktifMi] = 1");
        });

        modelBuilder.Entity<OdaOzellik>(entity =>
        {
            entity.ToTable("OdaOzellikleri", "dbo");
            entity.Property(x => x.Kod).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Ad).HasMaxLength(128).IsRequired();
            entity.Property(x => x.VeriTipi).HasMaxLength(16).IsRequired();
            entity.HasIndex(x => x.Kod)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [AktifMi] = 1");
            entity.HasIndex(x => x.Ad)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [AktifMi] = 1");
        });

        modelBuilder.Entity<OdaTipi>(entity =>
        {
            entity.ToTable("TesisOdaTipleri", "dbo");
            entity.Property(x => x.Ad).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => new { x.TesisId, x.Ad })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [AktifMi] = 1");

            entity.HasOne(x => x.Tesis)
                .WithMany(x => x.OdaTipleri)
                .HasForeignKey(x => x.TesisId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OdaSinifi)
                .WithMany(x => x.OdaTipleri)
                .HasForeignKey(x => x.OdaSinifiId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Oda>(entity =>
        {
            entity.ToTable("Odalar", "dbo");
            entity.Property(x => x.OdaNo).HasMaxLength(64).IsRequired();
            entity.Property(x => x.TemizlikDurumu)
                .HasMaxLength(32)
                .IsRequired()
                .HasDefaultValue(OdaTemizlikDurumlari.Hazir);
            entity.HasIndex(x => new { x.BinaId, x.OdaNo })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [AktifMi] = 1");

            entity.HasOne(x => x.Bina)
                .WithMany(x => x.Odalar)
                .HasForeignKey(x => x.BinaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.TesisOdaTipi)
                .WithMany(x => x.Odalar)
                .HasForeignKey(x => x.TesisOdaTipiId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TesisOdaTipiOzellikDeger>(entity =>
        {
            entity.ToTable("TesisOdaTipiOzellikDegerleri", "dbo");
            entity.Property(x => x.Deger).HasMaxLength(512);
            entity.HasIndex(x => new { x.TesisOdaTipiId, x.OdaOzellikId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.TesisOdaTipi)
                .WithMany(x => x.OdaOzellikDegerleri)
                .HasForeignKey(x => x.TesisOdaTipiId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OdaOzellik)
                .WithMany()
                .HasForeignKey(x => x.OdaOzellikId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OdaOzellikDeger>(entity =>
        {
            entity.ToTable("OdaOzellikDegerleri", "dbo");
            entity.Property(x => x.Deger).HasMaxLength(512);
            entity.HasIndex(x => new { x.OdaId, x.OdaOzellikId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.Oda)
                .WithMany(x => x.OdaOzellikDegerleri)
                .HasForeignKey(x => x.OdaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OdaOzellik)
                .WithMany(x => x.OdaDegerleri)
                .HasForeignKey(x => x.OdaOzellikId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<KonaklamaTipi>(entity =>
        {
            entity.ToTable("KonaklamaTipleri", "dbo");
            entity.Property(x => x.Kod).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Ad).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => x.Kod)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.Ad)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
        });

        modelBuilder.Entity<MisafirTipi>(entity =>
        {
            entity.ToTable("MisafirTipleri", "dbo");
            entity.Property(x => x.Kod).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Ad).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => x.Kod)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.Ad)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
        });

        modelBuilder.Entity<OdaFiyat>(entity =>
        {
            entity.ToTable("OdaFiyatlari", "dbo");
            entity.Property(x => x.ParaBirimi).HasMaxLength(3).IsRequired();
            entity.Property(x => x.Fiyat).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.TesisOdaTipiId, x.KonaklamaTipiId, x.MisafirTipiId, x.KisiSayisi, x.BaslangicTarihi, x.BitisTarihi })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.TesisOdaTipi)
                .WithMany()
                .HasForeignKey(x => x.TesisOdaTipiId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.KonaklamaTipi)
                .WithMany(x => x.OdaFiyatlari)
                .HasForeignKey(x => x.KonaklamaTipiId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.MisafirTipi)
                .WithMany(x => x.OdaFiyatlari)
                .HasForeignKey(x => x.MisafirTipiId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<IndirimKurali>(entity =>
        {
            entity.ToTable("IndirimKurallari", "dbo");
            entity.Property(x => x.Kod).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Ad).HasMaxLength(200).IsRequired();
            entity.Property(x => x.IndirimTipi).HasMaxLength(16).IsRequired();
            entity.Property(x => x.KapsamTipi).HasMaxLength(16).IsRequired();
            entity.Property(x => x.Deger).HasPrecision(18, 2);
            entity.HasIndex(x => x.Kod)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.Tesis)
                .WithMany()
                .HasForeignKey(x => x.TesisId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<IndirimKuraliMisafirTipi>(entity =>
        {
            entity.ToTable("IndirimKuraliMisafirTipleri", "dbo");
            entity.HasIndex(x => new { x.IndirimKuraliId, x.MisafirTipiId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.IndirimKurali)
                .WithMany(x => x.MisafirTipiKisitlari)
                .HasForeignKey(x => x.IndirimKuraliId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.MisafirTipi)
                .WithMany(x => x.IndirimKuralMisafirTipleri)
                .HasForeignKey(x => x.MisafirTipiId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<IndirimKuraliKonaklamaTipi>(entity =>
        {
            entity.ToTable("IndirimKuraliKonaklamaTipleri", "dbo");
            entity.HasIndex(x => new { x.IndirimKuraliId, x.KonaklamaTipiId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.IndirimKurali)
                .WithMany(x => x.KonaklamaTipiKisitlari)
                .HasForeignKey(x => x.IndirimKuraliId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.KonaklamaTipi)
                .WithMany(x => x.IndirimKuralKonaklamaTipleri)
                .HasForeignKey(x => x.KonaklamaTipiId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SezonKurali>(entity =>
        {
            entity.ToTable("SezonKurallari", "dbo");
            entity.Property(x => x.Kod).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Ad).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => new { x.TesisId, x.Kod })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => new { x.TesisId, x.BaslangicTarihi, x.BitisTarihi })
                .HasFilter("[IsDeleted] = 0 AND [AktifMi] = 1");

            entity.HasOne(x => x.Tesis)
                .WithMany()
                .HasForeignKey(x => x.TesisId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OdaKullanimBlok>(entity =>
        {
            entity.ToTable("OdaKullanimBloklari", "dbo");
            entity.Property(x => x.BlokTipi).HasMaxLength(16).IsRequired();
            entity.Property(x => x.Aciklama).HasMaxLength(512);
            entity.HasIndex(x => new { x.TesisId, x.OdaId, x.BaslangicTarihi, x.BitisTarihi })
                .HasFilter("[IsDeleted] = 0 AND [AktifMi] = 1");
            entity.HasIndex(x => x.OdaId)
                .HasFilter("[IsDeleted] = 0 AND [AktifMi] = 1");

            entity.HasOne(x => x.Tesis)
                .WithMany()
                .HasForeignKey(x => x.TesisId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Oda)
                .WithMany()
                .HasForeignKey(x => x.OdaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Rezervasyon>(entity =>
        {
            entity.ToTable("Rezervasyonlar", "dbo");
            entity.Property(x => x.ReferansNo).HasMaxLength(64).IsRequired();
            entity.Property(x => x.MisafirAdiSoyadi).HasMaxLength(200).IsRequired();
            entity.Property(x => x.MisafirTelefon).HasMaxLength(32).IsRequired();
            entity.Property(x => x.MisafirEposta).HasMaxLength(256);
            entity.Property(x => x.TcKimlikNo).HasMaxLength(32);
            entity.Property(x => x.PasaportNo).HasMaxLength(32);
            entity.Property(x => x.Notlar).HasMaxLength(1024);
            entity.Property(x => x.ToplamBazUcret).HasPrecision(18, 2);
            entity.Property(x => x.ToplamUcret).HasPrecision(18, 2);
            entity.Property(x => x.ParaBirimi).HasMaxLength(3).IsRequired();
            entity.Property(x => x.UygulananIndirimlerJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.RezervasyonDurumu).HasMaxLength(32).IsRequired();
            entity.HasIndex(x => x.ReferansNo)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => new { x.TesisId, x.GirisTarihi, x.CikisTarihi })
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.RezervasyonDurumu)
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.Tesis)
                .WithMany()
                .HasForeignKey(x => x.TesisId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RezervasyonSegment>(entity =>
        {
            entity.ToTable("RezervasyonSegmentleri", "dbo");
            entity.HasIndex(x => new { x.RezervasyonId, x.SegmentSirasi })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => new { x.BaslangicTarihi, x.BitisTarihi })
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.Rezervasyon)
                .WithMany(x => x.Segmentler)
                .HasForeignKey(x => x.RezervasyonId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RezervasyonSegmentOdaAtama>(entity =>
        {
            entity.ToTable("RezervasyonSegmentOdaAtamalari", "dbo");
            entity.Property(x => x.OdaNoSnapshot).HasMaxLength(64).IsRequired();
            entity.Property(x => x.BinaAdiSnapshot).HasMaxLength(200).IsRequired();
            entity.Property(x => x.OdaTipiAdiSnapshot).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => new { x.RezervasyonSegmentId, x.OdaId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.OdaId)
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.RezervasyonSegment)
                .WithMany(x => x.OdaAtamalari)
                .HasForeignKey(x => x.RezervasyonSegmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Oda)
                .WithMany()
                .HasForeignKey(x => x.OdaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RezervasyonDegisiklikGecmisi>(entity =>
        {
            entity.ToTable("RezervasyonDegisiklikGecmisleri", "dbo");
            entity.Property(x => x.IslemTipi).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Aciklama).HasMaxLength(512);
            entity.Property(x => x.OncekiDegerJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.YeniDegerJson).HasColumnType("nvarchar(max)");
            entity.HasIndex(x => new { x.RezervasyonId, x.CreatedAt })
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.Rezervasyon)
                .WithMany(x => x.DegisiklikGecmisiKayitlari)
                .HasForeignKey(x => x.RezervasyonId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RezervasyonKonaklayan>(entity =>
        {
            entity.ToTable("RezervasyonKonaklayanlar", "dbo");
            entity.Property(x => x.AdSoyad).HasMaxLength(200).IsRequired();
            entity.Property(x => x.TcKimlikNo).HasMaxLength(32);
            entity.Property(x => x.PasaportNo).HasMaxLength(32);
            entity.HasIndex(x => new { x.RezervasyonId, x.SiraNo })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.Rezervasyon)
                .WithMany(x => x.Konaklayanlar)
                .HasForeignKey(x => x.RezervasyonId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RezervasyonKonaklayanSegmentAtama>(entity =>
        {
            entity.ToTable("RezervasyonKonaklayanSegmentAtamalari", "dbo");
            entity.HasIndex(x => new { x.RezervasyonKonaklayanId, x.RezervasyonSegmentId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => new { x.RezervasyonSegmentId, x.OdaId })
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.RezervasyonKonaklayan)
                .WithMany(x => x.SegmentAtamalari)
                .HasForeignKey(x => x.RezervasyonKonaklayanId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RezervasyonSegment)
                .WithMany(x => x.KonaklayanAtamalari)
                .HasForeignKey(x => x.RezervasyonSegmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Oda)
                .WithMany()
                .HasForeignKey(x => x.OdaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RezervasyonOdeme>(entity =>
        {
            entity.ToTable("RezervasyonOdemeler", "dbo");
            entity.Property(x => x.OdemeTutari).HasPrecision(18, 2);
            entity.Property(x => x.ParaBirimi).HasMaxLength(3).IsRequired();
            entity.Property(x => x.OdemeTipi).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Aciklama).HasMaxLength(512);
            entity.HasIndex(x => new { x.RezervasyonId, x.OdemeTarihi })
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.Rezervasyon)
                .WithMany(x => x.Odemeler)
                .HasForeignKey(x => x.RezervasyonId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (IsBaseEntityType(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var prop = Expression.Property(parameter, nameof(BaseEntity<int>.IsDeleted));
                var body = Expression.Equal(prop, Expression.Constant(false));
                var lambda = Expression.Lambda(body, parameter);

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }
    }

    private void ApplyAuditInfo()
    {
        var entries = ChangeTracker.Entries()
            .Where(x => x.Entity is not null && IsBaseEntityType(x.Entity.GetType()))
            .ToList();
        var now = DateTime.UtcNow;
        var user = _currentUserAccessor?.GetCurrentUserName() ?? "system";

        foreach (var entry in entries)
        {
            var entity = entry.Entity;
            switch (entry.State)
            {
                case EntityState.Added:
                    SetProperty(entity, nameof(BaseEntity<int>.CreatedAt), now);
                    SetProperty(entity, nameof(BaseEntity<int>.CreatedBy), user);
                    SetProperty(entity, nameof(BaseEntity<int>.IsDeleted), false);
                    break;
                case EntityState.Modified:
                    SetProperty(entity, nameof(BaseEntity<int>.UpdatedAt), now);
                    SetProperty(entity, nameof(BaseEntity<int>.UpdatedBy), user);
                    break;
                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    SetProperty(entity, nameof(BaseEntity<int>.IsDeleted), true);
                    SetProperty(entity, nameof(BaseEntity<int>.DeletedAt), now);
                    SetProperty(entity, nameof(BaseEntity<int>.DeletedBy), user);
                    break;
            }
        }
    }

    private static bool IsBaseEntityType(Type entityType)
    {
        var current = entityType;
        while (current is not null)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(BaseEntity<>))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    private static void SetProperty(object entity, string propertyName, object? value)
    {
        var property = entity.GetType().GetProperty(propertyName);
        if (property is null || !property.CanWrite)
        {
            return;
        }

        property.SetValue(entity, value);
    }
}
