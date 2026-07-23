using System.ComponentModel.DataAnnotations;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.PosTahsilatValorleri.Entities;

/// <summary>
/// Kredi karti/POS tahsilatlari icin kalici valor takip kaydi. Her aktif kredi karti tahsilati
/// icin en fazla bir kayit olusur (bkz. unique index TahsilatOdemeBelgesiId). Tum snapshot
/// alanlari odeme aninda donar; KasaBankaHesap tanimi sonradan degisse bile bu kayit etkilenmez.
/// </summary>
public class PosTahsilatValor : BaseEntity<int>
{
    public int TesisId { get; set; }

    public int TahsilatOdemeBelgesiId { get; set; }

    public int KrediKartiHesapId { get; set; }

    /// <summary>Snapshot - odeme anindaki KasaBankaHesap.BagliBankaHesapId.</summary>
    public int? BagliBankaHesapId { get; set; }

    /// <summary>Snapshot - odeme anindaki KasaBankaHesap.KomisyonGiderHesapPlaniId. Aktarimda
    /// hesap tanimindaki guncel deger degil, bu snapshot deger kullanilir.</summary>
    public int? KomisyonGiderHesapPlaniId { get; set; }

    public DateTime OdemeTarihi { get; set; }

    public int ValorGunSayisi { get; set; }

    [MaxLength(16)]
    public string ValorGunTuru { get; set; } = string.Empty;

    public DateOnly BeklenenValorTarihi { get; set; }

    public bool OtomatikAktarimMi { get; set; }

    /// <summary>Snapshot - odeme anindaki KasaBankaHesap.KomisyonOrani. Null ise komisyon
    /// belirsizdi (bkz. Durum=MutabakatBekliyor).</summary>
    public decimal? KomisyonOraniSnapshot { get; set; }

    public decimal BrutTutar { get; set; }

    public decimal KomisyonTutari { get; set; }

    public decimal NetTutar { get; set; }

    [Required]
    [MaxLength(3)]
    public string ParaBirimi { get; set; } = "TRY";

    [Required]
    [MaxLength(24)]
    public string Durum { get; set; } = PosTahsilatValorDurumlari.ValorBekliyor;

    /// <summary>Claim/lease mekanizmasi - Aktariliyor/TersKayitOlusturuluyor'a gecis aninda
    /// doldurulur, islem bitince temizlenir.</summary>
    public DateTime? AktarimBaslamaTarihi { get; set; }

    /// <summary>EF concurrency token olarak isaretlenir (bkz. StysAppDbContext.OnModelCreating) -
    /// claim sahipligini garanti eder, eski/gecersiz worker'in sonucu ezmesini engeller.</summary>
    public Guid? ClaimToken { get; set; }

    public int DenemeSayisi { get; set; }

    public DateTime? SonDenemeTarihi { get; set; }

    public DateTime? AktarimTarihi { get; set; }

    public int? MuhasebeFisId { get; set; }

    /// <summary>"duzeltme-ters-kayit" sonrasi olusan ters kayit fisinin id'si. Orijinal
    /// MuhasebeFisId degistirilmez, tarihce korunur.</summary>
    public int? TersKayitMuhasebeFisId { get; set; }

    [MaxLength(1024)]
    public string? HataMesaji { get; set; }

    [MaxLength(1024)]
    public string? Aciklama { get; set; }

    public TahsilatOdemeBelgesi? TahsilatOdemeBelgesi { get; set; }
    public KasaBankaHesap? KrediKartiHesap { get; set; }
    public KasaBankaHesap? BagliBankaHesap { get; set; }
    public MuhasebeHesapPlani? KomisyonGiderHesapPlani { get; set; }
    public MuhasebeFis? MuhasebeFis { get; set; }
}
