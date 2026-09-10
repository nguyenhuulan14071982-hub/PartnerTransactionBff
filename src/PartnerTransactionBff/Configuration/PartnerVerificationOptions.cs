namespace PartnerTransactionBff.Configuration;

public sealed class PartnerVerificationOptions
{
    public const string SectionName = "PartnerVerification";
    public string BaseUrl { get; init; } = "http://localhost:5080";
}
