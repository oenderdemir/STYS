namespace TOD.Platform.Security.Auth.Models;

public class IdentityUser<TKey> where TKey : struct
{
    public TKey Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string? Name { get; set; }

    public string? Surname { get; set; }

    public string? Email { get; set; }

    public string? Status { get; set; }
}
