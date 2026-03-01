using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using STYS.AccessScope;
using STYS.Countries.Mapping;
using STYS.Infrastructure.EntityFramework;
using TOD.Platform.AspNetCore;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Filters;
using TOD.Platform.AspNetCore.Logging;
using TOD.Platform.AspNetCore.RateLimiting;
using TOD.Platform.Identity;
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
builder.Services.AddScoped<IUserAccessScopeService, UserAccessScopeService>();

builder.Services.AddTodPlatformJwtAuthentication(builder.Configuration);
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
            .AllowAnyMethod();
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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
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



app.Run();
