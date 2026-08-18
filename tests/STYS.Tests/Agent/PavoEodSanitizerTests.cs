using System.Text.Json;
using STYS.Entegrasyonlar.Pos.Services;

namespace STYS.Tests.Agent;

public sealed class PavoEodSanitizerTests
{
    [Fact]
    public void Sanitize_EodImageBase64_Temizlenir()
    {
        const string payload = """
        {"data":{"gunSonu":"OK","eodImage":"iVBORw0KGgoAAAANSUhEUg=="},"hasError":false,"HasAbondon":false}
        """;

        var sanitized = PavoEodSanitizer.Sanitize(payload);

        Assert.DoesNotContain("iVBORw0KGgoAAAANSUhEUg==", sanitized);
        Assert.Contains("\"gunSonu\"", sanitized);
    }

    [Fact]
    public void Sanitize_CardNoRecursiveOlarakKaldirilir()
    {
        const string payload = """
        {
          "data": {
            "gunSonu": "OK",
            "eodData": {
              "acquirers": [
                { "cardTransactions": [ { "transactions": [ { "cardNo": "1234********9999", "amount": 1 } ] } ] }
              ]
            }
          }
        }
        """;

        var sanitized = PavoEodSanitizer.Sanitize(payload);

        Assert.DoesNotContain("cardNo", sanitized);
        Assert.DoesNotContain("1234", sanitized);
        Assert.Contains("acquirers", sanitized);
    }

    [Fact]
    public void Sanitize_CardNoCasingDuyarsizKaldirilir()
    {
        const string payload = """{"data":{"eodData":{"CardNo":"X","CARDNO":"Y","cardno":"Z"}}}""";
        var sanitized = PavoEodSanitizer.Sanitize(payload);
        Assert.DoesNotContain("cardNo", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("X", sanitized);
        Assert.DoesNotContain("Y", sanitized);
        Assert.DoesNotContain("Z", sanitized);
    }

    [Fact]
    public void SanitizeEodData_CardNoKaldirilirVeJsonDoner()
    {
        using var doc = JsonDocument.Parse("""{"merchant":"M","cardNo":"1111"}""");
        var result = PavoEodSanitizer.SanitizeEodData(doc.RootElement);

        Assert.DoesNotContain("cardNo", result);
        Assert.Contains("merchant", result);
    }

    [Fact]
    public void SanitizeEodData_YoksaNullDoner()
    {
        Assert.Null(PavoEodSanitizer.SanitizeEodData(null));
        using var doc = JsonDocument.Parse("null");
        Assert.Null(PavoEodSanitizer.SanitizeEodData(doc.RootElement));
    }
}
