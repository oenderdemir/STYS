// Aliased reference: STYS.Agent.Updater and STYS.Agent both declare
// STYS.Agent.Options.AgentUpgradeOptions, so the updater's types are reached through an alias.
extern alias updater;

using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using AgentHealthProbe = updater::STYS.Agent.Updater.Services.AgentHealthProbe;

namespace STYS.Tests.Agent;

/// <summary>
/// Exercises the probe over a stubbed HTTP response rather than only its version helper: the gate
/// that decides whether an upgrade is kept or rolled back is the JSON handling here, and a version
/// field that is absent or blank must not be read as success.
/// </summary>
public sealed class AgentHealthProbeTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
    }

    private static async Task<bool> ProbeAsync(string body, string targetVersion = "1.0.1", HttpStatusCode status = HttpStatusCode.OK)
    {
        using var client = new HttpClient(new StubHandler(status, body));
        var probe = new AgentHealthProbe(client, NullLogger<AgentHealthProbe>.Instance);

        // A single short window: these cases are deterministic, so no need to burn retry time.
        return await probe.WaitForHealthyAsync(5180, targetVersion, TimeSpan.FromMilliseconds(200), CancellationToken.None);
    }

    [Fact] // A
    public async Task TamEslesenSurum_Saglikli()
    {
        Assert.True(await ProbeAsync("""{"startupHealthy":true,"agentVersion":"1.0.1"}"""));
    }

    [Fact] // B
    public async Task BuildMetadataliSurum_Saglikli()
    {
        // The SDK appends the commit sha in a git build; this must not read as a different binary.
        Assert.True(await ProbeAsync("""{"startupHealthy":true,"agentVersion":"1.0.1+abc"}"""));
    }

    [Fact] // C
    public async Task YanlisSurum_Saglikliz()
    {
        Assert.False(await ProbeAsync("""{"startupHealthy":true,"agentVersion":"1.0.2"}"""));
    }

    [Fact] // D
    public async Task SurumAlaniYok_Saglikliz()
    {
        // Fail closed: no version reported is not evidence the new build is running.
        Assert.False(await ProbeAsync("""{"startupHealthy":true}"""));
    }

    [Theory] // E
    [InlineData("\"\"")]
    [InlineData("\"   \"")]
    [InlineData("null")]
    public async Task SurumBos_Saglikliz(string versionJson)
    {
        Assert.False(await ProbeAsync($$"""{"startupHealthy":true,"agentVersion":{{versionJson}}}"""));
    }

    [Fact] // F
    public async Task StartupSaglikliDegil_Saglikliz()
    {
        Assert.False(await ProbeAsync("""{"startupHealthy":false,"agentVersion":"1.0.1"}"""));
    }

    [Fact]
    public async Task BasarisizHttpYaniti_Saglikliz()
    {
        Assert.False(await ProbeAsync(
            """{"startupHealthy":true,"agentVersion":"1.0.1"}""",
            status: HttpStatusCode.ServiceUnavailable));
    }

    [Fact]
    public async Task PrereleaseFarki_Saglikliz()
    {
        Assert.False(await ProbeAsync("""{"startupHealthy":true,"agentVersion":"1.0.1"}""", targetVersion: "1.0.1-beta.1"));
    }
}
