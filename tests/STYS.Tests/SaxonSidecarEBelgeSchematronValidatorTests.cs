using System.Collections.Immutable;
using System.Net;
using System.Text;
using STYS.Muhasebe.SatisBelgeleri;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// .NET sidecar HTTP client için sahte (fake) bir HttpMessageHandler kullanır - GERÇEK bir
/// schematron doğrulaması test edilmez (bu, EBelgeSchematronSidecarIntegrationTests'te gerçek
/// sidecar ile yapılır); burada yalnız HTTP hata sınıflandırması izole test edilir (bkz. görev
/// md.14 - "Mock testler yalnız .NET HTTP client hata eşlemesi için kullanılabilir").
/// </summary>
public class SaxonSidecarEBelgeSchematronValidatorTests
{
    private static readonly ImmutableArray<byte> OrnekXml = ImmutableArray.Create(Encoding.UTF8.GetBytes("<x/>"));

    private sealed class SahteHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _yanit;

        public SahteHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> yanit)
        {
            _yanit = yanit;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _yanit(request, cancellationToken);
    }

    private static SaxonSidecarEBelgeSchematronValidator CreateValidator(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> yanit,
        TimeSpan? timeout = null)
    {
        var handler = new SahteHandler(yanit);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://sidecar-test"),
            Timeout = timeout ?? TimeSpan.FromSeconds(5),
        };
        return new SaxonSidecarEBelgeSchematronValidator(httpClient);
    }

    // 21. Başarılı response doğrulama sonucu üretir.
    [Fact]
    public async Task BasariliResponseDogrulamaSonucuUretir()
    {
        var validator = CreateValidator((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"valid\":true,\"violations\":[]}", Encoding.UTF8, "application/json"),
        }));

        var sonuc = await validator.ValidateAsync(OrnekXml, "GIB-UBL-TR-1.2.1/2026-09-14", CancellationToken.None);

        Assert.True(sonuc.Valid);
        Assert.Empty(sonuc.Violations);
    }

    [Fact]
    public async Task IhlalliResponseValidFalseDoner()
    {
        var validator = CreateValidator((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"valid\":false,\"violations\":[{\"ruleId\":\"r1\",\"location\":\"/Invoice\",\"message\":\"hata\",\"severity\":\"error\"}]}",
                Encoding.UTF8, "application/json"),
        }));

        var sonuc = await validator.ValidateAsync(OrnekXml, "GIB-UBL-TR-1.2.1/2026-09-14", CancellationToken.None);

        Assert.False(sonuc.Valid);
        Assert.Single(sonuc.Violations);
        Assert.Equal("r1", sonuc.Violations[0].RuleId);
    }

    // 23. Timeout 503 service unavailable olur.
    [Fact]
    public async Task TimeoutServiceUnavailableOlur()
    {
        var validator = CreateValidator(async (_, ct) =>
        {
            await Task.Delay(500, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }, timeout: TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<EBelgeUblSchematronServiceUnavailableException>(
            () => validator.ValidateAsync(OrnekXml, "GIB-UBL-TR-1.2.1/2026-09-14", CancellationToken.None));
    }

    // 24. Connection failure geçici hata olarak sınıflanır.
    [Fact]
    public async Task ConnectionFailureGeciciHataOlarakSiniflanir()
    {
        var validator = CreateValidator((_, _) => throw new HttpRequestException("bağlantı reddedildi"));

        await Assert.ThrowsAsync<EBelgeUblSchematronServiceUnavailableException>(
            () => validator.ValidateAsync(OrnekXml, "GIB-UBL-TR-1.2.1/2026-09-14", CancellationToken.None));
    }

    [Fact]
    public async Task SidecarServiceUnavailableDonerse503Siniflanir()
    {
        var validator = CreateValidator((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        await Assert.ThrowsAsync<EBelgeUblSchematronServiceUnavailableException>(
            () => validator.ValidateAsync(OrnekXml, "GIB-UBL-TR-1.2.1/2026-09-14", CancellationToken.None));
    }

    [Fact]
    public async Task BilinmeyenRuleSet400RuleSetArtifactInvalidSiniflanir()
    {
        var validator = CreateValidator((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)));

        await Assert.ThrowsAsync<EBelgeUblRuleSetArtifactInvalidException>(
            () => validator.ValidateAsync(OrnekXml, "GIB-UBL-TR-1.2.1/2026-09-14", CancellationToken.None));
    }

    // 25. Geçersiz JSON 502 protocol error olur.
    [Fact]
    public async Task GecersizJsonProtocolErrorOlur()
    {
        var validator = CreateValidator((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("bu-json-degil", Encoding.UTF8, "application/json"),
        }));

        await Assert.ThrowsAsync<EBelgeUblSchematronProtocolErrorException>(
            () => validator.ValidateAsync(OrnekXml, "GIB-UBL-TR-1.2.1/2026-09-14", CancellationToken.None));
    }

    [Fact]
    public async Task BeklenmeyenDurumKodu502ProtocolErrorOlur()
    {
        var validator = CreateValidator((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        await Assert.ThrowsAsync<EBelgeUblSchematronProtocolErrorException>(
            () => validator.ValidateAsync(OrnekXml, "GIB-UBL-TR-1.2.1/2026-09-14", CancellationToken.None));
    }

    // 26. Aşırı büyük response reddedilir.
    [Fact]
    public async Task AsiriBuyukResponseReddedilir()
    {
        var validator = CreateValidator((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"valid\":true,\"violations\":[]}", Encoding.UTF8, "application/json"),
            };
            response.Content.Headers.ContentLength = 2_000_000;
            return Task.FromResult(response);
        });

        await Assert.ThrowsAsync<EBelgeUblSchematronProtocolErrorException>(
            () => validator.ValidateAsync(OrnekXml, "GIB-UBL-TR-1.2.1/2026-09-14", CancellationToken.None));
    }

    // 27. Cancellation token uygulanır - kullanıcı iptali ServiceUnavailable'a ÇEVRİLMEZ, doğrudan yayılır.
    [Fact]
    public async Task CancellationTokenDogrudanYayilir()
    {
        using var cts = new CancellationTokenSource();
        var validator = CreateValidator(async (_, ct) =>
        {
            cts.Cancel();
            await Task.Delay(1000, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => validator.ValidateAsync(OrnekXml, "GIB-UBL-TR-1.2.1/2026-09-14", cts.Token));
    }
}
