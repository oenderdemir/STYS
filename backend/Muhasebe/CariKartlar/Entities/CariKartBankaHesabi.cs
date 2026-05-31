using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.CariKartlar.Entities;

public class CariKartBankaHesabi : BaseEntity<int>
{
    public int CariKartId { get; set; }

    [MaxLength(128)]
    public string? BankaAdi { get; set; }

    [MaxLength(128)]
    public string? SubeAdi { get; set; }

    [MaxLength(64)]
    public string? HesapNo { get; set; }

    [MaxLength(34)]
    public string? Iban { get; set; }

    [MaxLength(512)]
    public string? Aciklama { get; set; }

    public CariKart? CariKart { get; set; }
}
