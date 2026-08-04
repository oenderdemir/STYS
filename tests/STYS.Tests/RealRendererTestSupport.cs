using STYS.Muhasebe.SatisBelgeleri;

namespace STYS.Tests;

/// <summary>GERÇEK renderer + GERÇEK yerel XSD doğrulayıcı + GERÇEK sidecar HTTP client'ını (verilen baseUrl'e) kurar - mock/sahte hiçbir bileşen yoktur.</summary>
internal static class RealRendererTestSupport
{
    public static IEBelgeUblRenderer CreateRealRenderer(string sidecarBaseUrl)
    {
        var kuralSeti = EBelgeUblRendererTestVerisi.KuralSetiYukle();
        var xsdValidator = new EBelgeUblXsdValidator(kuralSeti);
        var httpClient = new HttpClient { BaseAddress = new Uri(sidecarBaseUrl), Timeout = TimeSpan.FromSeconds(15) };
        var schematronValidator = new SaxonSidecarEBelgeSchematronValidator(httpClient);
        return new EBelgeUblRenderer(kuralSeti, xsdValidator, schematronValidator);
    }
}
