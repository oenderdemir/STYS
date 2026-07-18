namespace STYS.Rezervasyonlar.Dto;

/// <summary>Rezervasyon odeme ekranindan sinirli alanlarla (Musteri tipinde) hizli cari kart
/// olusturma istegi. Genel CariKart create endpoint'inin (CreateCariKartRequest) tersine,
/// hesap plani/banka hesabi/yetkili kisi/acilis bakiyesi gibi alanlar burada yer almaz.</summary>
public class RezervasyonCariKartHizliOlusturRequestDto
{
    public int TesisId { get; set; }

    public string UnvanAdSoyad { get; set; } = string.Empty;

    public string? VergiNoTckn { get; set; }

    public string? Telefon { get; set; }
}
