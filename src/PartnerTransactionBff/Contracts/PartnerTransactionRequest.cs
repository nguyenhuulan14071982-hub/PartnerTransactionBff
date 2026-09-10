namespace PartnerTransactionBff.Contracts;

public sealed record PartnerTransactionRequest(
    string? PartnerId,
    string? TransactionReference,
    decimal? Amount,
    string? Currency,
    DateTimeOffset? Timestamp);

public sealed record PartnerVerificationResponse(
    bool Verified,
    string PartnerId,
    string PartnerName,
    string PartnerTier);

public sealed record PartnerTransactionMessage(
    string MessageId,
    int SchemaVersion,
    string PartnerId,
    string TransactionReference,
    decimal Amount,
    string Currency,
    DateTimeOffset Timestamp,
    string PartnerName,
    string PartnerTier,
    DateTimeOffset EnrichedAt);

public sealed record ApiError(string Code, string Message, string TraceId);
