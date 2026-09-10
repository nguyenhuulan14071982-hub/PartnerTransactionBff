using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;
using PartnerTransactionBff.Application;
using PartnerTransactionBff.Configuration;
using PartnerTransactionBff.Domain;
using PartnerTransactionBff.Infrastructure;
using PartnerTransactionBff.Middleware;
using PartnerTransactionBff.Services;
using Polly;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddOptions<RabbitMqOptions>()
    .BindConfiguration(RabbitMqOptions.SectionName)
    .Validate(options => !string.IsNullOrWhiteSpace(options.Host), "RabbitMq:Host is required.")
    .Validate(options => options.Port is > 0 and <= 65535, "RabbitMq:Port is invalid.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.QueueName), "RabbitMq:QueueName is required.")
    .ValidateOnStart();

builder.Services.AddOptions<PartnerVerificationOptions>()
    .BindConfiguration(PartnerVerificationOptions.SectionName)
    .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _), "PartnerVerification:BaseUrl must be an absolute URI.")
    .ValidateOnStart();

builder.Services.AddOptions<SecurityOptions>()
    .BindConfiguration(SecurityOptions.SectionName)
    .Validate(options => !options.RequireApiKey || !string.IsNullOrWhiteSpace(options.ApiKey), "Security:ApiKey is required when RequireApiKey is true.")
    .ValidateOnStart();

builder.Services.AddOptions<IdempotencyOptions>()
    .BindConfiguration(IdempotencyOptions.SectionName)
    .Validate(options => options.EntryLifetime > TimeSpan.Zero, "Idempotency:EntryLifetime must be greater than zero.")
    .ValidateOnStart();

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
builder.Services.AddSingleton<ITransactionValidator, TransactionValidator>();
builder.Services.AddScoped<ITransactionService, TransactionService>();

builder.Services.AddHttpClient<IPartnerVerificationClient, PartnerVerificationClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<PartnerVerificationOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(2);
})
.AddResilienceHandler("partner-verification", pipeline =>
{
    pipeline.AddRetry(new HttpRetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromMilliseconds(150),
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true
    });
});

builder.Services.AddSingleton<IRabbitMqConnectionProvider, RabbitMqConnectionProvider>();
builder.Services.AddSingleton<ITransactionQueue, RabbitMqTransactionQueue>();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("partner-transactions", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddHealthChecks()
    .AddCheck<RabbitMqHealthCheck>("rabbitmq", failureStatus: HealthStatus.Unhealthy, tags: ["ready"]);

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<PartnerTransactionBff.Middleware.ApiKeyMiddleware>();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

await app.RunAsync();

