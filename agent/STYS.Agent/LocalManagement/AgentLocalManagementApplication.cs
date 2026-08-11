using STYS.Agent.Configuration;
using STYS.Agent.Client;
using STYS.Agent.Client.Authentication;
using STYS.Agent.LocalDevices;
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
        var localDeviceApi = app.MapGroup("/api/local-devices");

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

        app.MapGet("/api/agent/me", async (
            IStysAgentApiClient client,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var self = await client.GetMeAsync(cancellationToken);
                return Results.Ok(self);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (AgentApiException ex)
            {
                return Results.Json(new { message = ex.Message, traceId = ex.TraceId }, statusCode: (int)ex.StatusCode);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

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

        localDeviceApi.MapGet("/", async (
            ILocalDeviceManagementService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAllAsync(cancellationToken)));

        localDeviceApi.MapGet("/{id}", async (
            string id,
            ILocalDeviceManagementService service,
            CancellationToken cancellationToken) =>
        {
            var device = await service.GetByIdAsync(id, cancellationToken);
            return device is null ? Results.NotFound(new { message = "Local cihaz bulunamadı." }) : Results.Ok(device);
        });

        localDeviceApi.MapPost("/", async (
            LocalDeviceUpsertRequest request,
            ILocalDeviceManagementService service,
            CancellationToken cancellationToken) =>
            await ExecuteDeviceSaveAsync(request, null, service, cancellationToken));

        localDeviceApi.MapPut("/{id}", async (
            string id,
            LocalDeviceUpsertRequest request,
            ILocalDeviceManagementService service,
            CancellationToken cancellationToken) =>
        {
            request.Id = id;
            return await ExecuteDeviceSaveAsync(request, id, service, cancellationToken);
        });

        localDeviceApi.MapPost("/test-connection", async (
            LocalDeviceTestRequest request,
            ILocalDeviceManagementService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await service.TestAsync(request, cancellationToken);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        localDeviceApi.MapPost("/{id}/test-connection", async (
            string id,
            ILocalDeviceManagementService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await service.TestAsync(id, cancellationToken);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        localDeviceApi.MapPost("/{id}/device-info", async (
            string id,
            ILocalDeviceManagementService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var device = await service.GetDeviceInfoAsync(id, cancellationToken);
                return Results.Ok(device);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        localDeviceApi.MapGet("/{id}/terminals", async (
            string id,
            ILocalDeviceManagementService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var terminals = await service.GetTerminalsAsync(id, cancellationToken);
                return Results.Ok(terminals);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        localDeviceApi.MapPost("/{id}/terminals/discover", async (
            string id,
            ILocalDeviceManagementService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var terminals = await service.DiscoverTerminalsAsync(id, cancellationToken);
                return Results.Ok(terminals);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        localDeviceApi.MapGet("/{id}/provisioning-candidate", async (
            string id,
            int tesisId,
            ILocalDeviceManagementService service,
            IStysAgentApiClient client,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var self = await client.GetMeAsync(cancellationToken);
                var candidate = await service.BuildProvisioningCandidateAsync(id, tesisId, self, cancellationToken);
                return Results.Ok(candidate);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (AgentApiException ex)
            {
                return Results.Json(new { message = ex.Message, traceId = ex.TraceId }, statusCode: (int)ex.StatusCode);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        localDeviceApi.MapPost("/{id}/pairing", async (
            string id,
            LocalDevicePairingRequest request,
            ILocalDeviceManagementService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var device = await service.PairAsync(id, request.ForceRePair, cancellationToken);
                return Results.Ok(device);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        localDeviceApi.MapDelete("/{id}", async (
            string id,
            ILocalDeviceManagementService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await service.DeleteAsync(id, cancellationToken);
                return Results.NoContent();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        return app;
    }

    private static async Task<IResult> ExecuteDeviceSaveAsync(
        LocalDeviceUpsertRequest request,
        string? id,
        ILocalDeviceManagementService service,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                request.Id = id;
            }

            var saved = await service.SaveAsync(request, cancellationToken);
            return Results.Ok(saved);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static string NormalizeBaseUrl(string? baseUrl)
    {
        var value = string.IsNullOrWhiteSpace(baseUrl) ? "https://localhost:7160" : baseUrl.Trim();
        return value.TrimEnd('/');
    }
}
