namespace STYS.Bildirimler.Dto;

public class BildirimOlusturRequestDto
{
    public string Tip { get; set; } = "Genel";
    public string Baslik { get; set; } = string.Empty;
    public string Mesaj { get; set; } = string.Empty;
    public string Severity { get; set; } = "info";
    public string? Link { get; set; }
    public string? KaynakUserAdi { get; set; }
    public Guid? KaynakUserId { get; set; }
}
