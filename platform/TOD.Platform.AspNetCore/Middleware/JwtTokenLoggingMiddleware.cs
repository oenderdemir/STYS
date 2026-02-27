using Microsoft.AspNetCore.Http;
using System.IdentityModel.Tokens.Jwt;
using Serilog.Context;

namespace TOD.Platform.AspNetCore.Middleware;

public class JwtTokenLoggingMiddleware
{
    private readonly RequestDelegate _next;

    public JwtTokenLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        var token = context.Request.Headers.Authorization.FirstOrDefault()?.Split(' ').Last();

        if (string.IsNullOrWhiteSpace(token))
        {
            await _next(context);
            return;
        }

        var tokenHandler = new JwtSecurityTokenHandler();

        if (!tokenHandler.CanReadToken(token))
        {
            await _next(context);
            return;
        }

        var jwtToken = tokenHandler.ReadJwtToken(token);
        var userName = jwtToken.Claims.FirstOrDefault(claim => claim.Type == "userName")?.Value;

        using (LogContext.PushProperty("userName", userName ?? "unknown"))
        {
            await _next(context);
        }
    }
}
