using STYS.Bildirimler;

namespace STYS.Bildirimler.Dto;

public class BildirimDto
{
    public int Id { get; set; }
    public string Tip { get; set; } = string.Empty;
    public string Baslik { get; set; } = string.Empty;
    public string Mesaj { get; set; } = string.Empty;
    public string? Link { get; set; }
    public string? KaynakUserAdi { get; set; }
    public string Severity { get; set; } = BildirimSeverityleri.Info;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
