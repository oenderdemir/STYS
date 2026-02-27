using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Serilog;
using TOD.Platform.AspNetCore;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Filters;
using TOD.Platform.AspNetCore.Logging;
using TOD.Platform.AspNetCore.RateLimiting;
using TOD.Platform.Identity;

var builder = WebApplication.CreateBuilder(args);
SerilogHooks.Configure(builder.Configuration["Serilog:ArchiveDirectoryFormat"]);

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();
});

// Add services to the container.
builder.Services.AddTodPlatformDefaults();
builder.Services.AddTodPlatformIdentity(
    options => options.UseSqlServer(builder.Configuration.GetConnectionString("TodIdentityDbConnection")),
    builder.Configuration);

builder.Services.AddTodPlatformJwtAuthentication(builder.Configuration);
builder.Services.AddTodPlatformAuthorization();
builder.Services.AddTodPlatformRateLimiting(builder.Configuration);

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
app.UseTodPlatformRateLimiting();
app.UseAuthentication();
app.UseAuthorization();
app.MapTodPlatformHealthChecks();
app.MapControllers();



app.Run();
