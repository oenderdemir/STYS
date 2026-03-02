namespace TOD.Platform.Identity.Users.DTO;

public class UserResetPasswordDto
{
    public string NewPassword { get; set; } = string.Empty;

    public string NewPassword2 { get; set; } = string.Empty;
}
