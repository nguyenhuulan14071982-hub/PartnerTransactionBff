using System.Net;
using System.Net.Http.Json;
using PartnerTransactionBff.Contracts;
using PartnerTransactionBff.Services;
using Polly;
using Xunit;


namespace PartnerTransactionBff.UnitTests;

public sealed class PartnerVerificationClientTests
{
    [Fact]
    public async Task Not_found_returns_null()
    {
        var client = new PartnerVerificationClient(CreateClient(new HttpResponseMessage(HttpStatusCode.NotFound)));

        var result = await client.VerifyAsync("P-404", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Successful_response_returns_external_partner_data()
    {
        var expected = new PartnerVerificationResponse(true, "P-1001", "Contoso", "GOLD");
        var client = new PartnerVerificationClient(CreateClient(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(expected)
        }));

        var result = await client.VerifyAsync("P-1001", CancellationToken.None);

        Assert.Equal(expected, result);
    }

    private static HttpClient CreateClient(HttpResponseMessage response)
    {
        return new HttpClient(new StubHandler(response))
        {
            BaseAddress = new Uri("http://localhost")
        };
    }

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }
}
