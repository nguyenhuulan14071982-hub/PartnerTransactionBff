using Microsoft.AspNetCore.Mvc;
using PartnerTransactionBff.Contracts;

namespace PartnerTransactionBff.Controllers;

[ApiController]
[Route("mock/partners")]
public sealed class MockPartnerVerificationController : ControllerBase
{
    [HttpGet("{partnerId}/verify")]
    public async Task<ActionResult<PartnerVerificationResponse>> Verify(
        string partnerId,
        CancellationToken cancellationToken)
    {
        await Task.Delay(Random.Shared.Next(10, 50), cancellationToken);

        if (Random.Shared.NextDouble() < 0.30)
        {
            throw new TimeoutException("Simulated partner verification timeout.");
        }

        var verified = partnerId.StartsWith("P-", StringComparison.OrdinalIgnoreCase);
        return Ok(new PartnerVerificationResponse(
            verified,
            partnerId,
            verified ? $"Partner {partnerId}" : string.Empty,
            verified ? "STANDARD" : string.Empty));
    }
}
