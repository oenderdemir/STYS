using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using STYS.Kbs.Options;

namespace STYS.Kbs.Payload;

public sealed record KbsPayloadSnapshot
{
    public string Version { get; init; } = "2";
    public string BildirimTipi { get; init; } = string.Empty;
    public int KurumId { get; init; }
    public int TesisId { get; init; }
    public int RezervasyonId { get; init; }
    public int RezervasyonKonaklayanId { get; init; }
    public DateTime OlayTarihi { get; init; }
    public string? Ad { get; init; }
    public string? Soyad { get; init; }
    public string? KimlikTuru { get; init; }
    public string? KimlikNo { get; init; }
    public string? BelgeNo { get; init; }
    public string? BelgeTuru { get; init; }
    public string? UyrukKodu { get; init; }
    public DateTime? DogumTarihi { get; init; }
    public string? DogumYeri { get; init; }
    public string? Cinsiyet { get; init; }
    public string? Telefon { get; init; }
    public string? AracPlakasi { get; init; }
    public string? KonaklamaKullanimSekli { get; init; }
    public int? OdaDegisiklikAtamaId { get; init; }
    public Guid? OdaDegisiklikEventId { get; init; }
    public int? EskiOdaId { get; init; }
    public string? EskiOdaNo { get; init; }
    public int? YeniOdaId { get; init; }
    public string? YeniOdaNo { get; init; }
}

public static class KbsCanonicalPayload
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    public static string Serialize(KbsPayloadSnapshot snapshot) => JsonSerializer.Serialize(snapshot, JsonOptions);
    public static KbsPayloadSnapshot Deserialize(string json) => JsonSerializer.Deserialize<KbsPayloadSnapshot>(json, JsonOptions)
        ?? throw new InvalidOperationException("KBS payload snapshot okunamadi.");
    public static string Hash(string canonicalJson) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson)));
}

public interface IKbsPayloadProtector
{
    bool IsReady { get; }
    string Protect(string canonicalJson);
    string Unprotect(string protectedPayload);
}

public sealed class DataProtectionKbsPayloadProtector : IKbsPayloadProtector
{
    private readonly IDataProtector _protector;
    private readonly IHostEnvironment _environment;
    private readonly KbsOptions _options;

    public DataProtectionKbsPayloadProtector(IDataProtectionProvider provider, IHostEnvironment environment, IOptions<KbsOptions> options)
    {
        _protector = provider.CreateProtector("STYS.Kbs.Payload.v2");
        _environment = environment;
        _options = options.Value;
    }

    public bool IsReady => !_environment.IsProduction()
        || (!string.IsNullOrWhiteSpace(_options.PayloadProtectionKeyRingPath)
            && Directory.Exists(_options.PayloadProtectionKeyRingPath));

    public string Protect(string canonicalJson)
    {
        EnsureReady();
        return _protector.Protect(canonicalJson);
    }

    public string Unprotect(string protectedPayload)
    {
        EnsureReady();
        return _protector.Unprotect(protectedPayload);
    }

    private void EnsureReady()
    {
        if (!IsReady) throw new InvalidOperationException("KBS payload protection key-ring yapilandirmasi hazir degil.");
    }
}
