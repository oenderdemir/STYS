namespace STYS.Binalar.Dto;

public class BinaIsletmeAlaniDto
{
    public int? Id { get; set; }

    public int IsletmeAlaniSinifiId { get; set; }

    public string? IsletmeAlaniSinifiAd { get; set; }

    public string? OzelAd { get; set; }

    public bool AktifMi { get; set; } = true;
}
