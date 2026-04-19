using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using STYS.Bildirimler.Entities;
using STYS.Binalar.Entities;
using STYS.Countries.Entities;
using STYS.EkHizmetler.Entities;
using STYS.Fiyatlandirma.Entities;
using STYS.Iller.Entities;
using STYS.IsletmeAlanlari.Entities;
using STYS.Kamp.Entities;
using STYS.KonaklamaTipleri.Entities;
using STYS.Kullanicilar.Entities;
using STYS.MisafirTipleri.Entities;
using STYS.Muhasebe.BankaHareketleri.Entities;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Muhasebe.Hesaplar.Entities;
using STYS.Muhasebe.KasaHareketleri.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using STYS.Muhasebe.Depolar.Entities;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.TasinirKartlari.Entities;
using STYS.Muhasebe.TasinirKodlari.Entities;
using STYS.Restoranlar.Entities;
using STYS.RestoranMasalari.Entities;
using STYS.RestoranMenuKategorileri.Entities;
using STYS.RestoranMenuUrunleri.Entities;
using STYS.RestoranSiparisleri.Entities;
using STYS.RestoranOdemeleri.Entities;
using STYS.OdaKullanimBloklari.Entities;
using STYS.OdaOzellikleri.Entities;
using STYS.OdaSiniflari.Entities;
using STYS.Odalar;
using STYS.Odalar.Entities;
using STYS.OdaTipleri.Entities;
using STYS.Rezervasyonlar;
using STYS.Rezervasyonlar.Entities;
using STYS.SezonKurallari.Entities;
using STYS.Tesisler;
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
    public DbSet<KonaklamaTipiIcerikKalemi> KonaklamaTipiIcerikKalemleri => Set<KonaklamaTipiIcerikKalemi>();
    public DbSet<TesisKonaklamaTipi> TesisKonaklamaTipleri => Set<TesisKonaklamaTipi>();
    public DbSet<TesisKonaklamaTipiIcerikOverride> TesisKonaklamaTipiIcerikOverridelari => Set<TesisKonaklamaTipiIcerikOverride>();
    public DbSet<MisafirTipi> MisafirTipleri => Set<MisafirTipi>();
    public DbSet<TesisMisafirTipi> TesisMisafirTipleri => Set<TesisMisafirTipi>();
    public DbSet<KampProgrami> KampProgramlari => Set<KampProgrami>();
    public DbSet<KampDonemi> KampDonemleri => Set<KampDonemi>();
    public DbSet<KampDonemiTesis> KampDonemiTesisleri => Set<KampDonemiTesis>();
    public DbSet<KampBasvuruSahibi> KampBasvuruSahipleri => Set<KampBasvuruSahibi>();
    public DbSet<KampBasvuruGecmisKatilim> KampBasvuruGecmisKatilimlari => Set<KampBasvuruGecmisKatilim>();
    public DbSet<KampKuralSeti> KampKuralSetleri => Set<KampKuralSeti>();
    public DbSet<KampProgramiBasvuruSahibiTipKurali> KampProgramiBasvuruSahibiTipKurallari => Set<KampProgramiBasvuruSahibiTipKurali>();
    public DbSet<KampProgramiParametreAyari> KampProgramiParametreAyarlari => Set<KampProgramiParametreAyari>();
    public DbSet<KampKonaklamaTarifesi> KampKonaklamaTarifeleri => Set<KampKonaklamaTarifesi>();
    public DbSet<KampYasUcretKurali> KampYasUcretKurallari => Set<KampYasUcretKurali>();
    public DbSet<KampBasvuruSahibiTipi> KampBasvuruSahibiTipleri => Set<KampBasvuruSahibiTipi>();
    public DbSet<KampKatilimciTipi> KampKatilimciTipleri => Set<KampKatilimciTipi>();
    public DbSet<KampAkrabalikTipi> KampAkrabalikTipleri => Set<KampAkrabalikTipi>();
    public DbSet<KampBasvuru> KampBasvurulari => Set<KampBasvuru>();
    public DbSet<KampBasvuruKatilimci> KampBasvuruKatilimcilari => Set<KampBasvuruKatilimci>();
    public DbSet<KampBasvuruTercih> KampBasvuruTercihleri => Set<KampBasvuruTercih>();
    public DbSet<KampRezervasyon> KampRezervasyonlari => Set<KampRezervasyon>();
    public DbSet<KampParametre> KampParametreleri => Set<KampParametre>();
    public DbSet<OdaFiyat> OdaFiyatlari => Set<OdaFiyat>();
    public DbSet<GlobalEkHizmetTanimi> GlobalEkHizmetTanimlari => Set<GlobalEkHizmetTanimi>();
    public DbSet<EkHizmet> EkHizmetler => Set<EkHizmet>();
    public DbSet<EkHizmetTarife> EkHizmetTarifeleri => Set<EkHizmetTarife>();
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
    public DbSet<RezervasyonKonaklamaHakki> RezervasyonKonaklamaHaklari => Set<RezervasyonKonaklamaHakki>();
    public DbSet<RezervasyonKonaklamaHakkiTuketimKaydi> RezervasyonKonaklamaHakkiTuketimKayitlari => Set<RezervasyonKonaklamaHakkiTuketimKaydi>();
    public DbSet<RezervasyonEkHizmet> RezervasyonEkHizmetler => Set<RezervasyonEkHizmet>();
    public DbSet<RezervasyonOdeme> RezervasyonOdemeler => Set<RezervasyonOdeme>();
    public DbSet<Restoran> Restoranlar => Set<Restoran>();
    public DbSet<RestoranYonetici> RestoranYoneticileri => Set<RestoranYonetici>();
    public DbSet<RestoranGarson> RestoranGarsonlari => Set<RestoranGarson>();
    public DbSet<RestoranMasa> RestoranMasalari => Set<RestoranMasa>();
    public DbSet<RestoranMenuKategori> RestoranMenuKategorileri => Set<RestoranMenuKategori>();
    public DbSet<RestoranMenuUrun> RestoranMenuUrunleri => Set<RestoranMenuUrun>();
    public DbSet<RestoranSiparis> RestoranSiparisleri => Set<RestoranSiparis>();
    public DbSet<RestoranSiparisKalemi> RestoranSiparisKalemleri => Set<RestoranSiparisKalemi>();
    public DbSet<RestoranOdeme> RestoranOdemeleri => Set<RestoranOdeme>();
    public DbSet<CariKart> CariKartlar => Set<CariKart>();
    public DbSet<CariHareket> CariHareketler => Set<CariHareket>();
    public DbSet<KasaBankaHesap> KasaBankaHesaplari => Set<KasaBankaHesap>();
    public DbSet<Hesap> Hesaplar => Set<Hesap>();
    public DbSet<HesapKasaBankaBaglanti> HesapKasaBankaBaglantilari => Set<HesapKasaBankaBaglanti>();
    public DbSet<HesapDepoBaglanti> HesapDepoBaglantilari => Set<HesapDepoBaglanti>();
    public DbSet<KasaHareket> KasaHareketleri => Set<KasaHareket>();
    public DbSet<BankaHareket> BankaHareketleri => Set<BankaHareket>();
    public DbSet<TahsilatOdemeBelgesi> TahsilatOdemeBelgeleri => Set<TahsilatOdemeBelgesi>();
    public DbSet<MuhasebeHesapPlani> MuhasebeHesapPlanlari => Set<MuhasebeHesapPlani>();
    public DbSet<TasinirKod> TasinirKodlar => Set<TasinirKod>();
    public DbSet<TasinirKart> TasinirKartlar => Set<TasinirKart>();
    public DbSet<Depo> Depolar => Set<Depo>();
    public DbSet<StokHareket> StokHareketleri => Set<StokHareket>();
    public DbSet<Bildirim> Bildirimler => Set<Bildirim>();
    public DbSet<BildirimTercih> BildirimTercihleri => Set<BildirimTercih>();

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
        const string restoranSchema = "restoran";
        const string muhasebeSchema = "muhasebe";

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
            entity.Property(x => x.EkHizmetPaketCakismaPolitikasi).HasMaxLength(16).IsRequired().HasDefaultValue(EkHizmetPaketCakismaPolitikalari.OnayIste);

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

        modelBuilder.Entity<KonaklamaTipiIcerikKalemi>(entity =>
        {
            entity.ToTable("KonaklamaTipiIcerikKalemleri", "dbo");
            entity.Property(x => x.HizmetKodu).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Periyot).HasMaxLength(32).IsRequired();
            entity.Property(x => x.KullanimTipi).HasMaxLength(32).IsRequired();
            entity.Property(x => x.KullanimNoktasi).HasMaxLength(32).IsRequired();
            entity.Property(x => x.KullanimBaslangicSaati).HasColumnType("time");
            entity.Property(x => x.KullanimBitisSaati).HasColumnType("time");
            entity.Property(x => x.Aciklama).HasMaxLength(256);
            entity.HasIndex(x => new { x.KonaklamaTipiId, x.HizmetKodu })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.KonaklamaTipi)
                .WithMany(x => x.IcerikKalemleri)
                .HasForeignKey(x => x.KonaklamaTipiId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TesisKonaklamaTipi>(entity =>
        {
            entity.ToTable("TesisKonaklamaTipleri", "dbo");
            entity.HasIndex(x => new { x.TesisId, x.KonaklamaTipiId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.Tesis)
                .WithMany(x => x.KonaklamaTipleri)
                .HasForeignKey(x => x.TesisId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.KonaklamaTipi)
                .WithMany(x => x.TesisKonaklamaTipleri)
                .HasForeignKey(x => x.KonaklamaTipiId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TesisKonaklamaTipiIcerikOverride>(entity =>
        {
            entity.ToTable("TesisKonaklamaTipiIcerikOverridelari", "dbo");
            entity.Property(x => x.Periyot).HasMaxLength(32);
            entity.Property(x => x.KullanimTipi).HasMaxLength(32);
            entity.Property(x => x.KullanimNoktasi).HasMaxLength(32);
            entity.Property(x => x.KullanimBaslangicSaati).HasColumnType("time");
            entity.Property(x => x.KullanimBitisSaati).HasColumnType("time");
            entity.Property(x => x.Aciklama).HasMaxLength(256);
            entity.HasIndex(x => new { x.TesisId, x.KonaklamaTipiIcerikKalemiId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.Tesis)
                .WithMany(x => x.KonaklamaTipiIcerikOverridelari)
                .HasForeignKey(x => x.TesisId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.KonaklamaTipiIcerikKalemi)
                .WithMany(x => x.TesisOverrideKalemleri)
                .HasForeignKey(x => x.KonaklamaTipiIcerikKalemiId)
                .OnDelete(DeleteBehavior.Restrict);
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

        modelBuilder.Entity<TesisMisafirTipi>(entity =>
        {
            entity.ToTable("TesisMisafirTipleri", "dbo");
            entity.HasIndex(x => new { x.TesisId, x.MisafirTipiId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.Tesis)
                .WithMany(x => x.MisafirTipleri)
                .HasForeignKey(x => x.TesisId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.MisafirTipi)
                .WithMany(x => x.TesisMisafirTipleri)
                .HasForeignKey(x => x.MisafirTipiId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<KampProgrami>(entity =>
        {
            entity.ToTable("KampProgramlari", "dbo");
            entity.Property(x => x.Kod).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Ad).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Aciklama).HasMaxLength(512);
            entity.Property(x => x.Yil).IsRequired();
            entity.Property(x => x.MaksimumBasvuruSayisi).IsRequired().HasDefaultValue(1);
            entity.HasIndex(x => new { x.Yil, x.Kod })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => new { x.Yil, x.Ad })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
        });

        modelBuilder.Entity<KampDonemi>(entity =>
        {
            entity.ToTable("KampDonemleri", "dbo");
            entity.Property(x => x.Kod).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Ad).HasMaxLength(160).IsRequired();
            entity.Property(x => x.BasvuruBaslangicTarihi).HasColumnType("date");
            entity.Property(x => x.BasvuruBitisTarihi).HasColumnType("date");
            entity.Property(x => x.KonaklamaBaslangicTarihi).HasColumnType("date");
            entity.Property(x => x.KonaklamaBitisTarihi).HasColumnType("date");
            entity.Property(x => x.IptalSonGun).HasColumnType("date");
            entity.HasIndex(x => x.Kod)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => new { x.KampProgramiId, x.Ad })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.KampProgrami)
                .WithMany(x => x.KampDonemleri)
                .HasForeignKey(x => x.KampProgramiId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<KampDonemiTesis>(entity =>
        {
            entity.ToTable("KampDonemiTesisleri", "dbo");
            entity.Property(x => x.Aciklama).HasMaxLength(512);
            entity.HasIndex(x => new { x.KampDonemiId, x.TesisId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.KampDonemi)
                .WithMany(x => x.TesisAtamalari)
                .HasForeignKey(x => x.KampDonemiId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Tesis)
                .WithMany(x => x.KampDonemiTesisleri)
                .HasForeignKey(x => x.TesisId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<KampBasvuruSahibi>(entity =>
        {
            entity.ToTable("KampBasvuruSahipleri", "dbo");
            entity.Property(x => x.TcKimlikNo).HasMaxLength(32);
            entity.Property(x => x.AdSoyad).HasMaxLength(200).IsRequired();
            entity.Property(x => x.BasvuruSahibiTipi).HasMaxLength(32).IsRequired();
            entity.HasIndex(x => x.TcKimlikNo)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [TcKimlikNo] IS NOT NULL");
            entity.HasIndex(x => x.UserId)
                .HasFilter("[IsDeleted] = 0 AND [UserId] IS NOT NULL");
        });

        modelBuilder.Entity<KampBasvuruGecmisKatilim>(entity =>
        {
            entity.ToTable("KampBasvuruGecmisKatilimlari", "dbo");
            entity.HasIndex(x => new { x.KampBasvuruSahibiId, x.KatilimYili })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.KampBasvuruSahibi)
                .WithMany(x => x.GecmisKatilimlar)
                .HasForeignKey(x => x.KampBasvuruSahibiId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.KaynakBasvuru)
                .WithMany()
                .HasForeignKey(x => x.KaynakBasvuruId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<KampKuralSeti>(entity =>
        {
            entity.ToTable("KampKuralSetleri", "dbo");
            entity.HasIndex(x => x.KampProgramiId)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.KampProgrami)
                .WithMany()
                .HasForeignKey(x => x.KampProgramiId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<KampProgramiBasvuruSahibiTipKurali>(entity =>
        {
            entity.ToTable("KampProgramiBasvuruSahibiTipKurallari", "dbo");
            entity.Property(x => x.VarsayilanKatilimciTipiKodu).HasMaxLength(64);
            entity.HasIndex(x => new { x.KampProgramiId, x.KampBasvuruSahibiTipiId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.KampProgrami)
                .WithMany()
                .HasForeignKey(x => x.KampProgramiId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.KampBasvuruSahibiTipi)
                .WithMany()
                .HasForeignKey(x => x.KampBasvuruSahibiTipiId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<KampProgramiParametreAyari>(entity =>
        {
            entity.ToTable("KampProgramiParametreAyarlari", "dbo");
            entity.Property(x => x.KamuAvansKisiBasi).HasPrecision(18, 2);
            entity.Property(x => x.DigerAvansKisiBasi).HasPrecision(18, 2);
            entity.Property(x => x.GecBildirimGunlukKesintiyUzdesi).HasPrecision(18, 4);
            entity.HasIndex(x => x.KampProgramiId)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.KampProgrami)
                .WithMany()
                .HasForeignKey(x => x.KampProgramiId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<KampKonaklamaTarifesi>(entity =>
        {
            entity.ToTable("KampKonaklamaTarifeleri", "dbo");
            entity.Property(x => x.Kod).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Ad).HasMaxLength(128).IsRequired();
            entity.Property(x => x.KamuGunlukUcret).HasPrecision(18, 2);
            entity.Property(x => x.DigerGunlukUcret).HasPrecision(18, 2);
            entity.Property(x => x.BuzdolabiGunlukUcret).HasPrecision(18, 2);
            entity.Property(x => x.TelevizyonGunlukUcret).HasPrecision(18, 2);
            entity.Property(x => x.KlimaGunlukUcret).HasPrecision(18, 2);
            entity.HasOne(x => x.KampProgrami)
                .WithMany()
                .HasForeignKey(x => x.KampProgramiId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.KampProgramiId, x.Kod })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.AktifMi)
                .HasFilter("[IsDeleted] = 0");
        });

        modelBuilder.Entity<KampYasUcretKurali>(entity =>
        {
            entity.ToTable("KampYasUcretKurallari", "dbo");
            entity.Property(x => x.YemekOrani).HasPrecision(5, 2);
            entity.HasIndex(x => x.AktifMi)
                .HasFilter("[IsDeleted] = 0");
        });

        modelBuilder.Entity<KampBasvuruSahibiTipi>(entity =>
        {
            entity.ToTable("KampBasvuruSahibiTipleri", "dbo");
            entity.Property(x => x.Kod).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Ad).HasMaxLength(128).IsRequired();
            entity.Property(x => x.VarsayilanKatilimciTipiKodu).HasMaxLength(64);
            entity.HasIndex(x => x.Kod)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [AktifMi] = 1");
        });

        modelBuilder.Entity<KampKatilimciTipi>(entity =>
        {
            entity.ToTable("KampKatilimciTipleri", "dbo");
            entity.Property(x => x.Kod).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Ad).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => x.Kod)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [AktifMi] = 1");
        });

        modelBuilder.Entity<KampAkrabalikTipi>(entity =>
        {
            entity.ToTable("KampAkrabalikTipleri", "dbo");
            entity.Property(x => x.Kod).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Ad).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => x.Kod)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [AktifMi] = 1");
        });

        modelBuilder.Entity<KampBasvuru>(entity =>
        {
            entity.ToTable("KampBasvurulari", "dbo");
            entity.Property(x => x.KonaklamaBirimiTipi).HasMaxLength(32).IsRequired();
            entity.Property(x => x.BasvuruNo).HasMaxLength(32).IsRequired();
            entity.Property(x => x.BasvuruSahibiAdiSoyadiSnapshot).HasMaxLength(200).IsRequired();
            entity.Property(x => x.BasvuruSahibiTipiSnapshot).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Durum).HasMaxLength(32).IsRequired();
            entity.Property(x => x.GunlukToplamTutar).HasPrecision(18, 2);
            entity.Property(x => x.DonemToplamTutar).HasPrecision(18, 2);
            entity.Property(x => x.AvansToplamTutar).HasPrecision(18, 2);
            entity.Property(x => x.KalanOdemeTutari).HasPrecision(18, 2);
            entity.Property(x => x.UyariMesajlariJson).HasColumnType("nvarchar(max)");
            entity.HasIndex(x => new { x.KampDonemiId, x.TesisId, x.Durum })
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.BasvuruNo)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.KampDonemi)
                .WithMany(x => x.Basvurular)
                .HasForeignKey(x => x.KampDonemiId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.KampBasvuruSahibi)
                .WithMany(x => x.Basvurular)
                .HasForeignKey(x => x.KampBasvuruSahibiId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Tesis)
                .WithMany(x => x.KampBasvurulari)
                .HasForeignKey(x => x.TesisId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<KampBasvuruKatilimci>(entity =>
        {
            entity.ToTable("KampBasvuruKatilimcilari", "dbo");
            entity.Property(x => x.AdSoyad).HasMaxLength(200).IsRequired();
            entity.Property(x => x.TcKimlikNo).HasMaxLength(32);
            entity.Property(x => x.KatilimciTipi).HasMaxLength(32).IsRequired();
            entity.Property(x => x.AkrabalikTipi).HasMaxLength(32).IsRequired();
            entity.Property(x => x.DogumTarihi).HasColumnType("date");
            entity.HasIndex(x => new { x.KampBasvuruId, x.TcKimlikNo })
                .HasFilter("[IsDeleted] = 0 AND [TcKimlikNo] IS NOT NULL");

            entity.HasOne(x => x.KampBasvuru)
                .WithMany(x => x.Katilimcilar)
                .HasForeignKey(x => x.KampBasvuruId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<KampBasvuruTercih>(entity =>
        {
            entity.ToTable("KampBasvuruTercihleri", "dbo");
            entity.Property(x => x.TercihSirasi).IsRequired();
            entity.Property(x => x.KonaklamaBirimiTipi).HasMaxLength(32).IsRequired();
            entity.HasIndex(x => new { x.KampBasvuruId, x.TercihSirasi })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => new { x.KampDonemiId, x.TesisId })
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.KampBasvuru)
                .WithMany(x => x.Tercihler)
                .HasForeignKey(x => x.KampBasvuruId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.KampDonemi)
                .WithMany()
                .HasForeignKey(x => x.KampDonemiId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Tesis)
                .WithMany()
                .HasForeignKey(x => x.TesisId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<KampRezervasyon>(entity =>
        {
            entity.ToTable("KampRezervasyonlari", "dbo");
            entity.Property(x => x.RezervasyonNo).HasMaxLength(32).IsRequired();
            entity.Property(x => x.BasvuruSahibiAdiSoyadi).HasMaxLength(200).IsRequired();
            entity.Property(x => x.BasvuruSahibiTipi).HasMaxLength(32).IsRequired();
            entity.Property(x => x.KonaklamaBirimiTipi).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Durum).HasMaxLength(32).IsRequired();
            entity.Property(x => x.IptalNedeni).HasMaxLength(500);
            entity.Property(x => x.DonemToplamTutar).HasPrecision(18, 2);
            entity.Property(x => x.AvansToplamTutar).HasPrecision(18, 2);
            entity.HasIndex(x => x.RezervasyonNo).IsUnique().HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.KampBasvuruId).IsUnique().HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => new { x.KampDonemiId, x.TesisId, x.Durum });

            entity.HasOne(x => x.KampBasvuru)
                .WithMany()
                .HasForeignKey(x => x.KampBasvuruId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.KampDonemi)
                .WithMany()
                .HasForeignKey(x => x.KampDonemiId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Tesis)
                .WithMany()
                .HasForeignKey(x => x.TesisId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<KampParametre>(entity =>
        {
            entity.ToTable("KampParametreleri", "dbo");
            entity.Property(x => x.Kod).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Deger).HasMaxLength(512).IsRequired();
            entity.Property(x => x.Aciklama).HasMaxLength(256);
            entity.HasIndex(x => x.Kod).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        modelBuilder.Entity<OdaFiyat>(entity =>
        {
            entity.ToTable("OdaFiyatlari", "dbo");
            entity.Property(x => x.ParaBirimi).HasMaxLength(3).IsRequired();
            entity.Property(x => x.KullanimSekli).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Fiyat).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.TesisOdaTipiId, x.KonaklamaTipiId, x.MisafirTipiId, x.KullanimSekli, x.KisiSayisi, x.BaslangicTarihi, x.BitisTarihi })
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

        modelBuilder.Entity<GlobalEkHizmetTanimi>(entity =>
        {
            entity.ToTable("GlobalEkHizmetTanimlari", "dbo");
            entity.Property(x => x.Ad).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Aciklama).HasMaxLength(512);
            entity.Property(x => x.BirimAdi).HasMaxLength(32).IsRequired();
            entity.Property(x => x.PaketIcerikHizmetKodu).HasMaxLength(64);
            entity.HasIndex(x => x.Ad)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
        });

        modelBuilder.Entity<EkHizmet>(entity =>
        {
            entity.ToTable("EkHizmetler", "dbo");
            entity.Property(x => x.Ad).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Aciklama).HasMaxLength(512);
            entity.Property(x => x.BirimAdi).HasMaxLength(32).IsRequired();
            entity.Property(x => x.PaketIcerikHizmetKodu).HasMaxLength(64);
            entity.HasIndex(x => new { x.TesisId, x.Ad })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => new { x.TesisId, x.GlobalEkHizmetTanimiId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [GlobalEkHizmetTanimiId] IS NOT NULL");

            entity.HasOne(x => x.Tesis)
                .WithMany()
                .HasForeignKey(x => x.TesisId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.GlobalEkHizmetTanimi)
                .WithMany(x => x.TesisAtamalari)
                .HasForeignKey(x => x.GlobalEkHizmetTanimiId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EkHizmetTarife>(entity =>
        {
            entity.ToTable("EkHizmetTarifeleri", "dbo");
            entity.Property(x => x.BirimFiyat).HasPrecision(18, 2);
            entity.Property(x => x.ParaBirimi).HasMaxLength(3).IsRequired();
            entity.HasIndex(x => new { x.TesisId, x.EkHizmetId, x.BaslangicTarihi, x.BitisTarihi })
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.Tesis)
                .WithMany()
                .HasForeignKey(x => x.TesisId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.EkHizmet)
                .WithMany(x => x.Tarifeler)
                .HasForeignKey(x => x.EkHizmetId)
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
            entity.Property(x => x.MisafirCinsiyeti).HasMaxLength(16);
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

            entity.HasOne(x => x.KonaklamaTipi)
                .WithMany()
                .HasForeignKey(x => x.KonaklamaTipiId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RezervasyonKonaklamaHakki>(entity =>
        {
            entity.ToTable("RezervasyonKonaklamaHaklari", "dbo");
            entity.Property(x => x.HizmetKodu).HasMaxLength(64).IsRequired();
            entity.Property(x => x.HizmetAdiSnapshot).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Periyot).HasMaxLength(32).IsRequired();
            entity.Property(x => x.PeriyotAdiSnapshot).HasMaxLength(64).IsRequired();
            entity.Property(x => x.KullanimTipi).HasMaxLength(32).IsRequired();
            entity.Property(x => x.KullanimTipiAdiSnapshot).HasMaxLength(64).IsRequired();
            entity.Property(x => x.KullanimNoktasi).HasMaxLength(32).IsRequired();
            entity.Property(x => x.KullanimNoktasiAdiSnapshot).HasMaxLength(64).IsRequired();
            entity.Property(x => x.KullanimBaslangicSaati).HasColumnType("time");
            entity.Property(x => x.KullanimBitisSaati).HasColumnType("time");
            entity.Property(x => x.AciklamaSnapshot).HasMaxLength(256);
            entity.Property(x => x.Durum).HasMaxLength(32).IsRequired();
            entity.HasIndex(x => new { x.RezervasyonId, x.HizmetKodu, x.HakTarihi, x.Periyot })
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.Rezervasyon)
                .WithMany(x => x.KonaklamaHaklari)
                .HasForeignKey(x => x.RezervasyonId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RezervasyonKonaklamaHakkiTuketimKaydi>(entity =>
        {
            entity.ToTable("RezervasyonKonaklamaHakkiTuketimKayitlari", "dbo");
            entity.Property(x => x.KullanimTipi).HasMaxLength(32).IsRequired();
            entity.Property(x => x.KullanimNoktasi).HasMaxLength(32).IsRequired();
            entity.Property(x => x.KullanimNoktasiAdiSnapshot).HasMaxLength(64).IsRequired();
            entity.Property(x => x.TuketimNoktasiAdi).HasMaxLength(128);
            entity.Property(x => x.Aciklama).HasMaxLength(256);
            entity.HasIndex(x => new { x.RezervasyonKonaklamaHakkiId, x.TuketimTarihi })
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => new { x.RezervasyonId, x.TuketimTarihi })
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.Rezervasyon)
                .WithMany(x => x.KonaklamaHakkiTuketimKayitlari)
                .HasForeignKey(x => x.RezervasyonId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RezervasyonKonaklamaHakki)
                .WithMany(x => x.TuketimKayitlari)
                .HasForeignKey(x => x.RezervasyonKonaklamaHakkiId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.IsletmeAlani)
                .WithMany()
                .HasForeignKey(x => x.IsletmeAlaniId)
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
            entity.Property(x => x.Cinsiyet).HasMaxLength(16);
            entity.Property(x => x.KatilimDurumu).HasMaxLength(16).IsRequired().HasDefaultValue(KonaklayanKatilimDurumlari.Bekleniyor);
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
            entity.HasIndex(x => new { x.RezervasyonSegmentId, x.OdaId, x.YatakNo })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [YatakNo] IS NOT NULL");

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

        modelBuilder.Entity<RezervasyonEkHizmet>(entity =>
        {
            entity.ToTable("RezervasyonEkHizmetler", "dbo");
            entity.Property(x => x.TarifeAdiSnapshot).HasMaxLength(128).IsRequired();
            entity.Property(x => x.BirimAdiSnapshot).HasMaxLength(32).IsRequired();
            entity.Property(x => x.OdaNoSnapshot).HasMaxLength(64).IsRequired();
            entity.Property(x => x.BinaAdiSnapshot).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Miktar).HasPrecision(18, 2);
            entity.Property(x => x.BirimFiyat).HasPrecision(18, 2);
            entity.Property(x => x.ToplamTutar).HasPrecision(18, 2);
            entity.Property(x => x.ParaBirimi).HasMaxLength(3).IsRequired();
            entity.Property(x => x.Aciklama).HasMaxLength(512);
            entity.HasIndex(x => new { x.RezervasyonId, x.HizmetTarihi })
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => new { x.RezervasyonKonaklayanId, x.HizmetTarihi })
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.Rezervasyon)
                .WithMany(x => x.EkHizmetler)
                .HasForeignKey(x => x.RezervasyonId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RezervasyonKonaklayan)
                .WithMany()
                .HasForeignKey(x => x.RezervasyonKonaklayanId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.EkHizmet)
                .WithMany(x => x.RezervasyonEkHizmetler)
                .HasForeignKey(x => x.EkHizmetId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.EkHizmetTarife)
                .WithMany(x => x.RezervasyonEkHizmetleri)
                .HasForeignKey(x => x.EkHizmetTarifeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RezervasyonSegment)
                .WithMany()
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

        modelBuilder.Entity<Restoran>(entity =>
        {
            entity.ToTable("Restoranlar", restoranSchema);
            entity.Property(x => x.Ad).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Aciklama).HasMaxLength(512);
            entity.HasIndex(x => new { x.TesisId, x.Ad })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [AktifMi] = 1");
            entity.HasIndex(x => x.IsletmeAlaniId)
                .HasFilter("[IsDeleted] = 0 AND [IsletmeAlaniId] IS NOT NULL");

            entity.HasOne(x => x.Tesis)
                .WithMany()
                .HasForeignKey(x => x.TesisId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.IsletmeAlani)
                .WithMany()
                .HasForeignKey(x => x.IsletmeAlaniId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RestoranYonetici>(entity =>
        {
            entity.ToTable("RestoranYoneticileri", restoranSchema);
            entity.HasIndex(x => new { x.RestoranId, x.UserId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.UserId)
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.Restoran)
                .WithMany(x => x.Yoneticiler)
                .HasForeignKey(x => x.RestoranId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RestoranGarson>(entity =>
        {
            entity.ToTable("RestoranGarsonlari", restoranSchema);
            entity.HasIndex(x => new { x.RestoranId, x.UserId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.UserId)
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.Restoran)
                .WithMany(x => x.Garsonlar)
                .HasForeignKey(x => x.RestoranId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RestoranMasa>(entity =>
        {
            entity.ToTable("RestoranMasalari", restoranSchema);
            entity.Property(x => x.MasaNo).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Durum).HasMaxLength(32).IsRequired();
            entity.HasIndex(x => new { x.RestoranId, x.MasaNo })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [AktifMi] = 1");

            entity.HasOne(x => x.Restoran)
                .WithMany(x => x.Masalar)
                .HasForeignKey(x => x.RestoranId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RestoranMenuKategori>(entity =>
        {
            entity.ToTable("RestoranMenuKategorileri", restoranSchema);
            entity.Property(x => x.Ad).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => new { x.RestoranId, x.Ad })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [AktifMi] = 1");
            entity.HasIndex(x => new { x.RestoranId, x.SiraNo })
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.Restoran)
                .WithMany(x => x.MenuKategorileri)
                .HasForeignKey(x => x.RestoranId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RestoranMenuUrun>(entity =>
        {
            entity.ToTable("RestoranMenuUrunleri", restoranSchema);
            entity.Property(x => x.Ad).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Aciklama).HasMaxLength(512);
            entity.Property(x => x.Fiyat).HasPrecision(18, 2);
            entity.Property(x => x.ParaBirimi).HasMaxLength(3).IsRequired();
            entity.HasIndex(x => new { x.RestoranMenuKategoriId, x.Ad })
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.RestoranMenuKategori)
                .WithMany(x => x.Urunler)
                .HasForeignKey(x => x.RestoranMenuKategoriId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RestoranSiparis>(entity =>
        {
            entity.ToTable("RestoranSiparisleri", restoranSchema);
            entity.Property(x => x.SiparisNo).HasMaxLength(64).IsRequired();
            entity.Property(x => x.SiparisDurumu).HasMaxLength(32).IsRequired();
            entity.Property(x => x.ToplamTutar).HasPrecision(18, 2);
            entity.Property(x => x.OdenenTutar).HasPrecision(18, 2);
            entity.Property(x => x.KalanTutar).HasPrecision(18, 2);
            entity.Property(x => x.ParaBirimi).HasMaxLength(3).IsRequired();
            entity.Property(x => x.OdemeDurumu).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Notlar).HasMaxLength(1024);
            entity.HasIndex(x => x.SiparisNo)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => new { x.RestoranId, x.SiparisTarihi })
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => new { x.RestoranMasaId, x.SiparisDurumu })
                .HasFilter("[IsDeleted] = 0 AND [RestoranMasaId] IS NOT NULL");

            entity.HasOne(x => x.Restoran)
                .WithMany(x => x.Siparisler)
                .HasForeignKey(x => x.RestoranId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RestoranMasa)
                .WithMany(x => x.Siparisler)
                .HasForeignKey(x => x.RestoranMasaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RestoranSiparisKalemi>(entity =>
        {
            entity.ToTable("RestoranSiparisKalemleri", restoranSchema);
            entity.Property(x => x.UrunAdiSnapshot).HasMaxLength(128).IsRequired();
            entity.Property(x => x.BirimFiyat).HasPrecision(18, 2);
            entity.Property(x => x.Miktar).HasPrecision(18, 2);
            entity.Property(x => x.SatirToplam).HasPrecision(18, 2);
            entity.Property(x => x.Durum).HasMaxLength(32).IsRequired().HasDefaultValue(RestoranSiparisKalemDurumlari.Beklemede);
            entity.Property(x => x.Notlar).HasMaxLength(512);
            entity.HasIndex(x => x.RestoranSiparisId)
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.RestoranSiparis)
                .WithMany(x => x.Kalemler)
                .HasForeignKey(x => x.RestoranSiparisId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RestoranMenuUrun)
                .WithMany(x => x.SiparisKalemleri)
                .HasForeignKey(x => x.RestoranMenuUrunId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RestoranOdeme>(entity =>
        {
            entity.ToTable("RestoranOdemeleri", restoranSchema);
            entity.Property(x => x.OdemeTipi).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Tutar).HasPrecision(18, 2);
            entity.Property(x => x.ParaBirimi).HasMaxLength(3).IsRequired();
            entity.Property(x => x.Aciklama).HasMaxLength(512);
            entity.Property(x => x.Durum).HasMaxLength(32).IsRequired();
            entity.Property(x => x.IslemReferansNo).HasMaxLength(64);
            entity.HasIndex(x => new { x.RestoranSiparisId, x.OdemeTarihi })
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => new { x.RestoranSiparisId, x.RezervasyonId, x.OdemeTipi })
                .HasFilter("[IsDeleted] = 0 AND [RezervasyonId] IS NOT NULL");
            entity.HasIndex(x => x.IslemReferansNo)
                .HasFilter("[IsDeleted] = 0 AND [IslemReferansNo] IS NOT NULL");

            entity.HasOne(x => x.RestoranSiparis)
                .WithMany(x => x.Odemeler)
                .HasForeignKey(x => x.RestoranSiparisId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Rezervasyon)
                .WithMany()
                .HasForeignKey(x => x.RezervasyonId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RezervasyonOdeme)
                .WithMany()
                .HasForeignKey(x => x.RezervasyonOdemeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CariKart>(entity =>
        {
            entity.ToTable("CariKartlar", muhasebeSchema);
            entity.Property(x => x.CariTipi).HasMaxLength(32).IsRequired();
            entity.Property(x => x.CariKodu).HasMaxLength(64).IsRequired();
            entity.Property(x => x.UnvanAdSoyad).HasMaxLength(256).IsRequired();
            entity.Property(x => x.VergiNoTckn).HasMaxLength(32);
            entity.Property(x => x.VergiDairesi).HasMaxLength(128);
            entity.Property(x => x.Telefon).HasMaxLength(32);
            entity.Property(x => x.Eposta).HasMaxLength(256);
            entity.Property(x => x.Adres).HasMaxLength(512);
            entity.Property(x => x.Il).HasMaxLength(128);
            entity.Property(x => x.Ilce).HasMaxLength(128);
            entity.Property(x => x.Aciklama).HasMaxLength(1024);
            entity.HasIndex(x => x.CariKodu)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => new { x.CariTipi, x.UnvanAdSoyad })
                .HasFilter("[IsDeleted] = 0");
        });

        modelBuilder.Entity<CariHareket>(entity =>
        {
            entity.ToTable("CariHareketler", muhasebeSchema);
            entity.Property(x => x.BelgeTuru).HasMaxLength(32).IsRequired();
            entity.Property(x => x.BelgeNo).HasMaxLength(64);
            entity.Property(x => x.Aciklama).HasMaxLength(1024);
            entity.Property(x => x.BorcTutari).HasPrecision(18, 2);
            entity.Property(x => x.AlacakTutari).HasPrecision(18, 2);
            entity.Property(x => x.ParaBirimi).HasMaxLength(3).IsRequired();
            entity.Property(x => x.Durum).HasMaxLength(16).IsRequired();
            entity.Property(x => x.KaynakModul).HasMaxLength(64);
            entity.HasIndex(x => new { x.CariKartId, x.HareketTarihi })
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.BelgeNo)
                .HasFilter("[IsDeleted] = 0 AND [BelgeNo] IS NOT NULL");

            entity.HasOne(x => x.CariKart)
                .WithMany(x => x.CariHareketler)
                .HasForeignKey(x => x.CariKartId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<KasaBankaHesap>(entity =>
        {
            entity.ToTable("KasaBankaHesaplari", muhasebeSchema);
            entity.Property(x => x.Tip).HasMaxLength(16).IsRequired();
            entity.Property(x => x.Kod).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Ad).HasMaxLength(200).IsRequired();
            entity.Property(x => x.BankaAdi).HasMaxLength(128);
            entity.Property(x => x.SubeAdi).HasMaxLength(128);
            entity.Property(x => x.HesapNo).HasMaxLength(64);
            entity.Property(x => x.Iban).HasMaxLength(34);
            entity.Property(x => x.MusteriNo).HasMaxLength(64);
            entity.Property(x => x.HesapTuru).HasMaxLength(32);
            entity.Property(x => x.Aciklama).HasMaxLength(1024);
            entity.HasIndex(x => x.Kod)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => new { x.Tip, x.AktifMi })
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.MuhasebeHesapPlaniId)
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.MuhasebeHesapPlani)
                .WithMany()
                .HasForeignKey(x => x.MuhasebeHesapPlaniId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Hesap>(entity =>
        {
            entity.ToTable("Hesaplar", muhasebeSchema);
            entity.Property(x => x.Ad).HasMaxLength(128).IsRequired();
            entity.Property(x => x.MuhasebeFormu).HasMaxLength(64);
            entity.Property(x => x.Aciklama).HasMaxLength(1024);
            entity.HasIndex(x => x.Ad)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.MuhasebeHesapPlaniId)
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.MuhasebeHesapPlani)
                .WithMany()
                .HasForeignKey(x => x.MuhasebeHesapPlaniId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HesapKasaBankaBaglanti>(entity =>
        {
            entity.ToTable("HesapKasaBankaBaglantilari", muhasebeSchema);
            entity.HasIndex(x => new { x.HesapId, x.KasaBankaHesapId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.KasaBankaHesapId)
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.Hesap)
                .WithMany(x => x.KasaBankaBaglantilari)
                .HasForeignKey(x => x.HesapId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.KasaBankaHesap)
                .WithMany()
                .HasForeignKey(x => x.KasaBankaHesapId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HesapDepoBaglanti>(entity =>
        {
            entity.ToTable("HesapDepoBaglantilari", muhasebeSchema);
            entity.HasIndex(x => new { x.HesapId, x.DepoId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.DepoId)
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.Hesap)
                .WithMany(x => x.DepoBaglantilari)
                .HasForeignKey(x => x.HesapId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Depo)
                .WithMany()
                .HasForeignKey(x => x.DepoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<KasaHareket>(entity =>
        {
            entity.ToTable("KasaHareketleri", muhasebeSchema);
            entity.Property(x => x.KasaKodu).HasMaxLength(64).IsRequired();
            entity.Property(x => x.HareketTipi).HasMaxLength(16).IsRequired();
            entity.Property(x => x.Tutar).HasPrecision(18, 2);
            entity.Property(x => x.ParaBirimi).HasMaxLength(3).IsRequired();
            entity.Property(x => x.Aciklama).HasMaxLength(1024);
            entity.Property(x => x.BelgeNo).HasMaxLength(64);
            entity.Property(x => x.KaynakModul).HasMaxLength(64);
            entity.Property(x => x.Durum).HasMaxLength(16).IsRequired();
            entity.HasIndex(x => new { x.KasaKodu, x.HareketTarihi })
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.KasaBankaHesapId)
                .HasFilter("[IsDeleted] = 0 AND [KasaBankaHesapId] IS NOT NULL");
            entity.HasIndex(x => x.BelgeNo)
                .HasFilter("[IsDeleted] = 0 AND [BelgeNo] IS NOT NULL");

            entity.HasOne(x => x.KasaBankaHesap)
                .WithMany(x => x.KasaHareketler)
                .HasForeignKey(x => x.KasaBankaHesapId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CariKart)
                .WithMany(x => x.KasaHareketler)
                .HasForeignKey(x => x.CariKartId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BankaHareket>(entity =>
        {
            entity.ToTable("BankaHareketleri", muhasebeSchema);
            entity.Property(x => x.BankaAdi).HasMaxLength(128).IsRequired();
            entity.Property(x => x.HesapKoduIban).HasMaxLength(64).IsRequired();
            entity.Property(x => x.HareketTipi).HasMaxLength(16).IsRequired();
            entity.Property(x => x.Tutar).HasPrecision(18, 2);
            entity.Property(x => x.ParaBirimi).HasMaxLength(3).IsRequired();
            entity.Property(x => x.Aciklama).HasMaxLength(1024);
            entity.Property(x => x.BelgeNo).HasMaxLength(64);
            entity.Property(x => x.KaynakModul).HasMaxLength(64);
            entity.Property(x => x.Durum).HasMaxLength(16).IsRequired();
            entity.HasIndex(x => new { x.BankaAdi, x.HesapKoduIban, x.HareketTarihi })
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.KasaBankaHesapId)
                .HasFilter("[IsDeleted] = 0 AND [KasaBankaHesapId] IS NOT NULL");
            entity.HasIndex(x => x.BelgeNo)
                .HasFilter("[IsDeleted] = 0 AND [BelgeNo] IS NOT NULL");

            entity.HasOne(x => x.KasaBankaHesap)
                .WithMany(x => x.BankaHareketler)
                .HasForeignKey(x => x.KasaBankaHesapId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CariKart)
                .WithMany(x => x.BankaHareketler)
                .HasForeignKey(x => x.CariKartId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TahsilatOdemeBelgesi>(entity =>
        {
            entity.ToTable("TahsilatOdemeBelgeleri", muhasebeSchema);
            entity.Property(x => x.BelgeNo).HasMaxLength(64).IsRequired();
            entity.Property(x => x.BelgeTipi).HasMaxLength(16).IsRequired();
            entity.Property(x => x.Tutar).HasPrecision(18, 2);
            entity.Property(x => x.ParaBirimi).HasMaxLength(3).IsRequired();
            entity.Property(x => x.OdemeYontemi).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Aciklama).HasMaxLength(1024);
            entity.Property(x => x.KaynakModul).HasMaxLength(64);
            entity.Property(x => x.Durum).HasMaxLength(16).IsRequired();
            entity.HasIndex(x => x.BelgeNo)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => new { x.BelgeTarihi, x.BelgeTipi })
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.CariKartId)
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.CariKart)
                .WithMany(x => x.TahsilatOdemeBelgeleri)
                .HasForeignKey(x => x.CariKartId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TasinirKod>(entity =>
        {
            entity.ToTable("TasinirKodlar", muhasebeSchema);
            entity.Property(x => x.TamKod).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Kod).HasMaxLength(16).IsRequired();
            entity.Property(x => x.Ad).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Aciklama).HasMaxLength(1024);
            entity.HasIndex(x => x.TamKod)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => new { x.UstKodId, x.Kod })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => new { x.DuzeyNo, x.Ad })
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.UstKodId)
                .HasFilter("[IsDeleted] = 0 AND [UstKodId] IS NOT NULL");

            entity.HasOne(x => x.UstKod)
                .WithMany(x => x.AltKodlar)
                .HasForeignKey(x => x.UstKodId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MuhasebeHesapPlani>(entity =>
        {
            entity.ToTable("MuhasebeHesapPlanlari", muhasebeSchema);
            entity.Property(x => x.Kod).HasMaxLength(16).IsRequired();
            entity.Property(x => x.TamKod).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Ad).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Aciklama).HasMaxLength(1024);
            entity.HasIndex(x => x.TamKod)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => new { x.UstHesapId, x.Kod })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.UstHesapId)
                .HasFilter("[IsDeleted] = 0 AND [UstHesapId] IS NOT NULL");

            entity.HasOne(x => x.UstHesap)
                .WithMany(x => x.AltHesaplar)
                .HasForeignKey(x => x.UstHesapId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TasinirKart>(entity =>
        {
            entity.ToTable("TasinirKartlar", muhasebeSchema);
            entity.Property(x => x.StokKodu).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Ad).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Birim).HasMaxLength(32).IsRequired();
            entity.Property(x => x.MalzemeTipi).HasMaxLength(32).IsRequired();
            entity.Property(x => x.KdvOrani).HasPrecision(5, 2);
            entity.Property(x => x.Aciklama).HasMaxLength(1024);
            entity.HasIndex(x => x.StokKodu)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => new { x.TasinirKodId, x.Ad })
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.TasinirKod)
                .WithMany(x => x.TasinirKartlari)
                .HasForeignKey(x => x.TasinirKodId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Depo>(entity =>
        {
            entity.ToTable("Depolar", muhasebeSchema);
            entity.Property(x => x.Kod).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Ad).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Aciklama).HasMaxLength(1024);
            entity.HasIndex(x => x.Kod)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => new { x.TesisId, x.Ad })
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.Tesis)
                .WithMany()
                .HasForeignKey(x => x.TesisId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StokHareket>(entity =>
        {
            entity.ToTable("StokHareketleri", muhasebeSchema);
            entity.Property(x => x.HareketTipi).HasMaxLength(16).IsRequired();
            entity.Property(x => x.Miktar).HasPrecision(18, 3);
            entity.Property(x => x.BirimFiyat).HasPrecision(18, 2);
            entity.Property(x => x.Tutar).HasPrecision(18, 2);
            entity.Property(x => x.BelgeNo).HasMaxLength(64);
            entity.Property(x => x.Aciklama).HasMaxLength(1024);
            entity.Property(x => x.KaynakModul).HasMaxLength(64);
            entity.Property(x => x.Durum).HasMaxLength(16).IsRequired();
            entity.HasIndex(x => new { x.DepoId, x.TasinirKartId, x.HareketTarihi })
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.BelgeNo)
                .HasFilter("[IsDeleted] = 0 AND [BelgeNo] IS NOT NULL");
            entity.HasIndex(x => x.CariKartId)
                .HasFilter("[IsDeleted] = 0 AND [CariKartId] IS NOT NULL");

            entity.HasOne(x => x.Depo)
                .WithMany(x => x.StokHareketleri)
                .HasForeignKey(x => x.DepoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.TasinirKart)
                .WithMany(x => x.StokHareketleri)
                .HasForeignKey(x => x.TasinirKartId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CariKart)
                .WithMany()
                .HasForeignKey(x => x.CariKartId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Bildirim>(entity =>
        {
            entity.ToTable("Bildirimler", "dbo");
            entity.Property(x => x.Tip).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Baslik).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Mesaj).HasMaxLength(1024).IsRequired();
            entity.Property(x => x.Link).HasMaxLength(256);
            entity.Property(x => x.KaynakUserAdi).HasMaxLength(128);
            entity.Property(x => x.Severity).HasMaxLength(16).IsRequired();
            entity.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt })
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => new { x.UserId, x.CreatedAt })
                .HasFilter("[IsDeleted] = 0");
        });

        modelBuilder.Entity<BildirimTercih>(entity =>
        {
            entity.ToTable("BildirimTercihleri", "dbo");
            entity.Property(x => x.MinimumSeverity).HasMaxLength(16).IsRequired();
            entity.Property(x => x.IzinliTiplerJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.IzinliKaynaklarJson).HasColumnType("nvarchar(max)");
            entity.HasIndex(x => x.UserId)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
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
                    SetProperty(entity, nameof(BaseEntity<int>.UpdatedAt), now);
                    SetProperty(entity, nameof(BaseEntity<int>.UpdatedBy), user);
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
