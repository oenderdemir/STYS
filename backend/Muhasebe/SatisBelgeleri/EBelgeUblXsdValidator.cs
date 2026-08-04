using System.Collections.Immutable;
using System.Xml;
using System.Xml.Schema;

namespace STYS.Muhasebe.SatisBelgeleri;

/// <summary>
/// Üretilen UBL XML'ini, sabitlenmiş yerel GİB XSD kural setine (bkz.
/// EBelgeUblKuralSeti/manifest.json) göre doğrular. İnternete hiçbir erişim yoktur -
/// <see cref="SandboxXmlResolver"/> yalnız kural setinin kök dizini ALTINDAKİ dosyalara izin
/// verir; DTD/harici entity işleme tamamen kapalıdır (bkz. görev md.15, md.19).
/// </summary>
public interface IEBelgeUblXsdValidator
{
    /// <summary>Geçersizse EBelgeUblXsdValidationFailedException fırlatır; geçerliyse sessizce döner.</summary>
    void Validate(ImmutableArray<byte> xmlBytes);

    /// <summary>
    /// İmzasız renderer çıktısı için: resmî XSD, kök Invoice elemanının ilk çocuğu olarak
    /// boş-olmayan bir ext:UBLExtensions/ext:UBLExtension/ext:ExtensionContent bekler - bu yuva
    /// yalnız İMZALAMA fazında (ds:Signature ile) doldurulabilir (bkz. görev md.10, "iki aşamalı
    /// doğrulama modeli"). Bu metot TAM OLARAK bu tek, önceden bilinen bulgu dışında BAŞKA HİÇBİR
    /// XSD hatası yoksa sessizce döner; başka herhangi bir hata (bu bulgu OLSUN ya da OLMASIN)
    /// varsa EBelgeUblXsdValidationFailedException fırlatır. Kırılgan tam metin eşitliği KURULMAZ -
    /// yalnız kararlı öznitelikler (eleman adı "Invoice", eksik yuva "UBLExtensions") kontrol edilir.
    /// </summary>
    void ValidateUnsignedRendererOutput(ImmutableArray<byte> xmlBytes);
}

public sealed class EBelgeUblXsdValidator : IEBelgeUblXsdValidator
{
    private const string InvoiceEntryRelativePath = "xsdrt/maindoc/UBL-Invoice-2.1.xsd";
    private const string NsInvoice = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";

    private readonly XmlSchemaSet _schemaSet;

    public EBelgeUblXsdValidator(GibKuralSeti kuralSeti)
    {
        var kokDizinTam = Path.GetFullPath(kuralSeti.KokDizin);
        var girisDosyasi = kuralSeti.Bul(InvoiceEntryRelativePath);
        var girisTamYol = kuralSeti.TamYol(girisDosyasi);

        var resolver = new EBelgeUblSandboxXmlResolver(kokDizinTam);

        // DTD ayrıştırma yalnız burada (sabit, hash doğrulanmış, vendored XSD dosyaları için)
        // açıktır - UBL-xmldsig-core-schema-2.1.xsd, W3C'nin orijinal şemasının yalnız İÇ (harici
        // SYSTEM/PUBLIC kimliği OLMAYAN) DOCTYPE alt kümesini içerir. XmlResolver yine de yalnız
        // kural seti kök dizinine sandbox'lanmıştır (md.15, md.19) - üretilen belge (instance XML)
        // doğrulamasında (bkz. Validate) DTD tamamen KAPALIDIR.
        var readerSettings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Parse,
            XmlResolver = resolver,
        };

        using var xsdStream = File.OpenRead(girisTamYol);
        using var xsdReader = XmlReader.Create(xsdStream, readerSettings, girisTamYol);

        var schemaSet = new XmlSchemaSet { XmlResolver = resolver };
        var derlemeHatalari = new List<string>();
        schemaSet.ValidationEventHandler += (_, e) => derlemeHatalari.Add(e.Message);
        schemaSet.Add(NsInvoice, xsdReader);
        schemaSet.Compile();
        if (derlemeHatalari.Count > 0)
        {
            throw new EBelgeUblKuralSetiManifestException("XSD kural seti derleme hataları: " + string.Join(" | ", derlemeHatalari));
        }

        _schemaSet = schemaSet;
    }

    public void Validate(ImmutableArray<byte> xmlBytes)
    {
        var hatalar = new List<string>();

        var readerSettings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            ValidationType = ValidationType.Schema,
            Schemas = _schemaSet,
            ConformanceLevel = ConformanceLevel.Document,
        };
        readerSettings.ValidationEventHandler += (_, e) =>
        {
            hatalar.Add($"Satır {e.Exception.LineNumber}, Sütun {e.Exception.LinePosition}: {e.Message}");
        };

        using var memoryStream = new MemoryStream(xmlBytes.ToArray());
        using var xmlReader = XmlReader.Create(memoryStream, readerSettings);

        try
        {
            while (xmlReader.Read())
            {
            }
        }
        catch (XmlException ex)
        {
            hatalar.Add($"Satır {ex.LineNumber}, Sütun {ex.LinePosition}: {ex.Message}");
        }

        if (hatalar.Count > 0)
        {
            throw new EBelgeUblXsdValidationFailedException(hatalar);
        }
    }

    public void ValidateUnsignedRendererOutput(ImmutableArray<byte> xmlBytes)
    {
        try
        {
            Validate(xmlBytes);
        }
        catch (EBelgeUblXsdValidationFailedException ex)
        {
            var beklenmeyenHatalar = ex.Hatalar
                .Where(h => !IsBilinenUblExtensionsBulgusu(h))
                .ToList();

            if (beklenmeyenHatalar.Count > 0 || ex.Hatalar.Count != 1)
            {
                throw new EBelgeUblXsdValidationFailedException(ex.Hatalar);
            }
        }
    }

    private static bool IsBilinenUblExtensionsBulgusu(string hataMesaji) =>
        hataMesaji.Contains("'Invoice'", StringComparison.Ordinal) &&
        hataMesaji.Contains("UBLExtensions", StringComparison.Ordinal);
}
