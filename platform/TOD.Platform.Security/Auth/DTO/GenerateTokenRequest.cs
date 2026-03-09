namespace TOD.Platform.Security.Auth.DTO;

public class GenerateTokenRequest
{
    public string UserId { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Surname { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public List<string> Permissions { get; set; } = new();

    public int TokenVersion { get; set; }
}
