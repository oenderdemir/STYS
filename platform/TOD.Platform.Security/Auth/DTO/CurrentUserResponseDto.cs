namespace TOD.Platform.Security.Auth.DTO;

public class CurrentUserResponseDto
{
    public string? UserName { get; set; }

    public string? UserStatus { get; set; }

    public string? DefaultRoute { get; set; }

    public int? AktifKurumId { get; set; }

    public List<int> KurumIds { get; set; } = [];

    public List<int> KurumAdminKurumIds { get; set; } = [];

    public bool IsKurumAdmin { get; set; }

    public bool IsSuperAdmin { get; set; }
}
