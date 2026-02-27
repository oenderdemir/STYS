using TOD.Platform.Security.Auth.DTO;

namespace TOD.Platform.Security.Auth.DTO;

public class LoginResponseDto<TKey> : LoginResponseDto where TKey : struct
{
    public TKey? UserId { get; set; }
}
