using System.Collections.Immutable;
using System.Text;
using STYS.Muhasebe.SatisBelgeleri;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Faz 2B.5 keşif/duman testleri. Üçü de bilinen, raporda belgelenmiş TEK birer bulguyu
/// kilitler - başka hiçbir yapısal hata veya regresyon sessizce eklenirse bu testler kırılır.
/// </summary>
public class EBelgeUblRendererSmokeTests
{
    private const string MinimalInvoiceXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
                 xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2">
          <cbc:UBLVersionID>2.1</cbc:UBLVersionID>
          <cbc:CustomizationID>TR1.2</cbc:CustomizationID>
          <cbc:ProfileID>EARSIVFATURA</cbc:ProfileID>
          <cbc:ID>EAR2026000000001</cbc:ID>
          <cbc:CopyIndicator>false</cbc:CopyIndicator>
          <cbc:UUID>a1b2c3d4-e5f6-4789-a012-b3c4d5e6f789</cbc:UUID>
          <cbc:IssueDate>2026-09-15</cbc:IssueDate>
          <cbc:IssueTime>11:00:00</cbc:IssueTime>
          <cbc:InvoiceTypeCode>SATIS</cbc:InvoiceTypeCode>
          <cbc:DocumentCurrencyCode>TRY</cbc:DocumentCurrencyCode>
        </Invoice>
        """;

    /// <summary>
    /// TARİHSEL - .NET'in yerel XSLT 1.0 motoruyla resmî GİB schematron kurallarının
    /// çalıştırılamadığını (exists() XPath 2.0 engeli) kanıtlar; bu, sidecar mimarisi kararının
    /// gerekçesidir (bkz. rapor, "Faz 2B.5 tamamlanma"). Üretimde artık kullanılmaz.
    /// </summary>
    [Fact]
    public void SchematronDerlemesiXPath2ExistsFonksiyonundaBilinenEngeleTakilir()
    {
        var kuralSeti = EBelgeUblRendererTestVerisi.KuralSetiYukle();

        var ex = Assert.ThrowsAny<Exception>(() => new EBelgeUblSchematronValidator(kuralSeti));

        Assert.Equal("System.Xml.Xsl.XslLoadException", ex.GetType().FullName);
        Assert.Contains("exists", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MinimalXmlXsdDogrulamasindaYalnizBilinenUblExtensionsBoslugunuVerir()
    {
        var kuralSeti = EBelgeUblRendererTestVerisi.KuralSetiYukle();
        var xmlBytes = ImmutableArray.Create(Encoding.UTF8.GetBytes(MinimalInvoiceXml));

        var xsdValidator = new EBelgeUblXsdValidator(kuralSeti);
        var ex = Assert.Throws<EBelgeUblXsdValidationFailedException>(() => xsdValidator.Validate(xmlBytes));

        Assert.Single(ex.Hatalar);
        Assert.Contains("UBLExtensions", ex.Hatalar[0]);
    }

    [Fact]
    public void ValidateUnsignedRendererOutputBilinenBoslukDisindaSessizceGecer()
    {
        var kuralSeti = EBelgeUblRendererTestVerisi.KuralSetiYukle();
        var xmlBytes = ImmutableArray.Create(Encoding.UTF8.GetBytes(MinimalInvoiceXml));

        var xsdValidator = new EBelgeUblXsdValidator(kuralSeti);

        // Bilinen tek bulgu (ext:UBLExtensions) dışında başka hata yoksa fırlatmaz.
        xsdValidator.ValidateUnsignedRendererOutput(xmlBytes);
    }

    [Fact]
    public void ValidateUnsignedRendererOutputBaskaHataVarsaFirlatir()
    {
        var kuralSeti = EBelgeUblRendererTestVerisi.KuralSetiYukle();
        // Kök eleman adı kasıtlı olarak "Invoice" DIŞINDA bir şeye değiştirildi - bilinen
        // UBLExtensions bulgusuyla EŞLEŞMEYEN, tamamen farklı bir XSD hatası üretir; filtrenin
        // yalnız TAM OLARAK bilinen bulguyu geçirdiğini, başka her şeyi fırlattığını kanıtlar.
        var bozukXml = MinimalInvoiceXml.Replace("<Invoice ", "<InvoiceYanlisKok ").Replace("</Invoice>", "</InvoiceYanlisKok>");
        var xmlBytes = ImmutableArray.Create(Encoding.UTF8.GetBytes(bozukXml));

        var xsdValidator = new EBelgeUblXsdValidator(kuralSeti);

        var ex = Assert.Throws<EBelgeUblXsdValidationFailedException>(() => xsdValidator.ValidateUnsignedRendererOutput(xmlBytes));
        Assert.DoesNotContain(ex.Hatalar, h => h.Contains("UBLExtensions", StringComparison.Ordinal));
    }
}
