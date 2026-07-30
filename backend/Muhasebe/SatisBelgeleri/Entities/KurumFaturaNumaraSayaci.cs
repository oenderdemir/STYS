using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.SatisBelgeleri.Entities;

/// <summary>
/// Kurum + mali yıl + seri bazlı, eşzamanlılığa güvenli resmî fatura numarası sayacı.
/// MuhasebeYevmiyeNoSayac / PosValorFisNoSayac ile aynı desen (WITH (UPDLOCK, ROWLOCK, HOLDLOCK)
/// satır kilidi + tek transaction) - Max(ResmiFaturaNo)+1 yarışı KULLANILMAZ.
///
/// Sayaç kaydı bu iş kapsamında OTOMATİK oluşturulmaz (YevmiyeNoSayac'ın aksine) - kurum/yıl/seri
/// başına aktif bir sayaç kaydının önceden var olması FaturaKesAsync'in ön koşuludur (bkz.
/// SatisBelgesiService.FaturaKesAsync); "seri bulunamadı" hatası, sayaç kaydı hiç yoksa döner.
/// </summary>
public class KurumFaturaNumaraSayaci : BaseEntity<int>, ITenantEntity
{
    public int KurumId { get; set; }

    public int MaliYil { get; set; }

    /// <summary>3 alfanümerik karakter (A-Z, 0-9), trimlenmiş ve büyük harf — bkz. SatisBelgesiService.NormalizeSeriKodu.</summary>
    public string SeriKodu { get; set; } = string.Empty;

    public int SonNumara { get; set; }

    public bool AktifMi { get; set; } = true;
}
