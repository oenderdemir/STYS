using STYS.Agent.Configuration;
using STYS.Agent.Client.Authentication;
using STYS.Agent.Services;

namespace STYS.Agent.LocalManagement;

public static class AgentLocalManagementApplication
{
    public static WebApplication MapAgentLocalManagement(this WebApplication app)
    {
        var webRoot = app.Environment.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");

        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.MapGet("/", () => Results.File(Path.Combine(webRoot, "index.html"), "text/html"));
        app.MapGet("/setup", () => Results.File(Path.Combine(webRoot, "setup.html"), "text/html"));
        app.MapGet("/local-cihazlar", () => Results.File(Path.Combine(webRoot, "local-cihazlar.html"), "text/html"));
        app.MapGet("/entegrasyonlar", () => Results.File(Path.Combine(webRoot, "entegrasyonlar.html"), "text/html"));
        app.MapGet("/loglar", () => Results.File(Path.Combine(webRoot, "loglar.html"), "text/html"));

        var bootstrapApi = app.MapGroup("/api/bootstrap");

        bootstrapApi.MapGet("/config", async (
            IAgentBootstrapManagementService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.GetConfigurationAsync(cancellationToken)));

        bootstrapApi.MapPost("/config", async (
            AgentBootstrapConfiguration request,
            IAgentBootstrapManagementService service,
            IAgentBootstrapConfigurationStore store,
            IAgentCredentialStore credentialStore,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var current = await store.GetAsync(cancellationToken);
                var saved = await service.SaveConfigurationAsync(request, cancellationToken);
                var credential = await credentialStore.GetAsync(cancellationToken);
                var restartRequired = saved.RestartRequired;
                var reEnrollmentRequired = saved.ReEnrollmentRequired || (credential is not null && !string.Equals(NormalizeBaseUrl(current.StysBaseUrl), NormalizeBaseUrl(saved.Configuration.StysBaseUrl), StringComparison.OrdinalIgnoreCase));

                return Results.Ok(new
                {
                    configuration = saved.Configuration,
                    saved.Message,
                    restartRequired,
                    reEnrollmentRequired
                });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        bootstrapApi.MapPost("/test-connection", async (
            AgentBootstrapConfiguration request,
            IAgentBootstrapManagementService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await service.TestConnectionAsync(request, cancellationToken);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        bootstrapApi.MapPost("/enroll", async (
            AgentBootstrapEnrollmentRequest request,
            IAgentEnrollmentCoordinator coordinator,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await coordinator.EnrollAsync(request, cancellationToken);
                return result.Success
                    ? Results.Ok(result)
                    : Results.BadRequest(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (TOD.Platform.SharedKernel.Exceptions.BaseException ex)
            {
                return Results.BadRequest(new { message = ex.Message, errorCode = ex.ErrorCode });
            }
        });

        bootstrapApi.MapGet("/dashboard", async (
            IAgentBootstrapManagementService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.GetDashboardAsync(cancellationToken)));

        bootstrapApi.MapGet("/diagnostics", async (
            IAgentBootstrapManagementService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.GetDiagnosticsAsync(cancellationToken)));

        bootstrapApi.MapPost("/reset", async (
            AgentBootstrapResetRequest request,
            IAgentBootstrapManagementService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await service.ResetEnrollmentAsync(request, cancellationToken);
                return result.Success ? Results.Ok(result) : Results.BadRequest(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        return app;
    }

    private static string NormalizeBaseUrl(string? baseUrl)
    {
        var value = string.IsNullOrWhiteSpace(baseUrl) ? "https://localhost:7160" : baseUrl.Trim();
        return value.TrimEnd('/');
    }
}
