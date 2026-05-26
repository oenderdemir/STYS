using STYS.Muhasebe.SatisBelgeleri.Enums;
using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Muhasebe.SatisBelgeleri.Dtos;

public class SatisBelgesiDto : BaseRdbmsDto<int>
{
    public string BelgeNo { get; set; } = string.Empty;
    public SatisBelgesiTipi BelgeTipi { get; set; }
    public SatisBelgesiDurumu Durum { get; set; }
    public SatisKaynakModulu KaynakModul { get; set; }
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
    public decimal ToplamTevkifatTutari { get; set; }
    public decimal ToplamNetKdv { get; set; }
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
    public decimal IndirimTutari { get; set; }
    public int KdvUygulamaTipi { get; set; } = 1; // Kdvli
    public int? KdvIstisnaTanimId { get; set; }
    public decimal KdvOrani { get; set; }
    public int? TevkifatPay { get; set; }
    public int? TevkifatPayda { get; set; }
    public string? KaynakSatirId { get; set; }
}

public class UpdateSatisBelgesiRequest
{
    public string? BelgeNo { get; set; }
    public SatisBelgesiTipi? BelgeTipi { get; set; }
    public int? TesisId { get; set; }
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
    public decimal IndirimTutari { get; set; }
    public int KdvUygulamaTipi { get; set; } = 1;
    public int? KdvIstisnaTanimId { get; set; }
    public decimal KdvOrani { get; set; }
    public int? TevkifatPay { get; set; }
    public int? TevkifatPayda { get; set; }
    public string? KaynakSatirId { get; set; }
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
}

public class SatisBelgesiKaynakDto
{
    public SatisKaynakModulu KaynakModul { get; set; }
    public string? KaynakTipi { get; set; }
    public string? KaynakId { get; set; }
}
