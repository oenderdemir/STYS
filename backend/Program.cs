using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;
using TOD.Platform.AspNetCore;
using TOD.Platform.AspNetCore.Filters;
using TOD.Platform.Identity;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddTodPlatformDefaults();
builder.Services.AddTodPlatformIdentity(
    options => options.UseSqlServer(builder.Configuration.GetConnectionString("TodIdentityDbConnection")),
    builder.Configuration);

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key is required.");
var jwtIssuer = jwtSection["Issuer"];
var jwtAudience = jwtSection["Audience"];
var validateIssuer = !string.IsNullOrWhiteSpace(jwtIssuer);
var validateAudience = !string.IsNullOrWhiteSpace(jwtAudience);

void ConfigureJwt(JwtBearerOptions options)
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        ValidateIssuer = validateIssuer,
        ValidateAudience = validateAudience,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience
    };
}

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, ConfigureJwt)
    .AddJwtBearer("UIScheme", ConfigureJwt)
    .AddJwtBearer("ServiceScheme", ConfigureJwt);

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("UIPolicy", policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("permission", "KullaniciTipi.Admin", "KullaniciTipi.UIUser")
        .AddAuthenticationSchemes("UIScheme"));

    options.AddPolicy("ServicePolicy", policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("permission", "KullaniciTipi.Admin", "KullaniciTipi.ServiceUser")
        .AddAuthenticationSchemes("ServiceScheme"));
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
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();

    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
