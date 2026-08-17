using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using STYS.Agent.Client.Authentication;
using STYS.Agent.Client.Infrastructure;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Client;

namespace STYS.Tests.Agent;

public sealed class AgentAuthenticationHandlerTests
{
    [Fact]
    public async Task UnauthorizedResponse_AuthStateAndRuntimeResetilir()
    {
        var tokenStore = new AgentTokenStore();
        tokenStore.SetToken(new AgentTokenResponse
        {
            AccessToken = "jwt",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        });

        var recoverySink = new FakeUnauthorizedRecoverySink();
        recoverySink.Authenticated = true;

        var handler = new AgentAuthenticationHandler(
            tokenStore,
            recoverySink,
            Options.Create(new StysAgentClientOptions
            {
                BaseUrl = "https://stys.test",
                ClientId = string.Empty,
                ClientSecret = string.Empty,
                AgentInstanceId = "agent-1",
                RequestTimeoutSeconds = 30
            }),
            NullLogger<AgentAuthenticationHandler>.Instance)
        {
            InnerHandler = new UnauthorizedOnceHandler()
        };

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://stys.test")
        };

        var response = await client.GetAsync("/api/agent/heartbeat");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(recoverySink.Authenticated);
        Assert.False(tokenStore.HasValidToken());
        Assert.Null(tokenStore.GetToken());
    }

    private sealed class UnauthorizedOnceHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
    }

    private sealed class FakeUnauthorizedRecoverySink : IAgentUnauthorizedRecoverySink
    {
        public bool Authenticated { get; set; }

        public void HandleAuthenticationLost()
        {
            Authenticated = false;
        }
    }
}
