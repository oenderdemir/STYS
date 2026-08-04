using System.Xml;

namespace STYS.Muhasebe.SatisBelgeleri;

/// <summary>
/// XSD ve schematron doğrulayıcıları tarafından paylaşılan, internete tamamen kapalı XML
/// çözümleyici. Yalnız verilen kök dizin ALTINDAKİ dosya sistemine izin verir; http(s) veya kök
/// dizin dışına (path traversal) giden hiçbir referansa izin VERMEZ (bkz. görev md.15, md.19).
/// </summary>
internal sealed class EBelgeUblSandboxXmlResolver : XmlUrlResolver
{
    private readonly string _kokDizinTam;

    public EBelgeUblSandboxXmlResolver(string kokDizinTam)
    {
        _kokDizinTam = Path.GetFullPath(kokDizinTam).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public override Uri ResolveUri(Uri? baseUri, string? relativeUri)
    {
        var resolved = base.ResolveUri(baseUri, relativeUri);

        if (!resolved.IsFile)
        {
            throw new EBelgeUblKuralSetiManifestException("Kural seti dışı (yerel olmayan) şema referansına izin verilmiyor.");
        }

        var resolvedFullPath = Path.GetFullPath(resolved.LocalPath);
        if (!resolvedFullPath.StartsWith(_kokDizinTam, StringComparison.OrdinalIgnoreCase))
        {
            throw new EBelgeUblKuralSetiManifestException("Kural seti kök dizini dışına şema referansına izin verilmiyor.");
        }

        return resolved;
    }
}
