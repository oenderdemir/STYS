namespace STYS.Kamp.Dto;

public class KampIadeHesaplamaRequestDto
{
    public string BasvuruDurumu { get; set; } = KampBasvuruDurumlari.Beklemede;
    public int? KampDonemiId { get; set; }
    public DateTime KampBaslangicTarihi { get; set; }
    public int ToplamGunSayisi { get; set; }
    public DateTime? VazgecmeTarihi { get; set; }
    public decimal AvansTutari { get; set; }
    public decimal DonemToplamTutari { get; set; }
    public decimal OdenenToplamTutar { get; set; }
    public bool MazeretliZorunluAyrilisMi { get; set; }
    public int KullanilmayanGunSayisi { get; set; }
}
