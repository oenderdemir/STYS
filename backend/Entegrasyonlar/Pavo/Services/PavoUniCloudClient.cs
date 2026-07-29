using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using STYS.Entegrasyonlar.Pos.Entities;
using STYS.Entegrasyonlar.Pavo.Options;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Entegrasyonlar.Pavo.Services;

public sealed class PavoUniCloudClient : IPavoUniCloudClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly PavoOptions _options;
    private readonly ILogger<PavoUniCloudClient> _logger;

    public PavoUniCloudClient(
        HttpClient httpClient,
        IOptions<PavoOptions> options,
        ILogger<PavoUniCloudClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PavoPairingResult> PairingRequestAsync(PosTerminal terminal, CancellationToken cancellationToken)
    {
        var token = await GetTerminalAccessTokenAsync(terminal.SerialNumber, cancellationToken);
        var root = await PostAsync(
            "/api/PaymentLinkIntegration/PairingRequest",
            new
            {
                terminal.SourceFingerprint,
                TargetSerialNo = terminal.SerialNumber,
                ApplicationName = "STYS",
                SourceReference = terminal.SourceTerminalReference ?? $"STYS-TESIS-{terminal.TesisId}"
            },
            token,
            cancellationToken);

        var data = GetData(root);
        return new PavoPairingResult(
            GetLong(data, "Id"),
            GetString(data, "PairingCode"),
            GetString(data, "TargetFingerprint"),
            GetBool(data, "IsApproved"));
    }

    public async Task<PavoPairingResult> CheckPairingAsync(PosTerminal terminal, CancellationToken cancellationToken)
    {
        if (!terminal.PairingId.HasValue)
        {
            throw new BaseException("PAVO terminali icin once eslestirme talebi olusturulmalidir.", 400);
        }

        var token = await GetTerminalAccessTokenAsync(terminal.SerialNumber, cancellationToken);
        var root = await PostAsync(
            "/api/PaymentLinkIntegration/CheckPairing",
            new
            {
                PairingId = terminal.PairingId.Value,
                TargetSerialNo = terminal.SerialNumber,
                terminal.SourceFingerprint
            },
            token,
            cancellationToken);

        var data = GetData(root);
        return new PavoPairingResult(
            GetLong(data, "Id"),
            GetString(data, "PairingCode"),
            GetString(data, "TargetFingerprint"),
            GetBool(data, "IsApproved"));
    }

    public async Task<PavoCreateLinkResult> CreateLinkAsync(
        PosTerminal terminal,
        string reference,
        decimal amount,
        string currency,
        CancellationToken cancellationToken)
    {
        var token = await GetTerminalAccessTokenAsync(terminal.SerialNumber, cancellationToken);
        var payload = new
        {
            LinkType = "P",
            terminal.SourceFingerprint,
            SourceTerminalReference = terminal.SourceTerminalReference ?? $"STYS-TESIS-{terminal.TesisId}",
            terminal.TargetFingerprint,
            TargetSerialNo = terminal.SerialNumber,
            Request = new
            {
                Payment = new
                {
                    Amount = amount,
                    CurrencyCode = currency,
                    AdditionalInfo = new
                    {
                        print = true,
                        receiptImage = false,
                        customerReceiptImageEnabled = true,
                        merchantReceiptImageEnabled = false,
                        receiptWidth = "58mm",
                        headUnmaskLength = 4,
                        tailUnmaskLength = 4
                    }
                }
            },
            PaymentLinkReference = reference,
            PaymentAmount = amount,
            CurrencyCode = currency,
            RequestedMethod = "P",
            ApplicationName = "STYS",
            DisplayLayout = terminal.Ad
        };

        var root = await PostAsync("/api/PaymentLinkIntegration/CreateLinkRequest", payload, token, cancellationToken);
        var data = GetData(root);
        return new PavoCreateLinkResult(GetLong(data, "Id"), GetInt(data, "StatusId"), root.ToJsonString());
    }

    public async Task<PavoCheckLinkResult> CheckLinkAsync(
        PosTerminal terminal,
        long paymentLinkId,
        string reference,
        CancellationToken cancellationToken)
    {
        var token = await GetTerminalAccessTokenAsync(terminal.SerialNumber, cancellationToken);
        var root = await PostAsync(
            "/api/PaymentLinkIntegration/CheckLinkRequest",
            new { PaymentLinkId = paymentLinkId, PaymentLinkReference = reference },
            token,
            cancellationToken);

        var data = GetData(root);
        var statusId = GetInt(data, "StatusId");
        var responseText = GetString(data, "Response");
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return new PavoCheckLinkResult(statusId, true, false, root.ToJsonString(), null, null, null, null);
        }

        try
        {
            var response = JsonNode.Parse(responseText);
            var transaction = FindProperty(response, "Data");
            var successful = GetBool(transaction, "isSuccessful");
            var hasError = GetBool(response, "HasError");
            var error = GetString(transaction, "failMessage")
                ?? GetString(response, "Message")
                ?? (hasError ? "PAVO islemi basarisiz oldu." : null);

            return new PavoCheckLinkResult(
                statusId,
                false,
                successful,
                root.ToJsonString(),
                successful ? null : error ?? "PAVO odemesi onaylanmadi.",
                GetString(transaction, "retrievalReferenceNo"),
                GetString(transaction, "acquirerReference"),
                GetString(transaction, "authorizationCode"));
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "PAVO CheckLink Response alani JSON olarak okunamadi. PaymentLinkId={PaymentLinkId}", paymentLinkId);
            return new PavoCheckLinkResult(statusId, false, false, root.ToJsonString(), "PAVO yaniti cozumlenemedi.", null, null, null);
        }
    }

    private async Task<string> GetTerminalAccessTokenAsync(string serialNumber, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var initialToken = await AuthenticateAsync(_options.ApiKey, cancellationToken);

        using var merchantRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/Merchant/TerminalApiKey/FindMerchant?serialNumber={Uri.EscapeDataString(serialNumber)}");
        merchantRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", initialToken);
        var merchantRoot = await SendAsync(merchantRequest, cancellationToken);
        var merchantData = FindProperty(merchantRoot, "Data") ?? merchantRoot;
        var merchantUid = GetString(merchantData, "MerchantUid")
            ?? GetString(merchantData, "Uid")
            ?? GetString(merchantData, "merchantUid");
        if (string.IsNullOrWhiteSpace(merchantUid))
        {
            throw new BaseException("PAVO uye isyeri kimligi bulunamadi.", 502);
        }

        var terminalKeyRoot = await PostAsync(
            "/api/Merchant/TerminalApiKey/CreateTerminalApikeyForUniCloud",
            new { MerchantUid = merchantUid, SerialNo = serialNumber },
            initialToken,
            cancellationToken);
        var terminalKey = GetString(terminalKeyRoot, "Data");
        if (string.IsNullOrWhiteSpace(terminalKey))
        {
            throw new BaseException("PAVO terminal API anahtari alinamadi.", 502);
        }

        return await AuthenticateAsync(terminalKey, cancellationToken);
    }

    private async Task<string> AuthenticateAsync(string apiKey, CancellationToken cancellationToken)
    {
        var root = await PostAsync(
            "/api/ApiAuthentication/Authenticate",
            new { _options.AppToken, ApiKey = apiKey },
            bearerToken: null,
            cancellationToken);
        var token = GetString(root, "AccessToken");
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new BaseException(GetString(root, "Result") ?? "PAVO kimlik dogrulamasi basarisiz.", 502);
        }

        return token;
    }

    private async Task<JsonNode> PostAsync(
        string path,
        object payload,
        string? bearerToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        return await SendAsync(request, cancellationToken);
    }

    private async Task<JsonNode> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        JsonNode? root = null;
        try
        {
            root = JsonNode.Parse(content);
        }
        catch (JsonException)
        {
            // HTTP hata metni asagida guvenli ve sinirli olarak raporlanir.
        }

        if (!response.IsSuccessStatusCode)
        {
            var message = GetString(root, "Message")
                ?? GetString(root, "title")
                ?? $"PAVO servisi HTTP {(int)response.StatusCode} dondu.";
            throw new BaseException(message, 502);
        }

        if (root is null)
        {
            throw new BaseException("PAVO servisinden gecersiz yanit alindi.", 502);
        }

        var successNode = FindProperty(root, "Success");
        if (successNode is not null && bool.TryParse(successNode.ToString(), out var success) && !success)
        {
            throw new BaseException(GetString(root, "Message") ?? "PAVO islemi basarisiz.", 502);
        }

        return root;
    }

    private void EnsureConfigured()
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.AppToken) || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new BaseException("PAVO entegrasyonu sunucu konfigurasyonunda etkin degil.", 503);
        }
    }

    private static JsonNode GetData(JsonNode root) =>
        FindProperty(root, "Data") ?? throw new BaseException("PAVO yanitinda Data alani bulunamadi.", 502);

    private static JsonNode? FindProperty(JsonNode? node, string name)
    {
        if (node is not JsonObject obj)
        {
            return null;
        }

        return obj.FirstOrDefault(x => string.Equals(x.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
    }

    private static string? GetString(JsonNode? node, string name) => FindProperty(node, name)?.ToString();
    private static long GetLong(JsonNode? node, string name) => long.TryParse(GetString(node, name), out var value) ? value : 0;
    private static int GetInt(JsonNode? node, string name) => int.TryParse(GetString(node, name), out var value) ? value : 0;
    private static bool GetBool(JsonNode? node, string name) => bool.TryParse(GetString(node, name), out var value) && value;
}
