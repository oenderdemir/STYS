namespace STYS.Muhasebe.MuhasebeFisleri.Dtos;

public class MuhasebeFisIptalSonucDto
{
    public int OrijinalFisId { get; set; }
    public int TersKayitFisId { get; set; }

    /// <summary>True ise orijinal fis zaten daha once ters kayitla iptal edilmisti; bu cagri
    /// yeni bir ters kayit URETMEDI, mevcut olani buldu (idempotent kisa-devre).</summary>
    public bool ZatenTersKayitliMi { get; set; }
}
