namespace STYS.Fiyatlandirma.Dto;

public class OdaFiyatHesaplaRequestDto
{
    public int TesisOdaTipiId { get; set; }

    public int KonaklamaTipiId { get; set; }

    public int MisafirTipiId { get; set; }

    public int KisiSayisi { get; set; } = 1;

    public bool TekKisilikFiyatUygulansinMi { get; set; }

    public DateTime? Tarih { get; set; }
}
