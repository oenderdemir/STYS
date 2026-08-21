using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.TasinirKartlari.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.StokLotlari.Entities;

public class StokLot : BaseEntity<int>
{
    public int TesisId { get; set; }
    public int TasinirKartId { get; set; }
    public string LotNo { get; set; } = string.Empty;
    public DateTime? SonKullanmaTarihi { get; set; }
    public string? Aciklama { get; set; }
    public bool AktifMi { get; set; } = true;

    public Tesis? Tesis { get; set; }
    public TasinirKart? TasinirKart { get; set; }
    public ICollection<StokHareket> StokHareketleri { get; set; } = [];
}
