using System.Text.Json;
using STYS.Agent.Contracts.Dtos;
using STYS.Entegrasyonlar.Pos.Services;

namespace STYS.Tests.Agent;

public sealed class PavoReceiptSanitizerTests
{
    private static readonly JsonSerializerOptions AgentJsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Sanitize_GercekAgentPayload_CustomerMerchantErrorBase64_Temizlenir()
    {
        // Exactly what PavoStartPaymentCommandHandler produces: JsonSerializer.Serialize(response,
        // new JsonSerializerOptions(JsonSerializerDefaults.Web)). Data serializes as "data" (camelCase).
        var response = new PavoStartPaymentResponse
        {
            HasError = false,
            HasAbondon = false,
            ErrorCode = 0,
            Data = new PavoPaymentOperationData
            {
                IsSuccessful = true,
                SaleReference = "SALE-1",
                CustomerReceiptImage = "CUSTOMER-RECEIPT-BASE64",
                MerchantReceiptImage = "MERCHANT-RECEIPT-BASE64",
                ErrorReceiptImage = "ERROR-RECEIPT-BASE64"
            }
        };

        var payload = JsonSerializer.Serialize(response, AgentJsonOptions);
        Assert.Contains("\"data\"", payload);

        var sanitized = PavoReceiptSanitizer.Sanitize(payload);

        Assert.DoesNotContain("CUSTOMER-RECEIPT-BASE64", sanitized);
        Assert.DoesNotContain("MERCHANT-RECEIPT-BASE64", sanitized);
        Assert.DoesNotContain("ERROR-RECEIPT-BASE64", sanitized);
        Assert.Contains("\"SALE-1\"", sanitized);
    }

    [Fact]
    public void Sanitize_PascalCaseData_Temizlenir()
    {
        const string payload = """{"Data":{"customerReceiptImage":"AAA","merchantReceiptImage":"BBB","errorReceiptImage":"CCC","saleReference":"S"}}""";
        var sanitized = PavoReceiptSanitizer.Sanitize(payload);

        Assert.DoesNotContain("AAA", sanitized);
        Assert.DoesNotContain("BBB", sanitized);
        Assert.DoesNotContain("CCC", sanitized);
    }

    [Fact]
    public void Sanitize_ReceiptPropertyCasingKarisik_Temizlenir()
    {
        const string payload = """{"data":{"CustomerReceiptImage":"AAA","MERCHANTRECEIPTIMAGE":"BBB","ErrorReceiptImage":"CCC"}}""";
        var sanitized = PavoReceiptSanitizer.Sanitize(payload);

        Assert.DoesNotContain("AAA", sanitized);
        Assert.DoesNotContain("BBB", sanitized);
        Assert.DoesNotContain("CCC", sanitized);
    }

    [Fact]
    public void Sanitize_GecersizJson_OrjinaliDoner()
    {
        const string payload = "bu json degil";
        Assert.Equal(payload, PavoReceiptSanitizer.Sanitize(payload));
    }
}
