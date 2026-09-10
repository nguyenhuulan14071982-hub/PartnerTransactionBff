using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using PartnerTransactionBff.Contracts;
using Polly;
using Xunit;

namespace PartnerTransactionBff.UnitTests;

public sealed class RetryPolicyTests
{
    [Fact]
    public async Task Http_500_is_retried_and_eventually_succeeds()
    {
        var attempts = 0;
        var services = new ServiceCollection();
        services.AddHttpClient("test", client => client.BaseAddress = new Uri("http://test"))
            .ConfigurePrimaryHttpMessageHandler(() => new CountingHandler(() =>
            {
                attempts++;
                return attempts < 3 ? new HttpResponseMessage(HttpStatusCode.InternalServerError) : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new PartnerVerificationResponse(true, "P-1001", "Contoso Partner", "GOLD")) };
            }))
            .AddResilienceHandler("retry", pipeline => pipeline.AddRetry(new HttpRetryStrategyOptions { MaxRetryAttempts = 3, Delay = TimeSpan.FromMilliseconds(1) }));
        using var provider = services.BuildServiceProvider();
        var response = await provider.GetRequiredService<IHttpClientFactory>().CreateClient("test").GetAsync("/verify");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal(3, attempts);
    }

    private sealed class CountingHandler(Func<HttpResponseMessage> factory) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(factory()); }
}
