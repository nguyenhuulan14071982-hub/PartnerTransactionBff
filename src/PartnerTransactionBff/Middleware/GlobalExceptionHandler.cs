using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using PartnerTransactionBff.Contracts;
using RabbitMQ.Client.Exceptions;

namespace PartnerTransactionBff.Middleware;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var statusCode = exception switch
        {
            TimeoutException or HttpRequestException or BrokerUnreachableException or PublishException => (int)HttpStatusCode.ServiceUnavailable,
            InvalidOperationException when exception.Message.Contains("RabbitMQ", StringComparison.OrdinalIgnoreCase)
                => (int)HttpStatusCode.ServiceUnavailable,
            _ => (int)HttpStatusCode.InternalServerError
        };

        logger.LogError(
            exception,
            "Request failed. StatusCode: {StatusCode}, TraceId: {TraceId}",
            statusCode,
            httpContext.TraceIdentifier);

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json";

        var error = new ApiError(
            statusCode == StatusCodes.Status503ServiceUnavailable
                ? "dependency_unavailable"
                : "request_failed",
            statusCode == StatusCodes.Status503ServiceUnavailable
                ? "A required dependency is temporarily unavailable."
                : "The request could not be completed.",
            httpContext.TraceIdentifier);

        await httpContext.Response.WriteAsync(
            JsonSerializer.Serialize(error),
            cancellationToken);

        return true;
    }
}
