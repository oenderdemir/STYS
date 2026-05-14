using System.ComponentModel.DataAnnotations;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;

public class MuhasebeHesapPlani : BaseEntity<int>
{
    [Required]
    [MaxLength(64)]
    public string Kod { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string TamKod { get; set; } = string.Empty;

    /// <summary>
    /// Resmi/Tekdüzen hesap planındaki orijinal kod (opsiyonel).
    /// Muhasebe raporlarında kullanılır.
    /// </summary>
    [MaxLength(32)]
    public string? ResmiKod { get; set; }

    /// <summary>
    /// Hızlı bakış / uygulama içi kısa referans kodu (opsiyonel).
    /// </summary>
    [MaxLength(16)]
    public string? UygulamaKodu { get; set; }

    [Required]
    [MaxLength(256)]
    public string Ad { get; set; } = string.Empty;

    public int SeviyeNo { get; set; }

    /// <summary>
    /// AnaHesap, AltHesap veya DetayHesap.
    /// </summary>
    public HesapTipi HesapTipi { get; set; } = HesapTipi.AnaHesap;

    /// <summary>
    /// Ana hesap grubu kodu (örn: "100", "150", "153", "255", "740").
    /// Taşınır kod ↔ muhasebe hesap eşlemesi ve raporlama için kullanılır.
    /// </summary>
    [MaxLength(16)]
    public string? AnaHesapKodu { get; set; }

    public int? TesisId { get; set; }

    public int? UstHesapId { get; set; }

    public bool AktifMi { get; set; } = true;

    /// <summary>
    /// Bu hesap manuel olarak detay hesap olarak işaretlenmişse true.
    /// Otomatik oluşturulan tesis detay hesaplarında da true verilir.
    /// </summary>
    public bool DetayHesapMi { get; set; }

    /// <summary>
    /// Detay hesap üzerinden hareket görülebilir mi? (fiş girişinde seçilebilir).
    /// False ise yalnızca alt hesapları üzerinden işlem yapılabilir.
    /// </summary>
    public bool HareketGorebilirMi { get; set; } = true;

    [MaxLength(1024)]
    public string? Aciklama { get; set; }

    public Tesis? Tesis { get; set; }
    public MuhasebeHesapPlani? UstHesap { get; set; }
    public ICollection<MuhasebeHesapPlani> AltHesaplar { get; set; } = [];
}
