using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace STYS.Muhasebe.SatisBelgeleri;

/// <summary>Vendored GİB UBL-TR kural setinin (XSD + schematron + schematron iskeleti) kimliği ve dosya listesi.</summary>
public sealed record GibKuralSeti(
    string KuralSetiKimligi,
    string UblVersionId,
    string CustomizationId,
    string KokDizin,
    ImmutableArray<GibKuralSetiDosyasi> Manifest)
{
    /// <summary>Manifestteki bir dosyanın diskteki tam yolunu döner.</summary>
    public string TamYol(GibKuralSetiDosyasi dosya) => Path.Combine(KokDizin, dosya.GoreliYol.Replace('/', Path.DirectorySeparatorChar));

    public GibKuralSetiDosyasi Bul(string goreliYol) =>
        Manifest.FirstOrDefault(d => string.Equals(d.GoreliYol, goreliYol, StringComparison.Ordinal))
        ?? throw new EBelgeUblKuralSetiManifestException($"Manifestte beklenen dosya bulunamadı: {goreliYol}");
}

public sealed record GibKuralSetiDosyasi(string GoreliYol, string Sha256);

/// <summary>manifest.json'ın ham JSON şeması (yalnız deserialize amaçlı).</summary>
internal sealed class EBelgeUblKuralSetiManifestJson
{
    [JsonPropertyName("kuralSetiKimligi")]
    public string KuralSetiKimligi { get; set; } = string.Empty;

    [JsonPropertyName("ublVersionId")]
    public string UblVersionId { get; set; } = string.Empty;

    [JsonPropertyName("customizationId")]
    public string CustomizationId { get; set; } = string.Empty;

    [JsonPropertyName("dosyalar")]
    public List<EBelgeUblKuralSetiManifestJsonDosya> Dosyalar { get; set; } = new();
}

internal sealed class EBelgeUblKuralSetiManifestJsonDosya
{
    [JsonPropertyName("goreliYol")]
    public string GoreliYol { get; set; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;
}

/// <summary>
/// GİB kural seti manifestini (backend/Muhasebe/SatisBelgeleri/EBelgeUblKuralSeti/manifest.json)
/// sabit, yerel bir dizinden yükler ve HER dosyanın SHA-256'sını manifestteki kayıtlı değerle
/// karşılaştırır. İnternetten hiçbir dosya indirilmez; yalnız sabitlenmiş yerel dizine erişilir.
/// Eşleşmeyen tek bir dosya bile TÜM yüklemeyi kalıcı bir yapılandırma hatasıyla reddeder (bkz.
/// görev md.14 - "tek dosyada bile hash uyuşmazlığı: açık, kalıcı bir yapılandırma/artefakt hatası").
/// </summary>
public interface IEBelgeUblKuralSetiYukleyici
{
    GibKuralSeti Yukle();
}

public sealed class EBelgeUblKuralSetiYukleyici : IEBelgeUblKuralSetiYukleyici
{
    private readonly string _kokDizin;

    /// <param name="kokDizin">manifest.json'ı ve xsdrt/schematron/schematron-skeleton alt dizinlerini içeren dizin.</param>
    public EBelgeUblKuralSetiYukleyici(string kokDizin)
    {
        _kokDizin = kokDizin;
    }

    public GibKuralSeti Yukle()
    {
        var manifestYolu = Path.Combine(_kokDizin, "manifest.json");
        if (!File.Exists(manifestYolu))
        {
            throw new EBelgeUblKuralSetiManifestException("Kural seti manifest.json bulunamadı.");
        }

        EBelgeUblKuralSetiManifestJson? ham;
        try
        {
            var manifestJson = File.ReadAllText(manifestYolu);
            ham = JsonSerializer.Deserialize<EBelgeUblKuralSetiManifestJson>(manifestJson);
        }
        catch (JsonException)
        {
            throw new EBelgeUblKuralSetiManifestException("Kural seti manifest.json çözümlenemedi.");
        }

        if (ham is null || ham.Dosyalar.Count == 0)
        {
            throw new EBelgeUblKuralSetiManifestException("Kural seti manifest.json boş veya geçersiz.");
        }

        var dosyalar = ImmutableArray.CreateBuilder<GibKuralSetiDosyasi>();

        foreach (var d in ham.Dosyalar)
        {
            var tamYol = Path.Combine(_kokDizin, d.GoreliYol.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(tamYol))
            {
                throw new EBelgeUblKuralSetiManifestException($"Kural seti dosyası eksik: {d.GoreliYol}");
            }

            using var stream = File.OpenRead(tamYol);
            var hesaplananHash = Convert.ToHexStringLower(SHA256.HashData(stream));
            if (!string.Equals(hesaplananHash, d.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new EBelgeUblKuralSetiManifestException($"Kural seti dosyası bozuk veya değiştirilmiş (SHA-256 uyuşmuyor): {d.GoreliYol}");
            }

            dosyalar.Add(new GibKuralSetiDosyasi(d.GoreliYol, hesaplananHash));
        }

        return new GibKuralSeti(
            ham.KuralSetiKimligi,
            ham.UblVersionId,
            ham.CustomizationId,
            _kokDizin,
            dosyalar.ToImmutable());
    }
}
