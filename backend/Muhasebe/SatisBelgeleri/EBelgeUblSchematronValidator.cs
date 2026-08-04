using System.Collections.Immutable;
using System.Xml;
using System.Xml.Xsl;

namespace STYS.Muhasebe.SatisBelgeleri;

/// <summary>
/// TARİHSEL/KANIT AMAÇLI - ÜRETİMDE ARTIK KULLANILMIYOR. Bu sınıf, .NET'in yerel XSLT 1.0
/// motoruyla (XslCompiledTransform) resmî GİB schematron kuralları çalıştırılmaya çalışıldığında
/// karşılaşılan gerçek engeli belgeler: UBL-TR_Common_Schematron.xml XPath 2.0 (exists()) kullanır,
/// XSLT1 bunu ÇALIŞTIRAMAZ (bkz. EBelgeUblRendererSmokeTests.SchematronDerlemesiXPath2ExistsFonksiyonundaBilinenEngeleTakilir
/// - bu test hâlâ BİLİNÇLİ olarak bu sınıfın başarısız olduğunu doğrular). Üretimdeki gerçek
/// çözüm <see cref="IEBelgeSchematronValidator"/> / <see cref="SaxonSidecarEBelgeSchematronValidator"/>
/// - ayrı bir Java Saxon-HE 13.0 sidecar servisidir (bkz. docs/e-belge-ubl-pdf-eposta-renderer-hazirlik-raporu.md,
/// "Faz 2B.5 tamamlanma" bölümü). DI'a KAYITLI DEĞİLDİR, renderer akışında KULLANILMAZ.
/// </summary>
public interface IEBelgeUblSchematronValidator
{
    /// <summary>İhlal varsa EBelgeUblSchematronValidationFailedException fırlatır; geçerliyse sessizce döner.</summary>
    void Validate(ImmutableArray<byte> xmlBytes);
}

public sealed class EBelgeUblSchematronValidator : IEBelgeUblSchematronValidator
{
    private const string MainSchematronRelativePath = "schematron/UBL-TR_Main_Schematron.xml";
    private const string SkeletonIncludeRelativePath = "schematron-skeleton/iso_dsdl_include.xsl";
    private const string SkeletonAbstractExpandRelativePath = "schematron-skeleton/iso_abstract_expand.xsl";
    private const string SkeletonSvrlRelativePath = "schematron-skeleton/iso_svrl_for_xslt1.xsl";

    private const string SvrlNamespace = "http://purl.oclc.org/dsdl/svrl";

    /// <summary>
    /// ISO schematron iskeleti (özellikle iso_dsdl_include.xsl), sch:include yönergelerini
    /// document() XSLT işleviyle çözer - bu yalnız BURADA, sabit/vendored dosyalar için ve
    /// yalnız sandbox'lanmış resolver ile açıktır. EnableScript KAPALI kalır (gömülü script
    /// çalıştırma yoktur). Üretilen belgenin (instance XML) kendisi bu ayarları KULLANMAZ.
    /// </summary>
    private static readonly XsltSettings SkeletonXsltSettings = new(enableDocumentFunction: true, enableScript: false);

    private readonly XslCompiledTransform _derlenmisDogrulayici;

    public EBelgeUblSchematronValidator(GibKuralSeti kuralSeti)
    {
        var kokDizinTam = Path.GetFullPath(kuralSeti.KokDizin);
        var resolver = new EBelgeUblSandboxXmlResolver(kokDizinTam);

        var mainSchematronYol = kuralSeti.TamYol(kuralSeti.Bul(MainSchematronRelativePath));
        var includeXslYol = kuralSeti.TamYol(kuralSeti.Bul(SkeletonIncludeRelativePath));
        var abstractExpandXslYol = kuralSeti.TamYol(kuralSeti.Bul(SkeletonAbstractExpandRelativePath));
        var svrlXslYol = kuralSeti.TamYol(kuralSeti.Bul(SkeletonSvrlRelativePath));

        // Aşama 1: sch:include yönergelerini çöz (UBL-TR_Codelist.xml, UBL-TR_Common_Schematron.xml).
        var asama1 = Donustur(includeXslYol, mainSchematronYol, resolver);
        // Aşama 2: sch:extends soyut kurallarını genişlet.
        var asama2 = Donustur(abstractExpandXslYol, asama1, resolver);
        // Aşama 3: genişletilmiş şemayı çalıştırılabilir bir SVRL-üretici XSLT'ye derle.
        var asama3DogrulayiciXslt = Donustur(svrlXslYol, asama2, resolver);

        var derlenmis = new XslCompiledTransform();
        using (XmlReader xsltReader = new XmlNodeReader(asama3DogrulayiciXslt))
        {
            derlenmis.Load(xsltReader, SkeletonXsltSettings, resolver);
        }

        _derlenmisDogrulayici = derlenmis;
    }

    public void Validate(ImmutableArray<byte> xmlBytes)
    {
        var instanceReaderSettings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };

        var instanceDoc = new XmlDocument();
        using (var memoryStream = new MemoryStream(xmlBytes.ToArray()))
        using (var reader = XmlReader.Create(memoryStream, instanceReaderSettings))
        {
            instanceDoc.Load(reader);
        }

        var svrlSonuc = new XmlDocument();
        using (var writer = svrlSonuc.CreateNavigator()!.AppendChild())
        {
            _derlenmisDogrulayici.Transform(instanceDoc, null, writer);
        }

        var ihlaller = new List<string>();
        var nsManager = new XmlNamespaceManager(svrlSonuc.NameTable);
        nsManager.AddNamespace("svrl", SvrlNamespace);

        var failedAsserts = svrlSonuc.SelectNodes("//svrl:failed-assert", nsManager);
        if (failedAsserts is not null)
        {
            foreach (XmlNode node in failedAsserts)
            {
                var location = node.Attributes?["location"]?.Value ?? "?";
                var text = node.SelectSingleNode("svrl:text", nsManager)?.InnerText?.Trim() ?? "(mesaj yok)";
                ihlaller.Add($"[{location}] {text}");
            }
        }

        if (ihlaller.Count > 0)
        {
            throw new EBelgeUblSchematronValidationFailedException(ihlaller);
        }
    }

    private static XmlDocument Donustur(string xsltYol, string girdiXmlYol, XmlResolver resolver)
    {
        var xslt = new XslCompiledTransform();
        // Sabit, hash doğrulanmış, vendored .xsl/.sch dosyaları için DTD ayrıştırma açık -
        // XmlResolver yine de yalnız kural seti kök dizinine sandbox'lanmıştır (üretilen belge
        // doğrulaması için bkz. Validate, orada DTD tamamen kapalıdır).
        var xsltReaderSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = resolver };
        using (var xsltStream = File.OpenRead(xsltYol))
        using (var xsltReader = XmlReader.Create(xsltStream, xsltReaderSettings, xsltYol))
        {
            xslt.Load(xsltReader, SkeletonXsltSettings, resolver);
        }

        var girdiReaderSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = resolver };
        using var girdiStream = File.OpenRead(girdiXmlYol);
        using var girdiReader = XmlReader.Create(girdiStream, girdiReaderSettings, girdiXmlYol);

        var sonuc = new XmlDocument();
        using var writer = sonuc.CreateNavigator()!.AppendChild();
        xslt.Transform(girdiReader, null, writer, resolver);

        return sonuc;
    }

    private static XmlDocument Donustur(string xsltYol, XmlDocument girdi, XmlResolver resolver)
    {
        var xslt = new XslCompiledTransform();
        // Sabit, hash doğrulanmış, vendored .xsl/.sch dosyaları için DTD ayrıştırma açık -
        // XmlResolver yine de yalnız kural seti kök dizinine sandbox'lanmıştır (üretilen belge
        // doğrulaması için bkz. Validate, orada DTD tamamen kapalıdır).
        var xsltReaderSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = resolver };
        using (var xsltStream = File.OpenRead(xsltYol))
        using (var xsltReader = XmlReader.Create(xsltStream, xsltReaderSettings, xsltYol))
        {
            xslt.Load(xsltReader, SkeletonXsltSettings, resolver);
        }

        var sonuc = new XmlDocument();
        using var writer = sonuc.CreateNavigator()!.AppendChild();
        xslt.Transform(new XmlNodeReader(girdi), null, writer, resolver);

        return sonuc;
    }
}
