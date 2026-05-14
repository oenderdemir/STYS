using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.TasinirKodlari.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Entities;

/// <summary>
/// Taşınır kod (TasinirKod) ile Muhasebe Hesap Planı girişi arasındaki eşlemeyi tutar.
/// Bir taşınır kod birden fazla muhasebe hesabına bağlanabilir (örn: alış, satış, iade).
/// </summary>
public class TasinirKodMuhasebeHesapEsleme : BaseEntity<int>
{
    /// <summary>
    /// Eşlemenin ait olduğu taşınır kod.
    /// </summary>
    public int TasinirKodId { get; set; }

    /// <summary>
    /// Eşlemenin bağlandığı muhasebe hesap planı girişi.
    /// </summary>
    public int MuhasebeHesapPlaniId { get; set; }

    /// <summary>
    /// Bu eşlemenin hangi işlem türü için kullanılacağını belirtir:
    /// "Alis", "Satis", "Iade", "Transfer", "Dusum" vb.
    /// (Geriye dönük uyumluluk için korunur; yeni akışta HareketTipi önceliklidir.)
    /// </summary>
    public string IslemTuru { get; set; } = string.Empty;

    /// <summary>
    /// Bu eşlemenin hangi malzeme tipi için geçerli olduğunu belirtir:
    /// "Sarf", "Demirbas", "Hammadde", "YariMamul", "Mamul", "TicariMal" vb.
    /// </summary>
    public string MalzemeTipi { get; set; } = string.Empty;

    /// <summary>
    /// Bu eşlemenin hangi hareket tipi için geçerli olduğunu belirtir:
    /// "Giris", "Cikis", "Transfer", "Iade", "Dusum" vb.
    /// </summary>
    public string HareketTipi { get; set; } = string.Empty;

    /// <summary>
    /// Bu eşleme aktif olarak kullanılıyor mu?
    /// </summary>
    public bool AktifMi { get; set; } = true;

    /// <summary>
    /// Varsayılan eşleme mi? İşlem türü başına bir varsayılan olabilir.
    /// </summary>
    public bool VarsayilanMi { get; set; }

    // Navigation properties
    public TasinirKod? TasinirKod { get; set; }
    public MuhasebeHesapPlani? MuhasebeHesapPlani { get; set; }
}