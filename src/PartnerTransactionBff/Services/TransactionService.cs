using PartnerTransactionBff.Application;
using PartnerTransactionBff.Contracts;
using PartnerTransactionBff.Domain;
using PartnerTransactionBff.Infrastructure;

namespace PartnerTransactionBff.Services;

public enum TransactionProcessStatus
{
    Accepted,
    Duplicate,
    Rejected
}

public sealed record ProcessTransactionResult(
    TransactionProcessStatus Status,
    string? Reason = null,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);

public interface ITransactionService
{
    Task<ProcessTransactionResult> ProcessAsync(
        PartnerTransactionRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken);
}

public sealed class TransactionService(
    ITransactionValidator validator,
    IPartnerVerificationClient verificationClient,
    ITransactionQueue queue,
    IIdempotencyStore idempotencyStore,
    ILogger<TransactionService> logger) : ITransactionService
{
    public async Task<ProcessTransactionResult> ProcessAsync(
        PartnerTransactionRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid)
        {
            return new(TransactionProcessStatus.Rejected, "Validation failed.", validation.Errors);
        }

        var key = BuildIdempotencyKey(request, idempotencyKey);
        if (!await idempotencyStore.TryBeginAsync(key, cancellationToken))
        {
            logger.LogInformation(
                "Duplicate transaction ignored. PartnerId: {PartnerId}, TransactionReference: {TransactionReference}",
                request.PartnerId,
                request.TransactionReference);

            return new(TransactionProcessStatus.Duplicate);
        }

        try
        {
            var partner = await verificationClient.VerifyAsync(request.PartnerId!, cancellationToken);
            if (partner is null || !partner.Verified)
            {
                await idempotencyStore.RemoveAsync(key, cancellationToken);
                return new(TransactionProcessStatus.Rejected, "Partner could not be verified.");
            }

            var message = new PartnerTransactionMessage(
                MessageId: Guid.NewGuid().ToString("N"),
                SchemaVersion: 1,
                PartnerId: request.PartnerId!,
                TransactionReference: request.TransactionReference!,
                Amount: request.Amount!.Value,
                Currency: request.Currency!.ToUpperInvariant(),
                Timestamp: request.Timestamp!.Value,
                PartnerName: partner.PartnerName,
                PartnerTier: partner.PartnerTier,
                EnrichedAt: DateTimeOffset.UtcNow);

            await queue.PublishAsync(message, cancellationToken);
            await idempotencyStore.CompleteAsync(key, cancellationToken);

            return new(TransactionProcessStatus.Accepted);
        }
        catch
        {
            await idempotencyStore.RemoveAsync(key, CancellationToken.None);
            throw;
        }
    }

    private static string BuildIdempotencyKey(
        PartnerTransactionRequest request,
        string? explicitKey) =>
        string.IsNullOrWhiteSpace(explicitKey)
            ? $"{request.PartnerId}:{request.TransactionReference}".ToUpperInvariant()
            : explicitKey.Trim();
}
