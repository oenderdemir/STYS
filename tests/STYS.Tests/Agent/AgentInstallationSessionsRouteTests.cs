using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using STYS.Agent.Controllers;
using Xunit;

namespace STYS.Tests.Agent;

/// <summary>
/// The frontend calls /api/ui/agent-installations; the reverse proxy (and proxy.conf.json's
/// pathRewrite in dev) strips one leading /api, so the registered route must be exactly
/// "ui/agent-installations". A stray "api/" segment here produces a 404 that only reproduces
/// through the proxy, so pin the template.
/// </summary>
public sealed class AgentInstallationSessionsRouteTests
{
    private static RouteAttribute[] RouteAttributes(bool inherit) =>
        typeof(AgentInstallationSessionsController)
            .GetCustomAttributes(typeof(RouteAttribute), inherit)
            .Cast<RouteAttribute>()
            .ToArray();

    [Fact]
    public void Route_TamOlarakUiAgentInstallations()
    {
        var declared = RouteAttributes(inherit: false);

        var route = Assert.Single(declared);
        Assert.Equal("ui/agent-installations", route.Template);
    }

    [Fact]
    public void Route_ApiOnEkiIcermez()
    {
        // The proxy already supplies /api; a second one would resolve to /api/api/... and 404.
        foreach (var route in RouteAttributes(inherit: true))
        {
            Assert.DoesNotContain("api/", route.Template, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void TokenRoutuTekBasinaYetmez_ExplicitRouteZorunlu()
    {
        // RouteAttribute is Inherited = true / AllowMultiple = true, so inherit:true surfaces both
        // the declared template and UIController's "ui/[controller]". The inherited one expands to
        // ui/AgentInstallationSessions — the [controller] token cannot produce the hyphenated path
        // the frontend calls, which is exactly why the explicit [Route] above must stay.
        var inherited = RouteAttributes(inherit: true).Select(x => x.Template).ToArray();

        Assert.Contains("ui/agent-installations", inherited);
        Assert.Contains("ui/[controller]", inherited);
        Assert.DoesNotContain("agent-installations", nameof(AgentInstallationSessionsController));
    }
}
