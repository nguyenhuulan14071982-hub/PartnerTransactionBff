namespace PartnerTransactionBff.Configuration;

public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    public bool RequireApiKey { get; init; }
    public string ApiKey { get; init; } = string.Empty;
}
