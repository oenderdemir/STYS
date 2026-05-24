namespace STYS.Rezervasyonlar.Dto;

/// <summary>
/// Rezervasyon check-out sonrası satış belgesi taslağı oluşturma request modeli.
/// Controller route'undaki {id} ile eşleşmelidir.
/// </summary>
public class RezervasyonSatisBelgesiTaslakRequest
{
    /// <summary>
    /// Satış belgesi oluşturulacak rezervasyonun Id'si.
    /// Route değeriyle aynı olmalıdır, değilse 400 hatası döner.
    /// </summary>
    public int RezervasyonId { get; set; }
}
