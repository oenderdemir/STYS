using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.SatisBelgeleri.Enums;

namespace STYS.TicariBelgeler.Dtos;

/// <summary>
/// Operasyon modülleri (resepsiyon, rezervasyon, restoran, kamp vb.) için TicariBelge özet
/// görünümü. Muhasebe fişi/hesap planı/borç-alacak/entegratör/legacy SatisBelgesiDurumu gibi
/// muhasebeye özgü hiçbir alan İÇERMEZ — bkz. TicariBelgeService ve görev C/D.
/// </summary>
public class TicariBelgeDto
{
    public int Id { get; set; }
    public string BelgeNo { get; set; } = string.Empty;
    public SatisBelgesiTipi BelgeTipi { get; set; }

    /// <summary>OTORİTER ticari (hazırlık) durumu — bkz. TicariBelgeIslemYetkisi.</summary>
    public TicariBelgeDurumu TicariDurum { get; set; }
    /// <summary>
    /// OTORİTER muhasebeleştirme durumu — operasyon personelinin "onay bekliyor/reddedildi"
    /// bilgisini bilmesi için gösterilir; muhasebe işlemini (onaylama/reddetme/fiş üretme) bu
    /// katman ÜZERİNDEN yapmak MÜMKÜN DEĞİLDİR.
    /// </summary>
    public TicariBelgeMuhasebeDurumu MuhasebeDurumu { get; set; }
    /// <summary>OTORİTER faturalama/gönderim durumu.</summary>
    public TicariBelgeFaturalamaDurumu FaturalamaDurumu { get; set; }
    /// <summary>Üç otoriter durumdan türetilen, operasyon personeline yönelik kısa Türkçe özet (bkz. TicariBelgeIslemYetkisi.OperasyonelDurumAciklamasi).</summary>
    public string OperasyonelDurumAciklamasi { get; set; } = string.Empty;

    public SatisKaynakModulu KaynakModul { get; set; }
    public string? KaynakTipi { get; set; }
    public string? KaynakId { get; set; }

    public int? TesisId { get; set; }
    public int? CariKartId { get; set; }

    public DateTime BelgeTarihi { get; set; }
    public DateTime? VadeTarihi { get; set; }

    public bool KurumsalMi { get; set; }
    public string? MusteriUnvan { get; set; }
    public string? MusteriAdSoyad { get; set; }
    public string? MusteriVergiNo { get; set; }
    public string? MusteriTcKimlikNo { get; set; }
    public string? MusteriVergiDairesi { get; set; }
    public string? MusteriAdres { get; set; }
    public string? MusteriEposta { get; set; }
    public string? MusteriTelefon { get; set; }

    public decimal ToplamMatrah { get; set; }
    public decimal ToplamKdv { get; set; }
    public decimal ToplamTevkifatTutari { get; set; }
    public decimal ToplamNetKdv { get; set; }
    public decimal GenelToplam { get; set; }

    public string? Aciklama { get; set; }
    public string? RedNedeni { get; set; }

    public string? ResmiFaturaNo { get; set; }
    public string? KarsiTarafFaturaNo { get; set; }
    public int? IadeEdilenBelgeId { get; set; }

    public DateTime? MuhasebeOnayinaGonderilmeTarihi { get; set; }
    public DateTime? MuhasebeOnayTarihi { get; set; }
    public DateTime? FaturaKesimTarihi { get; set; }
    public DateTime? MusteriyeGonderimTarihi { get; set; }

    // ── İşlem yetenekleri — TicariBelgeIslemYetkisi'nden türetilir, frontend enum yorumlamaz ──
    public bool GuncellenebilirMi { get; set; }
    public bool SilinebilirMi { get; set; }
    public bool MuhasebeOnayinaGonderilebilirMi { get; set; }
    public bool IptalEdilebilirMi { get; set; }
}

/// <summary>TicariBelgeDto'ya satır bilgisini ekleyen ayrıntı görünümü (GetById/oluşturma/güncelleme sonuçları için).</summary>
public class TicariBelgeDetayDto : TicariBelgeDto
{
    public List<TicariBelgeSatirDto> Satirlar { get; set; } = [];
}

public class TicariBelgeSatirDto
{
    public int Id { get; set; }
    public int SiraNo { get; set; }
    public SatisBelgesiSatirTipi SatirTipi { get; set; }
    public string Aciklama { get; set; } = string.Empty;
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

public class TicariBelgeFilterDto
{
    public int? TesisId { get; set; }
    public List<SatisBelgesiTipi>? BelgeTipleri { get; set; }
    public TicariBelgeDurumu? TicariDurum { get; set; }
    public TicariBelgeMuhasebeDurumu? MuhasebeDurumu { get; set; }
    public SatisKaynakModulu? KaynakModul { get; set; }
    public string? KaynakTipi { get; set; }
    public string? KaynakId { get; set; }
    public string? BelgeNo { get; set; }
    public string? Musteri { get; set; }
    public DateTime? BaslangicTarihi { get; set; }
    public DateTime? BitisTarihi { get; set; }
}

/// <summary>Operasyon modüllerinin ortak fatura altyapısına taslak oluşturmak için göndereceği request modeli.</summary>
public class TicariBelgeTaslakOlusturRequest
{
    public SatisKaynakModulu KaynakModul { get; set; }
    public string KaynakTipi { get; set; } = string.Empty;
    public string KaynakId { get; set; } = string.Empty;

    public int? TesisId { get; set; }
    public int? CariKartId { get; set; }

    public DateTime BelgeTarihi { get; set; }
    public DateTime? VadeTarihi { get; set; }

    public bool KurumsalMi { get; set; }
    public string? MusteriUnvan { get; set; }
    public string? MusteriAdSoyad { get; set; }
    public string? MusteriVergiNo { get; set; }
    public string? MusteriTcKimlikNo { get; set; }
    public string? MusteriVergiDairesi { get; set; }
    public string? MusteriAdres { get; set; }
    public string? MusteriEposta { get; set; }
    public string? MusteriTelefon { get; set; }

    public string? Aciklama { get; set; }

    public List<TicariBelgeTaslakSatirRequest> Satirlar { get; set; } = [];
}

/// <summary>Taslak satır request modeli. SiraNo yoktur; servis tarafından liste sırasına göre 1'den başlayarak atanır.</summary>
public class TicariBelgeTaslakSatirRequest
{
    public SatisBelgesiSatirTipi SatirTipi { get; set; } = SatisBelgesiSatirTipi.Diger;
    public string Aciklama { get; set; } = string.Empty;
    public decimal Miktar { get; set; }
    public decimal BirimFiyat { get; set; }
    public KdvUygulamaTipi KdvUygulamaTipi { get; set; } = KdvUygulamaTipi.Kdvli;
    public decimal IndirimOrani { get; set; }
    public decimal IndirimTutari { get; set; }
    public decimal KdvOrani { get; set; }
    public int? KdvIstisnaTanimId { get; set; }
    public decimal OtvOrani { get; set; }
    public decimal OtvTutari { get; set; }
    public decimal OivOrani { get; set; }
    public decimal OivTutari { get; set; }
    public decimal KonaklamaVergisiOrani { get; set; }
    public decimal KonaklamaVergisiTutari { get; set; }
    public string? KaynakSatirId { get; set; }
}

/// <summary>TicariBelgeService.UpdateAsync için güncelleme request modeli — yalnızca güncellenebilir/reddedilmiş belgeler için geçerlidir (bkz. TicariBelgeIslemYetkisi.GuncellenebilirMi).</summary>
public class TicariBelgeGuncelleRequest
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
    public string? KarsiTarafFaturaNo { get; set; }
    public int? IadeEdilenBelgeId { get; set; }
    public bool IadeEdilenBelgeReferansiKaldir { get; set; }
    public List<TicariBelgeGuncelleSatirRequest>? Satirlar { get; set; }
}

public class TicariBelgeGuncelleSatirRequest
{
    public int SiraNo { get; set; }
    public SatisBelgesiSatirTipi SatirTipi { get; set; } = SatisBelgesiSatirTipi.Diger;
    public string Aciklama { get; set; } = string.Empty;
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
