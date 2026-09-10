using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PartnerTransactionBff.Services;

namespace PartnerTransactionBff.Controllers;

[ApiController]
[Route("api/v1/partner/transactions")]
public sealed class PartnerTransactionsController(
    ITransactionService service) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting("partner-transactions")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Post(
        Contracts.PartnerTransactionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.ProcessAsync(
            request,
            Request.Headers["Idempotency-Key"].FirstOrDefault(),
            cancellationToken);

        return result.Status switch
        {
            TransactionProcessStatus.Accepted => Accepted(new
            {
                status = "queued",
                transactionReference = request.TransactionReference
            }),
            TransactionProcessStatus.Duplicate => Accepted(new
            {
                status = "already-queued",
                transactionReference = request.TransactionReference
            }),
            //TransactionProcessStatus.Rejected when result.ValidationErrors is not null =>
            //    ValidationProblem(new ValidationProblemDetails(result.ValidationErrors)),
            TransactionProcessStatus.Rejected => StatusCode(
                StatusCodes.Status403Forbidden,
                new { error = result.Reason }),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
