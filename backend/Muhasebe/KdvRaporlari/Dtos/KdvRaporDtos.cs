namespace STYS.Muhasebe.KdvRaporlari.Dtos;

public sealed class KdvRaporFilterDto
{
    public int? TesisId { get; set; }
    public DateTime? BaslangicTarihi { get; set; }
    public DateTime? BitisTarihi { get; set; }
    public string? BelgeYonu { get; set; }
    public bool IstisnalarDahilMi { get; set; } = true;
    public bool TevkifatDahilMi { get; set; } = true;
}

public sealed class KdvOzetRaporDto
{
    public DateTime BaslangicTarihi { get; set; }
    public DateTime BitisTarihi { get; set; }
    public KdvOzetRaporOzetDto Ozet { get; set; } = new();
    public List<KdvOranOzetDto> OranOzetleri { get; set; } = [];
    public List<KdvIstisnaOzetDto> IstisnaOzetleri { get; set; } = [];
}

public sealed class KdvOzetRaporOzetDto
{
    public int ToplamKayitSayisi { get; set; }
    public int SatisKayitSayisi { get; set; }
    public int AlisKayitSayisi { get; set; }
    public int IadeKayitSayisi { get; set; }

    public decimal SatisMatrahToplam { get; set; }
    public decimal HesaplananKdvToplam { get; set; }
    public decimal AlisMatrahToplam { get; set; }
    public decimal IndirilecekKdvToplam { get; set; }
    public decimal SatisIadeMatrahToplam { get; set; }
    public decimal SatisIadeKdvToplam { get; set; }
    public decimal AlisIadeMatrahToplam { get; set; }
    public decimal AlisIadeKdvToplam { get; set; }
    public decimal IstisnaMatrahToplam { get; set; }
    public decimal TevkifatToplam { get; set; }
    public decimal NetKdv { get; set; }
}

public sealed class KdvOranOzetDto
{
    public string IslemYonu { get; set; } = string.Empty;
    public decimal KdvOrani { get; set; }
    public int HareketSayisi { get; set; }
    public decimal Matrah { get; set; }
    public decimal KdvTutari { get; set; }
}

public sealed class KdvIstisnaOzetDto
{
    public string IslemYonu { get; set; } = string.Empty;
    public string? KdvIstisnaKodu { get; set; }
    public string? KdvIstisnaAciklamasi { get; set; }
    public int HareketSayisi { get; set; }
    public decimal Matrah { get; set; }
}

public sealed class TevkifatOzetRaporDto
{
    public DateTime BaslangicTarihi { get; set; }
    public DateTime BitisTarihi { get; set; }
    public decimal SatisTevkifatToplam { get; set; }
    public decimal AlisTevkifatToplam { get; set; }
    public decimal NetTevkifat { get; set; }
    public int ToplamKayitSayisi { get; set; }
    public List<TevkifatOranOzetDto> OranOzetleri { get; set; } = [];
}

public sealed class TevkifatOranOzetDto
{
    public string IslemYonu { get; set; } = string.Empty;
    public int TevkifatPay { get; set; }
    public int TevkifatPayda { get; set; }
    public int HareketSayisi { get; set; }
    public decimal Matrah { get; set; }
    public decimal TevkifatTutari { get; set; }
}

public sealed class KdvHareketRaporDto
{
    public DateTime BaslangicTarihi { get; set; }
    public DateTime BitisTarihi { get; set; }
    public int ToplamKayitSayisi { get; set; }
    public KdvHareketRaporOzetDto Ozet { get; set; } = new();
    public List<KdvHareketRaporSatiriDto> Satirlar { get; set; } = [];
}

public sealed class KdvHareketRaporOzetDto
{
    public int SatisKayitSayisi { get; set; }
    public int AlisKayitSayisi { get; set; }
    public int IadeKayitSayisi { get; set; }
    public int IstisnaKayitSayisi { get; set; }
    public int TevkifatKayitSayisi { get; set; }
    public decimal ToplamMatrah { get; set; }
    public decimal ToplamKdvTutari { get; set; }
}

public sealed class KdvHareketRaporSatiriDto
{
    public int BelgeId { get; set; }
    public string BelgeNo { get; set; } = string.Empty;
    public DateTime BelgeTarihi { get; set; }
    public string BelgeTipi { get; set; } = string.Empty;
    public string IslemYonu { get; set; } = string.Empty;
    public int SatirId { get; set; }
    public string SatirAciklama { get; set; } = string.Empty;
    public decimal Matrah { get; set; }
    public decimal KdvOrani { get; set; }
    public decimal KdvTutari { get; set; }
    public string KdvUygulamaTipi { get; set; } = string.Empty;
    public int? KdvIstisnaTanimId { get; set; }
    public string? KdvIstisnaKodu { get; set; }
    public string? KdvIstisnaAciklamasi { get; set; }
    public decimal? TevkifatPay { get; set; }
    public decimal? TevkifatPayda { get; set; }
    public decimal TevkifatTutari { get; set; }
}

public sealed class TevkifatHareketRaporDto
{
    public DateTime BaslangicTarihi { get; set; }
    public DateTime BitisTarihi { get; set; }
    public int ToplamKayitSayisi { get; set; }
    public TevkifatHareketRaporOzetDto Ozet { get; set; } = new();
    public List<TevkifatHareketRaporSatiriDto> Satirlar { get; set; } = [];
}

public sealed class TevkifatHareketRaporOzetDto
{
    public int SatisKayitSayisi { get; set; }
    public int AlisKayitSayisi { get; set; }
    public decimal ToplamMatrah { get; set; }
    public decimal ToplamTevkifatTutari { get; set; }
}

public sealed class TevkifatHareketRaporSatiriDto
{
    public int BelgeId { get; set; }
    public string BelgeNo { get; set; } = string.Empty;
    public DateTime BelgeTarihi { get; set; }
    public string BelgeTipi { get; set; } = string.Empty;
    public string IslemYonu { get; set; } = string.Empty;
    public int SatirId { get; set; }
    public string SatirAciklama { get; set; } = string.Empty;
    public decimal Matrah { get; set; }
    public decimal KdvTutari { get; set; }
    public int TevkifatPay { get; set; }
    public int TevkifatPayda { get; set; }
    public decimal TevkifatTutari { get; set; }
}
