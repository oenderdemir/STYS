namespace TOD.Platform.Security.Auth.DTO;

public class ChangePasswordRequestDto
{
    public string CurrentPassword { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;

    public string NewPassword2 { get; set; } = string.Empty;
}
