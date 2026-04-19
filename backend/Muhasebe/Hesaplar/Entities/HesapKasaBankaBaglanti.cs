using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.Hesaplar.Entities;

public class HesapKasaBankaBaglanti : BaseEntity<int>
{
    public int HesapId { get; set; }
    public int KasaBankaHesapId { get; set; }

    public Hesap? Hesap { get; set; }
    public KasaBankaHesap? KasaBankaHesap { get; set; }
}
