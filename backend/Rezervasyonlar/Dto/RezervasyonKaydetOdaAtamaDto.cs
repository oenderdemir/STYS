using System.ComponentModel.DataAnnotations;

namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonKaydetOdaAtamaDto
{
    [Range(1, int.MaxValue)]
    public int OdaId { get; set; }

    [Range(1, int.MaxValue)]
    public int AyrilanKisiSayisi { get; set; }
}
