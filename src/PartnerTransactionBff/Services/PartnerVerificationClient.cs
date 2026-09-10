using System.Net;
using System.Net.Http.Json;
using PartnerTransactionBff.Contracts;

namespace PartnerTransactionBff.Services;

public interface IPartnerVerificationClient
{
    Task<PartnerVerificationResponse?> VerifyAsync(string partnerId, CancellationToken cancellationToken);
}

public sealed class PartnerVerificationClient(HttpClient httpClient) : IPartnerVerificationClient
{
    public async Task<PartnerVerificationResponse?> VerifyAsync(
        string partnerId,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            $"/mock/partners/{Uri.EscapeDataString(partnerId)}/verify",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PartnerVerificationResponse>(cancellationToken);
    }
}
