namespace STYS.Raporlar.Dto;

public class OdaDolulukTahsilatDto
{
    public int RezervasyonId { get; set; }

    public int? OdaId { get; set; }

    public string? OdaNo { get; set; }

    public DateTime OdemeTarihi { get; set; }

    public decimal OdemeTutari { get; set; }

    public string ParaBirimi { get; set; } = "TRY";

    public string? OdemeTipi { get; set; }

    public string? Aciklama { get; set; }

    public string? MisafirAdiSoyadi { get; set; }

    public string? KurumUnite { get; set; }

    public string? ReferansNo { get; set; }

    public DateTime? GirisTarihi { get; set; }

    public DateTime? CikisTarihi { get; set; }

    // TODO: RezervasyonOdeme entity'sine MakbuzNo alani eklendiginde buradan doldurulacak.
    public string? MakbuzNo { get; set; }

    // TODO: RezervasyonOdeme entity'sine ayri bir "odeme yapan" alani eklendiginde (kurum yetkilisi vb.) buradan doldurulacak; su an misafir adi kullanilir.
    public string? OdemeYapan { get; set; }
}
