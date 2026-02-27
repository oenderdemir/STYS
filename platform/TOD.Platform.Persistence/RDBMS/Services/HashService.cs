using System.Security.Cryptography;
using System.Text;

namespace TOD.Platform.Persistence.RDBMS.Services;

public class HashService : IHashService
{
    public Task<string> ComputeHash(string data)
    {
        var bytes = Encoding.UTF8.GetBytes(data);
        using var hash = SHA512.Create();
        var hashedInputBytes = hash.ComputeHash(bytes);

        var builder = new StringBuilder(128);
        foreach (var b in hashedInputBytes)
        {
            builder.Append(b.ToString("X2"));
        }

        return Task.FromResult(builder.ToString());
    }
}
