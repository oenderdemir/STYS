using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using STYS.AccessScope;
using STYS.Bildirimler.Hubs;
using STYS.Bildirimler.Services;
using STYS.Countries.Mapping;
using STYS.ErisimTeshis.Services;
using STYS.Infrastructure.EntityFramework;
using STYS.Kamp.Services;
using STYS.Kullanicilar.Services;
using STYS.OdaTemizlik.Services;
using STYS.Rezervasyonlar.Repositories;
using STYS.Rezervasyonlar.Services;
using STYS.YoneticiAdaylari.Services;
using TOD.Platform.AspNetCore;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Filters;
using TOD.Platform.AspNetCore.Logging;
using TOD.Platform.AspNetCore.RateLimiting;
using TOD.Platform.Identity;
using TOD.Platform.Identity.Infrastructure.EntityFramework;
using TOD.Platform.Identity.Users.Services;
using TOD.Platform.Persistence.Rdbms.Extensions;

var builder = WebApplication.CreateBuilder(args);
SerilogHooks.Configure(builder.Configuration["Serilog:ArchiveDirectoryFormat"]);
const string frontendCorsPolicy = "FrontendDevCors";

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();
});

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("TodIdentityDbConnection")
    ?? throw new InvalidOperationException("Connection string 'TodIdentityDbConnection' is required.");

builder.Services.AddTodPlatformDefaults();
builder.Services.AddTodPlatformIdentity(
    options => options.UseSqlServer(connectionString),
    builder.Configuration);

var mapperConfig = new MapperConfiguration(cfg =>
{
    cfg.AddMaps(typeof(TOD.Platform.Identity.DependencyInjection).Assembly);
    cfg.AddMaps(typeof(CountryProfile).Assembly);
}, NullLoggerFactory.Instance);
builder.Services.AddSingleton(mapperConfig);
builder.Services.AddScoped<IMapper>(sp => sp.GetRequiredService<MapperConfiguration>().CreateMapper(sp.GetService));

builder.Services.AddDbContext<StysAppDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddBaseRdbmsServicesAndRepositoriesScoped(typeof(Program).Assembly);
builder.Services.AddScoped<IUserService, StysScopedUserService>();
builder.Services.AddScoped<IAccessScopeProvider, AccessScopeProvider>();
builder.Services.AddScoped<IUserAccessScopeService, UserAccessScopeService>();
builder.Services.AddScoped<IYoneticiAdayService, YoneticiAdayService>();
builder.Services.AddScoped<IOdaTemizlikService, OdaTemizlikService>();
builder.Services.AddScoped<IErisimTeshisService, ErisimTeshisService>();
builder.Services.AddScoped<IKampParametreService, KampParametreService>();
builder.Services.AddScoped<IKampPuanlamaService, KampPuanlamaService>();
builder.Services.AddScoped<IKampPuanKuraliYonetimService, KampPuanKuraliYonetimService>();
builder.Services.AddScoped<IKampUcretHesaplamaService, KampUcretHesaplamaService>();
builder.Services.AddScoped<IKampIadeService, KampIadeService>();
builder.Services.AddScoped<IKampBasvuruService, KampBasvuruService>();
builder.Services.AddScoped<IKampTahsisService, KampTahsisService>();
builder.Services.AddScoped<IKampRezervasyonService, KampRezervasyonService>();
builder.Services.AddScoped<IRezervasyonRepository, RezervasyonRepository>();
builder.Services.AddScoped<IRezervasyonService, RezervasyonService>();
builder.Services.AddScoped<IBildirimService, BildirimService>();
builder.Services.AddSignalR();

builder.Services.AddTodPlatformJwtAuthentication(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddTodPlatformAuthorization();
builder.Services.AddTodPlatformRateLimiting(builder.Configuration);
builder.Services.AddCors(options =>
{
    options.AddPolicy(frontendCorsPolicy, policyBuilder =>
    {
        policyBuilder
            .WithOrigins(
                "http://localhost:4200",
                "https://localhost:4200",
                "http://localhost:4201",
                "https://localhost:4201")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "STYS API", Version = "v1" });
    options.OperationFilter<FileUploadOperationFilter>();

    var bearerScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT token'i: Bearer {token}"
    };

    options.AddSecurityDefinition("Bearer", bearerScheme);

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document, null)] = new List<string>()
    });
});

var app = builder.Build();

if (app.Configuration.GetValue<bool>("RunDatabaseMigrationsOnStartup"))
{
    await StartupMigrationRunner.ApplyAsync(app.Services);
}

// Configure the HTTP request pipeline.
var enableSwagger = app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("EnableSwagger");
if (enableSwagger)
{
    app.UseSwagger(options =>
    {
        options.PreSerializeFilters.Add((swagger, request) =>
        {
            swagger.Servers =
            [
                new OpenApiServer
                {
                    Url = $"{request.Scheme}://{request.Host}/api"
                }
            ];
        });
    });
    app.UseSwaggerUI();
}

app.UseTodPlatformDefaults();
app.UseRequestResponseLogging();
app.UseJwtTokenLogging();
app.UseSecurityHeaders();
app.UseHttpsRedirection();
app.UseCors(frontendCorsPolicy);
app.UseTodPlatformRateLimiting();
app.UseAuthentication();
app.UseAuthorization();
app.MapTodPlatformHealthChecks();
app.MapControllers();
app.MapHub<BildirimHub>(BildirimHub.HubRoute)
    .RequireAuthorization(TodPlatformAuthorizationConstants.UiPolicy);



app.Run();

internal static class StartupMigrationRunner
{
    public static async Task ApplyAsync(IServiceProvider rootServices)
    {
        await using var scope = rootServices.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>().CreateLogger("StartupMigration");

        await MigrateWithRetryAsync<TodIdentityDbContext>(scope.ServiceProvider, logger);
        await MigrateWithRetryAsync<StysAppDbContext>(scope.ServiceProvider, logger);
    }

    private static async Task MigrateWithRetryAsync<TContext>(IServiceProvider services, Microsoft.Extensions.Logging.ILogger logger)
        where TContext : DbContext
    {
        const int maxAttempts = 12;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var dbContext = services.GetRequiredService<TContext>();
                await dbContext.Database.MigrateAsync();
                logger.LogInformation("Database migrations applied for {DbContext}.", typeof(TContext).Name);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(5 * attempt, 30));
                logger.LogWarning(ex, "Migration attempt {Attempt}/{MaxAttempts} failed for {DbContext}. Retrying in {DelaySeconds}s.", attempt, maxAttempts, typeof(TContext).Name, delay.TotalSeconds);
                await Task.Delay(delay);
            }
        }

        var finalContext = services.GetRequiredService<TContext>();
        await finalContext.Database.MigrateAsync();
    }
}
