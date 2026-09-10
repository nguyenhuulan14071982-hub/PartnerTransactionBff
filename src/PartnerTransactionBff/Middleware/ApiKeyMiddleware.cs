using Microsoft.Extensions.Options;
using PartnerTransactionBff.Configuration;

namespace PartnerTransactionBff.Middleware;

public sealed class ApiKeyMiddleware(
    RequestDelegate next,
    IOptions<SecurityOptions> options)
{
    private const string HeaderName = "X-API-Key";

    public async Task InvokeAsync(HttpContext context)
    {
        var settings = options.Value;
        if (!settings.RequireApiKey ||
            !context.Request.Path.StartsWithSegments("/api/v1/partner/transactions"))
        {
            await next(context);
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.ApiKey) ||
            !context.Request.Headers.TryGetValue(HeaderName, out var provided) ||
            !string.Equals(provided.ToString(), settings.ApiKey, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "unauthorized",
                message = "A valid X-API-Key header is required."
            });
            return;
        }

        await next(context);
    }
}
