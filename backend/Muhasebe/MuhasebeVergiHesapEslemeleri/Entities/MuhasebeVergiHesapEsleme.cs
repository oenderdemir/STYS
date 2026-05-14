using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.MuhasebeVergiHesapEslemeleri.Entities;

/// <summary>
/// Vergi tipi ve oranına göre alış/satış KDV hesaplarını eşleştirir.
/// Tesis bazlı veya genel (TesisId=null) tanım yapılabilir.
/// </summary>
public class MuhasebeVergiHesapEsleme : BaseEntity<int>
{
    /// <summary>
    /// Tesis bazlı tanım için tesis id. null ise genel tanımdır.
    /// </summary>
    public int? TesisId { get; set; }

    /// <summary>
    /// Vergi tipi: "KDV" gibi.
    /// </summary>
    public string VergiTipi { get; set; } = string.Empty;

    /// <summary>
    /// Vergi oranı (0-100 arası).
    /// </summary>
    public decimal Oran { get; set; }

    /// <summary>
    /// Alış KDV hesabı (örn: 191 İndirilecek KDV).
    /// </summary>
    public int AlisKdvHesapId { get; set; }

    /// <summary>
    /// Satış KDV hesabı (örn: 391 Hesaplanan KDV).
    /// </summary>
    public int SatisKdvHesapId { get; set; }

    /// <summary>
    /// Kayıt aktif mi?
    /// </summary>
    public bool AktifMi { get; set; } = true;

    // Navigation properties
    public MuhasebeHesapPlani? AlisKdvHesap { get; set; }
    public MuhasebeHesapPlani? SatisKdvHesap { get; set; }
}
