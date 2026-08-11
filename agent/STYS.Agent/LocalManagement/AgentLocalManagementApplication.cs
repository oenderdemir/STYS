using STYS.Agent.Configuration;

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
            CancellationToken cancellationToken) =>
        {
            try
            {
                var saved = await service.SaveConfigurationAsync(request, cancellationToken);
                return Results.Ok(saved);
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

        bootstrapApi.MapGet("/dashboard", async (
            IAgentBootstrapManagementService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.GetDashboardAsync(cancellationToken)));

        return app;
    }
}
