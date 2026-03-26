namespace STYS.ErisimTeshis.Dto;

public class ErisimTeshisKullaniciDto
{
    public Guid Id { get; set; }

    public string KullaniciAdi { get; set; } = string.Empty;

    public string AdSoyad { get; set; } = string.Empty;

    public string? Eposta { get; set; }
}
