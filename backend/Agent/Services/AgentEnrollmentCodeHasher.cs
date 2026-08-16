using System.Security.Cryptography;
using System.Text;

namespace STYS.Agent.Services;

/// <summary>
/// Single place that turns an enrollment code into its persisted form. The plaintext code is a
/// one-time secret: it is handed to the operator once at generation time and only ever stored as a
/// SHA-256 hash, so a database read can never recover a usable code.
/// </summary>
public static class AgentEnrollmentCodeHasher
{
    /// <summary>Length of the non-secret prefix retained for operator identification in listings.</summary>
    public const int PrefixLength = 6;

    public static string Hash(string code) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(code))));

    public static string BuildPrefix(string code)
    {
        var normalized = Normalize(code);
        return normalized.Length <= PrefixLength ? normalized : normalized[..PrefixLength];
    }

    /// <summary>Enrollment codes are generated from an unambiguous uppercase alphabet, so operators
    /// retyping one in a different case (or with stray whitespace) still enroll successfully.</summary>
    private static string Normalize(string code) => (code ?? string.Empty).Trim().ToUpperInvariant();
}
