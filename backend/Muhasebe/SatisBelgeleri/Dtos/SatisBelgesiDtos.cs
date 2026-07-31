using STYS.Muhasebe.SatisBelgeleri.Enums;
using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Muhasebe.SatisBelgeleri.Dtos;

public class SatisBelgesiDto : BaseRdbmsDto<int>
{
    /// <summary>Salt okunur — CreateAsync sırasında TesisId üzerinden atanır, istemciden alınmaz/değiştirilemez.</summary>
    public int KurumId { get; set; }
    public string BelgeNo { get; set; } = string.Empty;
    public SatisBelgesiTipi BelgeTipi { get; set; }
    public SatisBelgesiDurumu Durum { get; set; }
    /// <summary>AYRIŞTIRILMIŞ, HENÜZ OTORİTER OLMAYAN projeksiyon alanı — bkz. SatisBelgesiDurumProjection.</summary>
    public TicariBelgeDurumu? TicariDurum { get; set; }
    /// <summary>AYRIŞTIRILMIŞ, HENÜZ OTORİTER OLMAYAN projeksiyon alanı — bkz. SatisBelgesiDurumProjection.</summary>
    public TicariBelgeMuhasebeDurumu? MuhasebeDurumu { get; set; }
    /// <summary>AYRIŞTIRILMIŞ, HENÜZ OTORİTER OLMAYAN projeksiyon alanı — bkz. SatisBelgesiDurumProjection.</summary>
    public TicariBelgeFaturalamaDurumu? FaturalamaDurumu { get; set; }
    public SatisKaynakModulu KaynakModul { get; set; }
    public string? KaynakTipi { get; set; }
    public string? KaynakId { get; set; }
    public int? TesisId { get; set; }
    public int? CariKartId { get; set; }
    public string? CariKartKodu { get; set; }
    public string? CariKartUnvanAdSoyad { get; set; }
    public string? CariKartTipi { get; set; }
    public string? CariKartVergiNoTckn { get; set; }
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
    public decimal ToplamTevkifatTutari { get; set; }
    public decimal ToplamNetKdv { get; set; }
    public decimal GenelToplam { get; set; }
    public string? Aciklama { get; set; }
    public string? RedNedeni { get; set; }
    public string? ResmiFaturaNo { get; set; }
    public string? EBelgeUuid { get; set; }
    public string? KarsiTarafFaturaNo { get; set; }
    public int? IadeEdilenBelgeId { get; set; }
    /// <summary>Salt okunur — iade edilen asıl belgenin BelgeNo'su (varsa).</summary>
    public string? IadeEdilenBelgeNo { get; set; }
    /// <summary>Salt okunur — asıl belge SatisFaturasi ise ResmiFaturaNo, AlisFaturasi ise KarsiTarafFaturaNo.</summary>
    public string? IadeEdilenFaturaNo { get; set; }
    public DateTime? IadeEdilenBelgeTarihi { get; set; }
    public SatisBelgesiTipi? IadeEdilenBelgeTipi { get; set; }
    public DateTime? MuhasebeOnayinaGonderilmeTarihi { get; set; }
    public DateTime? MuhasebeOnayTarihi { get; set; }
    public DateTime? FaturaKesimTarihi { get; set; }
    public DateTime? MusteriyeGonderimTarihi { get; set; }
    public int? MuhasebeFisId { get; set; }
    public DateTime? MuhasebeFisOlusturmaTarihi { get; set; }
    public List<SatisBelgesiSatiriDto> Satirlar { get; set; } = [];
}

public class SatisBelgesiSatiriDto : BaseRdbmsDto<int>
{
    public int SatisBelgesiId { get; set; }
    public int SiraNo { get; set; }
    public SatisBelgesiSatirTipi SatirTipi { get; set; }
    public string Aciklama { get; set; } = string.Empty;
    public int? TasinirKartId { get; set; }
    public int? DepoId { get; set; }
    public string Birim { get; set; } = "Adet";
    public decimal Miktar { get; set; }
    public decimal BirimFiyat { get; set; }
    public decimal IndirimOrani { get; set; }
    public decimal IndirimTutari { get; set; }
    public decimal Matrah { get; set; }
    public int KdvUygulamaTipi { get; set; }
    public int? KdvIstisnaTanimId { get; set; }
    public string? KdvIstisnaKodu { get; set; }
    public string? KdvIstisnaAciklamasi { get; set; }
    public decimal KdvOrani { get; set; }
    public decimal KdvTutari { get; set; }
    public int? TevkifatPay { get; set; }
    public int? TevkifatPayda { get; set; }
    public decimal TevkifatTutari { get; set; }
    public decimal OtvOrani { get; set; }
    public decimal OtvTutari { get; set; }
    public decimal OivOrani { get; set; }
    public decimal OivTutari { get; set; }
    public decimal KonaklamaVergisiOrani { get; set; }
    public decimal KonaklamaVergisiTutari { get; set; }
    public decimal NetKdv { get; set; }
    public decimal SatirToplami { get; set; }
    public string? KaynakSatirId { get; set; }
}

public class CreateSatisBelgesiRequest
{
    public SatisBelgesiTipi BelgeTipi { get; set; } = SatisBelgesiTipi.FaturaTaslagi;
    public SatisKaynakModulu KaynakModul { get; set; } = SatisKaynakModulu.Manuel;
    public string? KaynakTipi { get; set; }
    public string? KaynakId { get; set; }
    public int? TesisId { get; set; }
    public int? CariKartId { get; set; }
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
    public string? Aciklama { get; set; }
    public string? BelgeNo { get; set; }
    /// <summary>Yalnızca AlisFaturasi/SatisIadeFaturasi için kullanılabilir. Bkz. SatisBelgesi.KarsiTarafFaturaNo.</summary>
    public string? KarsiTarafFaturaNo { get; set; }
    /// <summary>Yalnızca SatisIadeFaturasi/AlisIadeFaturasi için kullanılabilir. Bkz. SatisBelgesi.IadeEdilenBelgeId.</summary>
    public int? IadeEdilenBelgeId { get; set; }
    public List<CreateSatisBelgesiSatiriRequest> Satirlar { get; set; } = [];
}

public class CreateSatisBelgesiSatiriRequest
{
    public int SiraNo { get; set; }
    public SatisBelgesiSatirTipi SatirTipi { get; set; } = SatisBelgesiSatirTipi.Diger;
    public string Aciklama { get; set; } = string.Empty;
    public int? TasinirKartId { get; set; }
    public int? DepoId { get; set; }
    public string Birim { get; set; } = "Adet";
    public decimal Miktar { get; set; }
    public decimal BirimFiyat { get; set; }
    public decimal IndirimOrani { get; set; }
    public decimal IndirimTutari { get; set; }
    public int KdvUygulamaTipi { get; set; } = 1; // Kdvli
    public int? KdvIstisnaTanimId { get; set; }
    public decimal KdvOrani { get; set; }
    public int? TevkifatPay { get; set; }
    public int? TevkifatPayda { get; set; }
    public decimal OtvOrani { get; set; }
    public decimal OtvTutari { get; set; }
    public decimal OivOrani { get; set; }
    public decimal OivTutari { get; set; }
    public decimal KonaklamaVergisiOrani { get; set; }
    public decimal KonaklamaVergisiTutari { get; set; }
    public string? KaynakSatirId { get; set; }
}

public class UpdateSatisBelgesiRequest
{
    public string? BelgeNo { get; set; }
    public SatisBelgesiTipi? BelgeTipi { get; set; }
    public int? TesisId { get; set; }
    public int? CariKartId { get; set; }
    public DateTime? BelgeTarihi { get; set; }
    public DateTime? VadeTarihi { get; set; }
    public string? MusteriUnvan { get; set; }
    public string? MusteriAdSoyad { get; set; }
    public string? MusteriVergiNo { get; set; }
    public string? MusteriTcKimlikNo { get; set; }
    public string? MusteriVergiDairesi { get; set; }
    public string? MusteriAdres { get; set; }
    public string? MusteriEposta { get; set; }
    public string? MusteriTelefon { get; set; }
    public bool? KurumsalMi { get; set; }
    public string? Aciklama { get; set; }
    /// <summary>
    /// Null ise mevcut değer değişmez. Trim sonrası boş/whitespace ise değer AÇIKÇA temizlenir
    /// (null yapılır). Dolu bir değer, güncel (veya bu istekle değişen) BelgeTipi'ye uygun
    /// olmalıdır - aksi halde güncelleme reddedilir.
    /// </summary>
    public string? KarsiTarafFaturaNo { get; set; }
    /// <summary>Verilirse ilişki güncellenir. Kaldırmak için IadeEdilenBelgeReferansiKaldir kullanılmalıdır.</summary>
    public int? IadeEdilenBelgeId { get; set; }
    /// <summary>true ise mevcut IadeEdilenBelgeId referansı kaldırılır. IadeEdilenBelgeId ile birlikte gönderilemez.</summary>
    public bool IadeEdilenBelgeReferansiKaldir { get; set; }
    public List<CreateSatisBelgesiSatiriRequest>? Satirlar { get; set; }
}

public class UpdateSatisBelgesiSatiriRequest
{
    public int? Id { get; set; }
    public int SiraNo { get; set; }
    public SatisBelgesiSatirTipi SatirTipi { get; set; } = SatisBelgesiSatirTipi.Diger;
    public string Aciklama { get; set; } = string.Empty;
    public int? TasinirKartId { get; set; }
    public int? DepoId { get; set; }
    public string Birim { get; set; } = "Adet";
    public decimal Miktar { get; set; }
    public decimal BirimFiyat { get; set; }
    public decimal IndirimOrani { get; set; }
    public decimal IndirimTutari { get; set; }
    public int KdvUygulamaTipi { get; set; } = 1;
    public int? KdvIstisnaTanimId { get; set; }
    public decimal KdvOrani { get; set; }
    public int? TevkifatPay { get; set; }
    public int? TevkifatPayda { get; set; }
    public decimal OtvOrani { get; set; }
    public decimal OtvTutari { get; set; }
    public decimal OivOrani { get; set; }
    public decimal OivTutari { get; set; }
    public decimal KonaklamaVergisiOrani { get; set; }
    public decimal KonaklamaVergisiTutari { get; set; }
    public string? KaynakSatirId { get; set; }
}

/// <summary>
/// SatisBelgesiService.FaturaKesAsync isteği. İlk sürümde yalnızca standart SatisFaturasi için
/// otomatik resmî numara üretilir (bkz. FaturaKesAsync XML doc'u).
/// </summary>
public class FaturaKesRequest
{
    /// <summary>
    /// 3 alfanümerik karakter (A-Z, 0-9) — kurumun bu seri için önceden tanımlanmış AKTİF bir
    /// KurumFaturaNumaraSayaci kaydı olmalıdır. Trim + büyük harf normalize edilir.
    /// </summary>
    public string SeriKodu { get; set; } = string.Empty;
}

public class SatisBelgesiFilterDto
{
    public int? TesisId { get; set; }
    public List<SatisBelgesiTipi>? BelgeTipleri { get; set; }
    public SatisBelgesiDurumu? Durum { get; set; }
    public SatisKaynakModulu? KaynakModul { get; set; }
    public string? KaynakTipi { get; set; }
    public string? KaynakId { get; set; }
    public string? BelgeNo { get; set; }
    public string? Musteri { get; set; }
    public DateTime? BaslangicTarihi { get; set; }
    public DateTime? BitisTarihi { get; set; }
    public string? KarsiTarafFaturaNo { get; set; }
    public int? IadeEdilenBelgeId { get; set; }
}

public class SatisBelgesiKaynakDto
{
    public SatisKaynakModulu KaynakModul { get; set; }
    public string? KaynakTipi { get; set; }
    public string? KaynakId { get; set; }
}
