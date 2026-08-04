using System.Collections.Immutable;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace STYS.Muhasebe.SatisBelgeleri;

/// <summary>
/// <see cref="IEBelgeSchematronValidator"/>'ın gerçek HTTP implementasyonu - ayrı Java Saxon-HE
/// 13.0 sidecar servisine (POST /internal/schematron/validate) bağlanır. Typed HttpClient
/// kullanır (DI'da AddHttpClient ile kaydedilir - bkz. Program.cs), kısa/kontrollü timeout
/// (client.Timeout, DI'da yapılandırılır) ve response boyutu sınırı (MaxResponseContentBufferSize)
/// uygular. XML İÇERİĞİ HİÇBİR YERDE LOGLANMAZ. Geçici bağlantı hatası ile GERÇEK schematron
/// ihlali kesin biçimde AYRILIR - biri exception (altyapı), diğeri normal dönüş değeridir
/// (Valid=false, iş kuralı sonucu) (bkz. görev md.7, md.8).
/// </summary>
public sealed class SaxonSidecarEBelgeSchematronValidator : IEBelgeSchematronValidator
{
    private const string ValidatePath = "/internal/schematron/validate";
    private static readonly MediaTypeHeaderValue XmlContentType = new("application/xml");

    private readonly HttpClient _httpClient;

    public SaxonSidecarEBelgeSchematronValidator(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<EBelgeSchematronValidationResult> ValidateAsync(
        ImmutableArray<byte> xmlBytes,
        string ruleSetId,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ValidatePath);
        request.Headers.Add("X-RuleSet-Id", ruleSetId);
        request.Headers.Add("X-Correlation-Id", Guid.NewGuid().ToString());
        request.Content = new ByteArrayContent(xmlBytes.ToArray());
        request.Content.Headers.ContentType = XmlContentType;

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new EBelgeUblSchematronServiceUnavailableException("Schematron sidecar'a bağlantı zaman aşımına uğradı.");
        }
        catch (HttpRequestException ex)
        {
            throw new EBelgeUblSchematronServiceUnavailableException($"Schematron sidecar'a bağlanılamadı: {ex.GetType().Name}");
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                throw new EBelgeUblSchematronServiceUnavailableException("Schematron sidecar henüz hazır değil.");
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                throw new EBelgeUblRuleSetArtifactInvalidException($"Sidecar bilinmeyen/desteklenmeyen rule-set kimliğini reddetti: {ruleSetId}");
            }

            if (response.StatusCode == HttpStatusCode.GatewayTimeout || response.StatusCode == HttpStatusCode.RequestTimeout)
            {
                throw new EBelgeUblSchematronServiceUnavailableException("Schematron sidecar doğrulama zaman aşımına uğradı.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new EBelgeUblSchematronProtocolErrorException($"Schematron sidecar beklenmeyen durum kodu döndürdü: {(int)response.StatusCode}");
            }

            var contentLength = response.Content.Headers.ContentLength;
            if (contentLength.HasValue && contentLength.Value > EBelgeSchematronSidecarOptions.MaxResponseBytes)
            {
                throw new EBelgeUblSchematronProtocolErrorException("Schematron sidecar yanıtı izin verilen boyutu aşıyor.");
            }

            string json;
            try
            {
                json = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                throw new EBelgeUblSchematronProtocolErrorException($"Schematron sidecar yanıtı okunamadı: {ex.GetType().Name}");
            }

            if (json.Length > EBelgeSchematronSidecarOptions.MaxResponseBytes)
            {
                throw new EBelgeUblSchematronProtocolErrorException("Schematron sidecar yanıtı izin verilen boyutu aşıyor.");
            }

            SidecarResponseDto? dto;
            try
            {
                dto = JsonSerializer.Deserialize<SidecarResponseDto>(json, JsonOptions);
            }
            catch (JsonException ex)
            {
                throw new EBelgeUblSchematronProtocolErrorException($"Schematron sidecar yanıtı çözümlenemedi: {ex.GetType().Name}");
            }

            if (dto is null || dto.Violations is null)
            {
                throw new EBelgeUblSchematronProtocolErrorException("Schematron sidecar yanıtı beklenen protokole uymuyor.");
            }

            var violations = dto.Violations
                .Select(v => new EBelgeSchematronViolation(v.RuleId ?? string.Empty, v.Location ?? string.Empty, v.Message ?? string.Empty, v.Severity ?? "error"))
                .ToImmutableArray();

            return new EBelgeSchematronValidationResult(dto.Valid, violations);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class SidecarResponseDto
    {
        [JsonPropertyName("valid")]
        public bool Valid { get; set; }

        [JsonPropertyName("violations")]
        public List<SidecarViolationDto>? Violations { get; set; }
    }

    private sealed class SidecarViolationDto
    {
        [JsonPropertyName("ruleId")]
        public string? RuleId { get; set; }

        [JsonPropertyName("location")]
        public string? Location { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("severity")]
        public string? Severity { get; set; }
    }
}
