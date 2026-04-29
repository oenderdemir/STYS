using System.ComponentModel.DataAnnotations;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.Depolar.Entities;

public class Depo : BaseEntity<int>
{
    public int? TesisId { get; set; }
    public int? UstDepoId { get; set; }
    public int? MuhasebeHesapPlaniId { get; set; }
    public string? AnaMuhasebeHesapKodu { get; set; }
    public int? MuhasebeHesapSiraNo { get; set; }

    [Required]
    [MaxLength(64)]
    public string Kod { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Ad { get; set; } = string.Empty;

    [Required]
    public DepoMalzemeKayitTipleri MalzemeKayitTipi { get; set; } = DepoMalzemeKayitTipleri.MalzemeleriAyriKayittaTut;

    public bool SatisFiyatlariniGoster { get; set; }

    public bool AvansGenel { get; set; }

    public bool AktifMi { get; set; } = true;

    [MaxLength(1024)]
    public string? Aciklama { get; set; }

    public Tesis? Tesis { get; set; }
    public Depo? UstDepo { get; set; }
    public MuhasebeHesapPlani? MuhasebeHesapPlani { get; set; }
    public ICollection<Depo> AltDepolar { get; set; } = [];
    public ICollection<DepoCikisGrup> DepoCikisGruplari { get; set; } = [];
    public ICollection<StokHareket> StokHareketleri { get; set; } = [];
}
