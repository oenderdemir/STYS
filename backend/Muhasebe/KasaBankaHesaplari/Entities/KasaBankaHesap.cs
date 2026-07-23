using System.ComponentModel.DataAnnotations;
using STYS.Muhasebe.BankaHareketleri.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.KasaHareketleri.Entities;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.KasaBankaHesaplari.Entities;

public class KasaBankaHesap : BaseEntity<int>
{
    public int? TesisId { get; set; }

    [Required]
    [MaxLength(16)]
    public string Tip { get; set; } = KasaBankaHesapTipleri.NakitKasa;

    [Required]
    [MaxLength(64)]
    public string Kod { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Ad { get; set; } = string.Empty;

    public int? MuhasebeHesapPlaniId { get; set; }

    [MaxLength(50)]
    public string? AnaMuhasebeHesapKodu { get; set; }

    public int? MuhasebeHesapSiraNo { get; set; }

    [MaxLength(3)]
    public string? ParaBirimi { get; set; }

    public int ValorGunSayisi { get; set; }

    [MaxLength(128)]
    public string? KartAdi { get; set; }

    [MaxLength(32)]
    public string? KartNoMaskeli { get; set; }

    public decimal? KartLimiti { get; set; }

    public int? HesapKesimGunu { get; set; }

    public int? SonOdemeGunu { get; set; }

    public int? BagliBankaHesapId { get; set; }

    /// <summary>Valor gununde POS alacaginin BagliBankaHesapId'ye otomatik aktarilip
    /// aktarilmayacagi. Yalnizca STYS ici muhasebe aktarimidir, bankaya fiziksel para
    /// transferi yapmaz.</summary>
    public bool ValorGunundeOtomatikHesabaAktarMi { get; set; }

    [MaxLength(16)]
    public string ValorGunTuru { get; set; } = ValorGunTurleri.TakvimGunu;

    /// <summary>Komisyon kesintisinin yazilacagi gider hesabi. KomisyonOrani>0 ise zorunludur.</summary>
    public int? KomisyonGiderHesapPlaniId { get; set; }

    /// <summary>Sabit oranli POS komisyon tarifesi (yuzde). Null ise komisyon belirsiz kabul
    /// edilir; otomatik aktarimda bu durumda MutabakatBekliyor'a dusulur.</summary>
    public decimal? KomisyonOrani { get; set; }

    [MaxLength(128)]
    public string? BankaAdi { get; set; }

    [MaxLength(128)]
    public string? SubeAdi { get; set; }

    [MaxLength(64)]
    public string? HesapNo { get; set; }

    [MaxLength(34)]
    public string? Iban { get; set; }

    [MaxLength(64)]
    public string? MusteriNo { get; set; }

    [MaxLength(32)]
    public string? HesapTuru { get; set; }

    [MaxLength(128)]
    public string? SorumluKisi { get; set; }

    [MaxLength(128)]
    public string? Lokasyon { get; set; }

    public bool AktifMi { get; set; } = true;

    [MaxLength(1024)]
    public string? Aciklama { get; set; }

    public MuhasebeHesapPlani? MuhasebeHesapPlani { get; set; }
    public KasaBankaHesap? BagliBankaHesap { get; set; }
    public Tesis? Tesis { get; set; }
    public ICollection<KasaHareket> KasaHareketler { get; set; } = [];
    public ICollection<BankaHareket> BankaHareketler { get; set; } = [];
    public ICollection<KasaBankaHesap> BagliKartlar { get; set; } = [];
}
