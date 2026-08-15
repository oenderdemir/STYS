using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Modules.Pavo;

namespace STYS.Tests.Agent;

/// <summary>
/// Golden contract tests derived from the verified-working Pavo509.Client reference project
/// (pavo.rar). Fixtures below are the reference request/response shapes; any drift in property
/// names, nesting, JSON primitive types, or success/sequence semantics must fail here.
/// </summary>
public sealed class PavoReferenceGoldenContractTests
{
    private const string ReferenceFingerprint = "Pavo509DotNetClient";
    private const string ReferenceSerialNumber = "PAV200019619";

    // ---------------------------------------------------------------- A. Pairing request

    [Fact]
    public async Task A_PairingRequest_ReferansGoldenJsonIleBirebir()
    {
        var handler = new CapturingHandler();
        var client = CreateClient(handler);

        await client.PairingAsync(BuildPairingRequest(), CancellationToken.None);

        const string expected = """
        {
          "TransactionHandle": {
            "SerialNumber": "PAV200019619",
            "Fingerprint": "Pavo509DotNetClient",
            "TransactionSequence": 1,
            "TransactionDate": "2026-08-15T10:20:30.123456"
          }
        }
        """;

        AssertJsonStructurallyEqual(expected, handler.LastBody);
        Assert.Equal("/Pairing", handler.LastRequestUri?.AbsolutePath);
        Assert.Equal("http", handler.LastRequestUri?.Scheme);
        Assert.Equal(4567, handler.LastRequestUri?.Port);
    }

    // ---------------------------------------------------------------- B. Pairing response

    [Fact]
    public async Task B_PairingResponse_ReferansGoldenJsonDeserializeOlur()
    {
        // Reference PavoResponse shape, including a device-side TransactionHandle.
        const string body = """
        {
          "HasAbondon": false,
          "HasError": false,
          "ErrorCode": 0,
          "Message": "Eşleştirme başarılı",
          "TransactionHandle": {
            "SerialNumber": "PAV200019619",
            "Fingerprint": "DEVICE-FP",
            "TransactionSequence": 47,
            "TransactionDate": "2026-08-15T10:20:31.000000"
          },
          "Errors": null,
          "Data": null
        }
        """;

        var handler = new CapturingHandler { ResponseBody = body };
        var client = CreateClient(handler);

        var response = await client.PairingAsync(BuildPairingRequest(), CancellationToken.None);

        Assert.False(response.HasError);
        Assert.False(response.HasAbondon);
        Assert.Equal(0, response.ErrorCode);
        Assert.Equal("Eşleştirme başarılı", response.Message);
        Assert.Null(response.Errors);
        Assert.NotNull(response.TransactionHandle);
        Assert.Equal(47, response.TransactionHandle!.TransactionSequence);
        Assert.True(PavoResponseHelpers.IsSuccessful(response));
    }

    // ---------------------------------------------------------------- C. StartPayment request

    [Fact]
    public async Task C_StartPaymentRequest_ReferansGoldenJsonIleBirebir()
    {
        var handler = new CapturingHandler();
        var client = CreateClient(handler);

        await client.StartPaymentAsync(BuildReferenceStartPaymentRequest(), CancellationToken.None);

        var json = handler.LastBody ?? throw new Xunit.Sdk.XunitException("Request body missing.");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Exactly two top-level properties, matching StartPaymentRequest.
        Assert.Equal(["Payment", "TransactionHandle"], PropertyNames(root));

        var payment = root.GetProperty("Payment");
        Assert.Equal(
        [
            "AdditionalInfo", "AllowDismissCardRead", "Amount", "CardReadTimeout", "CurrencyCode",
            "CustomApp", "CustomCommission", "CustomLogin", "InstallmentCount", "IsPfInstallmentEnabled",
            "MaxInstallmentCount", "MinInstallmentCount", "PinEntryTimeout", "Puan", "SaleReference",
            "SelectedSlots", "SelectedTerminals"
        ], PropertyNames(payment));

        Assert.Equal(125.50m, payment.GetProperty("Amount").GetDecimal());
        Assert.Equal(0, payment.GetProperty("InstallmentCount").GetInt32());
        Assert.Equal("TRY", payment.GetProperty("CurrencyCode").GetString());
        Assert.Equal("SALE-123", payment.GetProperty("SaleReference").GetString());
        Assert.Equal(60, payment.GetProperty("CardReadTimeout").GetInt32());
        Assert.Equal(30, payment.GetProperty("PinEntryTimeout").GetInt32());
        Assert.True(payment.GetProperty("AllowDismissCardRead").GetBoolean());
        Assert.False(payment.GetProperty("IsPfInstallmentEnabled").GetBoolean());
        Assert.Equal(0m, payment.GetProperty("Puan").GetDecimal());
        Assert.Equal(JsonValueKind.Null, payment.GetProperty("MinInstallmentCount").ValueKind);
        Assert.Equal(JsonValueKind.Null, payment.GetProperty("MaxInstallmentCount").ValueKind);
        // Reference sends null - not [] - when no terminals are selected.
        Assert.Equal(JsonValueKind.Null, payment.GetProperty("SelectedTerminals").ValueKind);
        Assert.Equal(
            ["rf", "icc", "magneticStripe", "qr", "manual"],
            payment.GetProperty("SelectedSlots").EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray());

        var additionalInfo = payment.GetProperty("AdditionalInfo");
        Assert.Equal(
        [
            "customerReceiptImageEnabled", "footer", "headUnmaskLength", "header",
            "isCustomerReceiptPrintEnabled", "isMerchantReceiptPrintEnabled",
            "isResponseBeforePrintEnabled", "list", "merchantReceiptImageEnabled", "print",
            "printData", "qrCodeText", "receiptImage", "receiptWidth", "tailUnmaskLength"
        ], PropertyNames(additionalInfo));

        // Reference: header is the configured ReceiptHeader, never the payment description.
        Assert.Equal("ÖDEME BİLGİSİ", additionalInfo.GetProperty("header").GetString());
        Assert.Equal("İyi günler dileriz.", additionalInfo.GetProperty("footer").GetString());
        Assert.Equal("SALE-123", additionalInfo.GetProperty("qrCodeText").GetString());
        Assert.Equal("58mm", additionalInfo.GetProperty("receiptWidth").GetString());
        Assert.Equal(4, additionalInfo.GetProperty("headUnmaskLength").GetInt32());
        Assert.Equal(4, additionalInfo.GetProperty("tailUnmaskLength").GetInt32());
        Assert.True(additionalInfo.GetProperty("print").GetBoolean());
        Assert.False(additionalInfo.GetProperty("isResponseBeforePrintEnabled").GetBoolean());

        // Reference receipt list: exactly three rows, in this order. No "Açıklama:" row.
        var list = additionalInfo.GetProperty("list").EnumerateArray().ToArray();
        Assert.Equal(3, list.Length);
        Assert.Equal("İşlem referansı:", list[0].GetProperty("name").GetString());
        Assert.Equal("SALE-123", list[0].GetProperty("value").GetString());
        Assert.Equal("Ödeme tutarı:", list[1].GetProperty("name").GetString());
        // Reference formats the amount with "N2" under the machine's current culture (no explicit
        // InvariantCulture), so the expectation is culture-relative in exactly the same way.
        Assert.Equal($"{125.50m:N2} TL", list[1].GetProperty("value").GetString());
        Assert.Equal("İşlem tarihi:", list[2].GetProperty("name").GetString());

        var printData = additionalInfo.GetProperty("printData");
        Assert.Equal(
        [
            "customerReceiptJsonEnabled", "customerReceiptTextEnabled", "customerReceiptTextWidth",
            "merchantReceiptJsonEnabled", "merchantReceiptTextEnabled", "merchantReceiptTextWidth",
            "receiptJsonEnabled", "receiptTextEnabled", "receiptTextWidth"
        ], PropertyNames(printData));
        Assert.Equal("40", printData.GetProperty("receiptTextWidth").GetString());
    }

    // ---------------------------------------------------------------- D. StartPayment success

    [Fact]
    public async Task D_StartPaymentSuccessResponse_ReferansGoldenJsonDeserializeOlur()
    {
        var handler = new CapturingHandler { ResponseBody = SuccessfulPaymentBody };
        var client = CreateClient(handler);

        var response = await client.StartPaymentAsync(BuildReferenceStartPaymentRequest(), CancellationToken.None);

        Assert.True(PavoResponseHelpers.IsPaymentSuccessful(response));
        var data = response.Data!;
        Assert.Equal(9876543210L, data.Id);
        Assert.Equal(1234L, data.TransactionNo);
        Assert.Equal(77L, data.BatchNo);
        Assert.True(data.IsSuccessful);
        Assert.Equal("ONAYLANDI", data.StatusText);
        Assert.Equal("SALE-123", data.SaleReference);
        Assert.Equal(125.50m, data.Amount);
        Assert.Equal("TRY", data.CurrencyCode);
        Assert.Equal("00", data.ResponseCode);
        Assert.Equal("Ziraat", data.AcquirerName);
        Assert.Equal("RRN-1", data.RetrievalReferenceNo);
        Assert.Equal("AUTH-1", data.AuthorizationCode);
        Assert.Equal("Onaylandı", data.CevapAciklama);
        Assert.Equal(1, data.ResultStatus);
        Assert.Equal("2026-08-15 10:20:35", data.ResultDate);
        Assert.Equal("TERM-PAY", data.Terminal);
    }

    // ------------------------------------------- E. Business failure despite a clean envelope

    [Fact]
    public async Task E_StartPayment_HttpBasariliAmaDataIsSuccessfulFalse_BasarisizSayilir()
    {
        const string body = """
        {
          "HasAbondon": false,
          "HasError": false,
          "ErrorCode": 0,
          "Data": { "isSuccessful": false, "statusText": "RED", "failMessage": "Yetersiz bakiye" }
        }
        """;

        var handler = new CapturingHandler { ResponseBody = body };
        var client = CreateClient(handler);

        var response = await client.StartPaymentAsync(BuildReferenceStartPaymentRequest(), CancellationToken.None);

        // The envelope alone reads as success...
        Assert.True(PavoResponseHelpers.IsSuccessful(response));
        // ...but StartPayment additionally requires Data.IsSuccessful.
        Assert.False(PavoResponseHelpers.IsPaymentSuccessful(response));
    }

    // ---------------------------------------------------- F/G. Empty and malformed responses

    [Fact]
    public async Task F_Http200BosGovde_Basarisizdir()
    {
        var handler = new CapturingHandler { ResponseBody = string.Empty };
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<PavoRestClientException>(
            () => client.PairingAsync(BuildPairingRequest(), CancellationToken.None));

        Assert.Equal("EMPTY_RESPONSE", ex.ErrorCode);
        Assert.True(ex.HttpResponseReceived);
    }

    [Fact]
    public async Task G_Http200BozukJson_Basarisizdir()
    {
        var handler = new CapturingHandler { ResponseBody = "{ this is not json" };
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<PavoRestClientException>(
            () => client.PairingAsync(BuildPairingRequest(), CancellationToken.None));

        Assert.Equal("INVALID_RESPONSE", ex.ErrorCode);
        Assert.True(ex.HttpResponseReceived);
    }

    // ------------------------------------- H. Response sequence never drives request sequence

    [Fact]
    public async Task H_ResponseSequence_SonrakiRequestSequenceyiEtkilemez()
    {
        const string body = """
        {
          "HasAbondon": false, "HasError": false, "ErrorCode": 0,
          "TransactionHandle": {
            "SerialNumber": "PAV200019619", "Fingerprint": "DEVICE-FP",
            "TransactionSequence": 50, "TransactionDate": "2026-08-15T10:20:31.000000"
          }
        }
        """;

        var handler = new CapturingHandler { ResponseBody = body };
        var client = CreateClient(handler);

        var response = await client.PairingAsync(BuildPairingRequest(), CancellationToken.None);

        // The device reports its own sequence (50); it is remote metadata only.
        Assert.Equal(50, response.TransactionHandle!.TransactionSequence);

        // A client that advanced from the response would now send 51. Reference advances its own
        // counter by one instead, so the next request goes out on 2.
        var store = new InMemorySequenceStore();
        Assert.Equal(1, await store.PeekAsync());
        await store.AdvanceAsync();
        Assert.Equal(2, await store.PeekAsync());
    }

    // -------------------------------------- I/J. Retry sequence after failure vs. no response

    [Fact]
    public async Task I_HttpCevabiAlinanIsFailure_SonrakiDenemeSequenceyiIlerletir()
    {
        // Reference: _transactionSequence++ runs whenever an HTTP response was received - business
        // error included - so the retry goes out on the next number.
        var handler = new CapturingHandler
        {
            ResponseBody = """{"HasAbondon":false,"HasError":true,"ErrorCode":17,"Message":"Eşleştirme reddedildi"}"""
        };
        var client = CreateClient(handler);
        var store = new InMemorySequenceStore();

        var first = await store.PeekAsync();
        var response = await client.PairingAsync(BuildPairingRequest(first), CancellationToken.None);
        await store.AdvanceAsync();

        Assert.False(PavoResponseHelpers.IsSuccessful(response));
        Assert.Equal(1, first);
        Assert.Equal(2, await store.PeekAsync());
    }

    [Fact]
    public async Task J_BaglantiHatasi_SonrakiDenemeAyniSequenceyiKullanir()
    {
        var handler = new ThrowingHandler(new HttpRequestException(
            "connection refused", new SocketException((int)SocketError.ConnectionRefused)));
        var client = CreateClient(handler);
        var store = new InMemorySequenceStore();

        var first = await store.PeekAsync();
        var ex = await Assert.ThrowsAsync<PavoRestClientException>(
            () => client.PairingAsync(BuildPairingRequest(first), CancellationToken.None));

        Assert.Equal("CONNECTION_REFUSED", ex.ErrorCode);
        // No HTTP response means the reference leaves its counter untouched.
        Assert.False(ex.HttpResponseReceived);
        Assert.Equal(1, await store.PeekAsync());
    }

    [Fact]
    public async Task J_Timeout_SonrakiDenemeAyniSequenceyiKullanir()
    {
        var handler = new ThrowingHandler(new TaskCanceledException("timeout"));
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<PavoRestClientException>(
            () => client.PairingAsync(BuildPairingRequest(), CancellationToken.None));

        Assert.Equal("TIMEOUT", ex.ErrorCode);
        Assert.False(ex.HttpResponseReceived);
    }

    // ---------------------------------------------------------------- K. Payment wire types

    [Fact]
    public async Task K_PaymentDataWireTipleri_ReferansIleBirebir()
    {
        // transactionNo/batchNo are JSON numbers and resultStatus is a JSON number in the
        // reference contract; resultDate stays a string. A string-typed transactionNo must NOT
        // deserialize into the numeric property.
        var handler = new CapturingHandler { ResponseBody = SuccessfulPaymentBody };
        var client = CreateClient(handler);

        var response = await client.StartPaymentAsync(BuildReferenceStartPaymentRequest(), CancellationToken.None);
        var data = response.Data!;

        Assert.IsType<long>(data.TransactionNo!.Value);
        Assert.IsType<long>(data.BatchNo!.Value);
        Assert.IsType<int>(data.ResultStatus!.Value);
        Assert.IsType<string>(data.ResultDate!);

        var handlerWithStringTypes = new CapturingHandler
        {
            ResponseBody = """
            {"HasError":false,"HasAbondon":false,"ErrorCode":0,
             "Data":{"isSuccessful":true,"transactionNo":"1234"}}
            """
        };
        var stringClient = CreateClient(handlerWithStringTypes);

        await Assert.ThrowsAsync<PavoRestClientException>(
            () => stringClient.StartPaymentAsync(BuildReferenceStartPaymentRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task K_CardNo_DomainModeleTasinmaz()
    {
        var handler = new CapturingHandler { ResponseBody = SuccessfulPaymentBody };
        var client = CreateClient(handler);

        var response = await client.StartPaymentAsync(BuildReferenceStartPaymentRequest(), CancellationToken.None);

        // cardNo is on the wire but must never survive the wire-to-domain boundary.
        var serialized = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("cardNo", handler.ResponseBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("454360", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cardNo", serialized, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------- L. PerformEOD

    [Fact]
    public async Task L_PerformEodRequest_ReferansGoldenJsonIleBirebir()
    {
        var handler = new CapturingHandler();
        var client = CreateClient(handler);

        await client.PerformEodAsync(new PavoPerformEodRequest
        {
            IpAddress = "10.0.0.5",
            HttpPort = 4567,
            UseSummary = true,
            Print = false,
            ReceiptImage = true,
            TransactionHandle = BuildHandle(1)
        }, CancellationToken.None);

        const string expected = """
        {
          "PerformEOD": {
            "AdditionalInfo": {
              "print": false,
              "receiptImage": true,
              "useSummary": true,
              "receiptWidth": "58mm",
              "printData": {
                "receiptJsonEnabled": true,
                "receiptTextEnabled": true,
                "receiptTextWidth": "40"
              }
            }
          },
          "TransactionHandle": {
            "SerialNumber": "PAV200019619",
            "Fingerprint": "Pavo509DotNetClient",
            "TransactionSequence": 1,
            "TransactionDate": "2026-08-15T10:20:30.123456"
          }
        }
        """;

        AssertJsonStructurallyEqual(expected, handler.LastBody);
        Assert.Equal("/PerformEOD", handler.LastRequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task L_PerformEodResponse_EodAlanlariDeserializeOlur()
    {
        const string body = """
        {
          "HasAbondon": false, "HasError": false, "ErrorCode": 0,
          "Data": {
            "isSuccessful": true,
            "gunSonu": "OK",
            "eodData": { "total": 1250.75 },
            "eodJson": { "rows": [1, 2] },
            "eodText": "GUN SONU RAPORU",
            "eodImage": "BASE64DATA"
          }
        }
        """;

        var handler = new CapturingHandler { ResponseBody = body };
        var client = CreateClient(handler);

        var response = await client.PerformEodAsync(new PavoPerformEodRequest
        {
            IpAddress = "10.0.0.5",
            HttpPort = 4567,
            TransactionHandle = BuildHandle(1)
        }, CancellationToken.None);

        Assert.True(PavoResponseHelpers.IsSuccessful(response));
        Assert.Equal("OK", response.Data!.GunSonu);
        Assert.Equal("BASE64DATA", response.Data.EodImage);
        Assert.Equal(1250.75m, response.Data.EodData!.Value.GetProperty("total").GetDecimal());
        Assert.Equal(JsonValueKind.Object, response.Data.EodJson!.Value.ValueKind);
        Assert.Equal("GUN SONU RAPORU", response.Data.EodText!.Value.GetString());
    }

    // ------------------------------------------------- M/N/O. Reboot and PIN mode commands

    [Theory]
    [InlineData("reboot")]
    [InlineData("enterPinMode")]
    [InlineData("exitPinMode")]
    public async Task MNO_CihazKomutlari_YalnizcaTransactionHandleGonderir(string operation)
    {
        var handler = new CapturingHandler();
        var client = CreateClient(handler);

        var (expectedPath, message) = operation switch
        {
            "reboot" => ("/RebootDevice", await RebootAsync(client, handler)),
            "enterPinMode" => ("/EnterPinMode", await EnterPinAsync(client, handler)),
            _ => ("/ExitPinMode", await ExitPinAsync(client, handler))
        };

        const string expected = """
        {
          "TransactionHandle": {
            "SerialNumber": "PAV200019619",
            "Fingerprint": "Pavo509DotNetClient",
            "TransactionSequence": 1,
            "TransactionDate": "2026-08-15T10:20:30.123456"
          }
        }
        """;

        AssertJsonStructurallyEqual(expected, handler.LastBody);
        Assert.Equal(expectedPath, handler.LastRequestUri?.AbsolutePath);
        Assert.Equal("TAMAM", message);
    }

    private static async Task<string?> RebootAsync(IPavoRestClient client, CapturingHandler handler)
    {
        handler.ResponseBody = """{"HasError":false,"HasAbondon":false,"ErrorCode":0,"Data":{"reboot":"TAMAM"}}""";
        var response = await client.RebootDeviceAsync(new PavoRebootDeviceRequest
        {
            IpAddress = "10.0.0.5",
            HttpPort = 4567,
            TransactionHandle = BuildHandle(1)
        }, CancellationToken.None);
        return response.Data?.Reboot;
    }

    private static async Task<string?> EnterPinAsync(IPavoRestClient client, CapturingHandler handler)
    {
        handler.ResponseBody = """{"HasError":false,"HasAbondon":false,"ErrorCode":0,"Data":{"enterPinModeMessage":"TAMAM"}}""";
        var response = await client.EnterPinModeAsync(new PavoEnterPinModeRequest
        {
            IpAddress = "10.0.0.5",
            HttpPort = 4567,
            TransactionHandle = BuildHandle(1)
        }, CancellationToken.None);
        return response.Data?.EnterPinModeMessage;
    }

    private static async Task<string?> ExitPinAsync(IPavoRestClient client, CapturingHandler handler)
    {
        handler.ResponseBody = """{"HasError":false,"HasAbondon":false,"ErrorCode":0,"Data":{"exitPinModeMessage":"TAMAM"}}""";
        var response = await client.ExitPinModeAsync(new PavoExitPinModeRequest
        {
            IpAddress = "10.0.0.5",
            HttpPort = 4567,
            TransactionHandle = BuildHandle(1)
        }, CancellationToken.None);
        return response.Data?.ExitPinModeMessage;
    }

    // ---------------------------------------------------------------- Payment validation

    [Theory]
    [InlineData(0, 0, "SALE-1")]      // amount must be > 0
    [InlineData(-5, 0, "SALE-1")]
    [InlineData(100, 1, "SALE-1")]    // installment 1 is invalid
    [InlineData(100, -2, "SALE-1")]
    [InlineData(100, 0, "  ")]        // sale reference must be non-blank
    public async Task PaymentValidation_ReferansKurallariIleAyni(decimal amount, int installmentCount, string saleReference)
    {
        var handler = new CapturingHandler();
        var client = CreateClient(handler);

        var request = BuildReferenceStartPaymentRequest();
        request.Amount = amount;
        request.InstallmentCount = installmentCount;
        request.SaleReference = saleReference;

        var ex = await Assert.ThrowsAsync<PavoRestClientException>(
            () => client.StartPaymentAsync(request, CancellationToken.None));

        Assert.Equal("INVALID_REQUEST", ex.ErrorCode);
        // Never reached the device, so the outgoing sequence must not move.
        Assert.False(ex.HttpResponseReceived);
        Assert.Null(handler.LastBody);
    }

    // ---------------------------------------------------------------- helpers

    private const string SuccessfulPaymentBody = """
    {
      "HasAbondon": false,
      "HasError": false,
      "ErrorCode": 0,
      "Message": null,
      "Errors": null,
      "Data": {
        "id": 9876543210,
        "transactionNo": 1234,
        "batchNo": 77,
        "isSuccessful": true,
        "statusText": "ONAYLANDI",
        "saleReference": "SALE-123",
        "amount": 125.50,
        "currencyCode": "TRY",
        "cardNo": "454360******1234",
        "cardReaderSlotText": "TEMASSIZ",
        "responseCode": "00",
        "acquirerName": "Ziraat",
        "retrievalReferenceNo": "RRN-1",
        "authorizationCode": "AUTH-1",
        "failMessage": null,
        "cevapAciklama": "Onaylandı",
        "resultStatus": 1,
        "resultDate": "2026-08-15 10:20:35",
        "terminal": "TERM-PAY",
        "customerReceiptImage": null,
        "merchantReceiptImage": null
      }
    }
    """;

    private static PavoPairingRequest BuildPairingRequest(long sequence = 1) => new()
    {
        IpAddress = "10.0.0.5",
        HttpPort = 4567,
        UseHttps = false,
        TransactionHandle = BuildHandle(sequence)
    };

    private static PavoStartPaymentRequest BuildReferenceStartPaymentRequest() => new()
    {
        IpAddress = "10.0.0.5",
        HttpPort = 4567,
        UseHttps = false,
        SaleReference = "SALE-123",
        Amount = 125.50m,
        CurrencyCode = "TRY",
        InstallmentCount = 0,
        TransactionHandle = BuildHandle(1)
    };

    private static PavoTransactionHandle BuildHandle(long sequence) => new()
    {
        SerialNumber = ReferenceSerialNumber,
        Fingerprint = ReferenceFingerprint,
        TransactionSequence = sequence,
        TransactionDate = new DateTime(2026, 8, 15, 10, 20, 30, DateTimeKind.Local).AddTicks(1234560)
    };

    private static PavoRestClient CreateClient(HttpMessageHandler handler)
    {
        var factory = new SingleClientFactory(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1")
        });

        return new PavoRestClient(factory, NullLogger<PavoRestClient>.Instance);
    }

    private static string[] PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal).ToArray();

    /// <summary>Structural JSON comparison: property names, nesting, primitive kinds and values must
    /// match. Property ordering and whitespace are not protocol-significant and are ignored, but a
    /// type mismatch (e.g. 123 vs "123") fails.</summary>
    private static void AssertJsonStructurallyEqual(string expectedJson, string? actualJson)
    {
        Assert.NotNull(actualJson);
        using var expected = JsonDocument.Parse(expectedJson);
        using var actual = JsonDocument.Parse(actualJson!);
        AssertElementsEqual(expected.RootElement, actual.RootElement, "$");
    }

    private static void AssertElementsEqual(JsonElement expected, JsonElement actual, string path)
    {
        Assert.True(
            expected.ValueKind == actual.ValueKind,
            $"{path}: JSON türü farklı. Beklenen={expected.ValueKind}, Gerçek={actual.ValueKind}");

        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                var expectedNames = PropertyNames(expected);
                var actualNames = PropertyNames(actual);
                Assert.True(
                    expectedNames.SequenceEqual(actualNames, StringComparer.Ordinal),
                    $"{path}: property kümesi farklı.\nBeklenen: {string.Join(", ", expectedNames)}\nGerçek:   {string.Join(", ", actualNames)}");
                foreach (var property in expected.EnumerateObject())
                {
                    AssertElementsEqual(property.Value, actual.GetProperty(property.Name), $"{path}.{property.Name}");
                }
                break;

            case JsonValueKind.Array:
                var expectedItems = expected.EnumerateArray().ToArray();
                var actualItems = actual.EnumerateArray().ToArray();
                Assert.True(expectedItems.Length == actualItems.Length, $"{path}: dizi uzunluğu farklı.");
                for (var i = 0; i < expectedItems.Length; i++)
                {
                    AssertElementsEqual(expectedItems[i], actualItems[i], $"{path}[{i}]");
                }
                break;

            case JsonValueKind.Number:
                Assert.True(
                    expected.GetDecimal() == actual.GetDecimal(),
                    $"{path}: sayı farklı. Beklenen={expected.GetDecimal()}, Gerçek={actual.GetDecimal()}");
                break;

            case JsonValueKind.String:
                Assert.True(
                    expected.GetString() == actual.GetString(),
                    $"{path}: metin farklı. Beklenen={expected.GetString()}, Gerçek={actual.GetString()}");
                break;
        }
    }

    /// <summary>Mirrors the reference client's in-memory counter so sequence expectations in these
    /// tests are stated against the reference algorithm rather than STYS persistence.</summary>
    private sealed class InMemorySequenceStore
    {
        private long _sequence = 1;

        public Task<long> PeekAsync() => Task.FromResult(_sequence);

        public Task AdvanceAsync()
        {
            _sequence++;
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? LastBody { get; private set; }
        public Uri? LastRequestUri { get; private set; }
        public string ResponseBody { get; set; } = """{"HasError":false,"HasAbondon":false,"ErrorCode":0}""";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ResponseBody, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        private readonly Exception _exception;

        public ThrowingHandler(Exception exception) => _exception = exception;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw _exception;
    }

    private sealed class SingleClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public SingleClientFactory(HttpClient client) => _client = client;

        public HttpClient CreateClient(string name) => _client;
    }
}
