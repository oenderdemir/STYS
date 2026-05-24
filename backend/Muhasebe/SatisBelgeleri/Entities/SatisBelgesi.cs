using STYS.Muhasebe.MuhasebeFisleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.SatisBelgeleri.Entities;

public class SatisBelgesi : BaseEntity<int>
{
    public string BelgeNo { get; set; } = string.Empty;

    public SatisBelgesiTipi BelgeTipi { get; set; } = SatisBelgesiTipi.FaturaTaslagi;
    public SatisBelgesiDurumu Durum { get; set; } = SatisBelgesiDurumu.Taslak;

    public SatisKaynakModulu KaynakModul { get; set; } = SatisKaynakModulu.Manuel;
    public string? KaynakTipi { get; set; }
    public string? KaynakId { get; set; }

    public int? TesisId { get; set; }

    public DateTime BelgeTarihi { get; set; }
    public DateTime? VadeTarihi { get; set; }

    public string? MusteriUnvan { get; set; }
    public string? MusteriAdSoyad { get; set; }
    public string? MusteriVergiNo { get; set; }
    public string? MusteriTcKimlikNo { get; set; }
    public string? MusteriVergiDairesi { get; set; }
    public string? MusteriAdres { get; set; }
    public string? MusteriEposta { get; set; }
    public string? MusteriTelefon { get; set; }

    public bool KurumsalMi { get; set; }

    public decimal ToplamMatrah { get; set; }
    public decimal ToplamKdv { get; set; }
    public decimal GenelToplam { get; set; }

    public string? Aciklama { get; set; }
    public string? RedNedeni { get; set; }

    public string? ResmiFaturaNo { get; set; }
    public string? EBelgeUuid { get; set; }

    public DateTime? MuhasebeOnayinaGonderilmeTarihi { get; set; }
    public DateTime? MuhasebeOnayTarihi { get; set; }
    public DateTime? FaturaKesimTarihi { get; set; }
    public DateTime? MusteriyeGonderimTarihi { get; set; }

    public int? MuhasebeFisId { get; set; }
    public MuhasebeFis? MuhasebeFis { get; set; }
    public DateTime? MuhasebeFisOlusturmaTarihi { get; set; }

    public ICollection<SatisBelgesiSatiri> Satirlar { get; set; } = new List<SatisBelgesiSatiri>();
}
