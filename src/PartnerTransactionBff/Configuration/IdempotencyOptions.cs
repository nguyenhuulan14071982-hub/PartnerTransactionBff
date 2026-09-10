namespace PartnerTransactionBff.Configuration;

public sealed class IdempotencyOptions
{
    public const string SectionName = "Idempotency";
    public TimeSpan EntryLifetime { get; init; } = TimeSpan.FromMinutes(30);
}
