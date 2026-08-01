using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.SatisBelgeleri.Enums;

namespace STYS.TicariBelgeler.Dtos;

/// <summary>
/// Operasyonel, MİNİMAL lookup DTO'ları — TicariBelgeYonetimi.View yetkisiyle erişilir.
/// BİLİNÇLİ OLARAK İÇERMEZ: MuhasebeFisId, hesap kodları, borç/alacak bilgileri, entegratör
/// alanları, legacy SatisBelgesiDurumu, muhasebe servislerinin iç DTO'ları.
/// </summary>
public class TicariBelgeTesisLookupDto
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
}

public class TicariBelgeCariKartLookupDto
{
    public int Id { get; set; }
    public string CariKodu { get; set; } = string.Empty;
    public string CariTipi { get; set; } = string.Empty;
    public string UnvanAdSoyad { get; set; } = string.Empty;
    public string? VergiNoTckn { get; set; }
    public string? VergiDairesi { get; set; }
    public string? Adres { get; set; }
    public string? Eposta { get; set; }
    public string? Telefon { get; set; }
    public bool KurumsalMi { get; set; }
}

public class TicariBelgeKdvIstisnaLookupDto
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public KdvUygulamaTipi UygulamaTipi { get; set; }
}

/// <summary>KDV istisna lookup filtresi - belge yönü/tipi, satırın KDV uygulama tipi ve belge tarihine göre daraltılır.</summary>
public class TicariBelgeKdvIstisnaLookupFilterDto
{
    public SatisBelgesiTipi BelgeTipi { get; set; }
    public KdvUygulamaTipi KdvUygulamaTipi { get; set; }
    public DateTime BelgeTarihi { get; set; }
}

/// <summary>İade edilen belge adayı araması için filtre - kurum kapsamı istemciden ALINMAZ, sunucu tarafında çözümlenir; TesisId erişim kapsamına karşı doğrulanır.</summary>
public class TicariBelgeIadeAdayiFilterDto
{
    /// <summary>Düzenlenmekte olan mevcut belge (varsa) - aday listesinden hariç tutulur.</summary>
    public int? MevcutBelgeId { get; set; }
    public int TesisId { get; set; }
    /// <summary>SatisIadeFaturasi veya AlisIadeFaturasi olmalıdır.</summary>
    public SatisBelgesiTipi BelgeTipi { get; set; }
    public int CariKartId { get; set; }
    public DateTime BelgeTarihi { get; set; }
    public string? BelgeNoArama { get; set; }
}

public class TicariBelgeIadeAdayiDto
{
    public int Id { get; set; }
    public string BelgeNo { get; set; } = string.Empty;
    public DateTime BelgeTarihi { get; set; }
    public string? ResmiFaturaNo { get; set; }
    public string? KarsiTarafFaturaNo { get; set; }
}

/// <summary>Kaynak (iade edilen) belgenin satırlarını operasyonel/mali alanlarla gösterir - muhasebe hesap/fiş ayrıntısı İÇERMEZ.</summary>
public class TicariBelgeKaynakSatirDto
{
    public int Id { get; set; }
    public string Aciklama { get; set; } = string.Empty;
    public string Birim { get; set; } = string.Empty;
    public decimal Miktar { get; set; }
    /// <summary>Asıl miktardan, bu belge dışındaki geçerli iadelerin kümülatif toplamı düşülerek hesaplanır.</summary>
    public decimal IadeEdilebilirKalanMiktar { get; set; }
    public decimal BirimFiyat { get; set; }
    public decimal IndirimOrani { get; set; }
    public int KdvUygulamaTipi { get; set; }
    public decimal KdvOrani { get; set; }
    public int? KdvIstisnaTanimId { get; set; }
    public int? TevkifatPay { get; set; }
    public int? TevkifatPayda { get; set; }
}
