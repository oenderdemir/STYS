namespace STYS.Rezervasyonlar.Dto;

/// <summary>
/// Rezervasyon check-out sonrası satış belgesi taslağı oluşturma request modeli.
/// Controller route'undaki {rezervasyonId} ile RezervasyonId eşleşmelidir.
/// </summary>
public class RezervasyonSatisBelgesiTaslakRequest
{
    /// <summary>
    /// Satış belgesi oluşturulacak rezervasyonun Id'si.
    /// Route değeriyle aynı olmalıdır, değilse 400 hatası döner.
    /// </summary>
    public int RezervasyonId { get; set; }

    // ── Müşteri / Fatura bilgileri ──

    /// <summary>Kurumsal fatura mı? false → bireysel.</summary>
    public bool KurumsalMi { get; set; }

    /// <summary>Kurumsal müşteri ünvanı. KurumsalMi=true ise zorunludur.</summary>
    public string? MusteriUnvan { get; set; }

    /// <summary>Bireysel müşteri ad soyad. KurumsalMi=false ise zorunludur; boşsa rezervasyondan alınır.</summary>
    public string? MusteriAdSoyad { get; set; }

    /// <summary>Kurumsal müşteri vergi numarası. KurumsalMi=true ise zorunludur.</summary>
    public string? MusteriVergiNo { get; set; }

    /// <summary>Bireysel müşteri TC kimlik numarası.</summary>
    public string? MusteriTcKimlikNo { get; set; }

    /// <summary>Kurumsal müşteri vergi dairesi.</summary>
    public string? MusteriVergiDairesi { get; set; }

    /// <summary>Müşteri adres bilgisi.</summary>
    public string? MusteriAdres { get; set; }

    /// <summary>Müşteri e‑posta. Boşsa rezervasyondan alınır.</summary>
    public string? MusteriEposta { get; set; }

    /// <summary>Müşteri telefon. Boşsa rezervasyondan alınır.</summary>
    public string? MusteriTelefon { get; set; }

    // ── Belge bilgileri ──

    /// <summary>Belge tarihi. Boşsa rezervasyonun çıkış tarihi kullanılır.</summary>
    public DateTime? BelgeTarihi { get; set; }

    /// <summary>Vade tarihi (isteğe bağlı).</summary>
    public DateTime? VadeTarihi { get; set; }

    /// <summary>Belge açıklaması. Boşsa otomatik oluşturulur.</summary>
    public string? Aciklama { get; set; }

    // ── KDV override ──

    /// <summary>KDV oranı (varsayılan %10). KdvIstisnaTanimId verilirse göz ardı edilir.</summary>
    public decimal? KdvOrani { get; set; }

    /// <summary>KDV istisna tanım Id'si. Verilirse satırlar bu istisna ile oluşturulur.</summary>
    public int? KdvIstisnaTanimId { get; set; }

    /// <summary>Cari kart otomatik çözümlenemezse (422) kullanıcının elle seçtiği cari kart.
    /// Boşsa IRezervasyonCariKartResolver'ın standart kademesi uygulanır.</summary>
    public int? CariKartIdOverride { get; set; }
}
