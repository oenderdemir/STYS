using System.ComponentModel.DataAnnotations;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Rezervasyonlar.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Entegrasyonlar.Pos.Entities;

public class PosOdemeIslemi : BaseEntity<int>, ITenantEntity
{
    public int KurumId { get; set; }
    public int TesisId { get; set; }
    public int RezervasyonId { get; set; }
    public int PosTerminalId { get; set; }
    public int KasaBankaHesapId { get; set; }
    public int? CariKartId { get; set; }
    public int? RezervasyonOdemeId { get; set; }

    [Required, MaxLength(96)]
    public string IslemReferansi { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? SaglayiciIslemId { get; set; }

    public decimal Tutar { get; set; }

    [Required, MaxLength(3)]
    public string ParaBirimi { get; set; } = "TRY";

    [Required, MaxLength(32)]
    public string Durum { get; set; } = PosOdemeDurumlari.Olusturuldu;

    [MaxLength(512)]
    public string? Aciklama { get; set; }

    [MaxLength(64)]
    public string? RetrievalReferenceNo { get; set; }

    [MaxLength(64)]
    public string? AcquirerReference { get; set; }

    [MaxLength(64)]
    public string? AuthorizationCode { get; set; }

    [MaxLength(1024)]
    public string? HataMesaji { get; set; }

    public string? SonSaglayiciYaniti { get; set; }
    public DateTime? SonSorgulamaTarihi { get; set; }
    public DateTime? TamamlanmaTarihi { get; set; }
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();

    public PosTerminal? PosTerminal { get; set; }
    public KasaBankaHesap? KasaBankaHesap { get; set; }
    public Rezervasyon? Rezervasyon { get; set; }
    public RezervasyonOdeme? RezervasyonOdeme { get; set; }
}

public static class PosOdemeDurumlari
{
    public const string Olusturuldu = "Olusturuldu";
    public const string PosIslemiBekleniyor = "PosIslemiBekleniyor";
    public const string Basarili = "Basarili";
    public const string Basarisiz = "Basarisiz";
    public const string Muhasebelestirildi = "Muhasebelestirildi";
    public const string MutabakatGerekli = "MutabakatGerekli";
}
