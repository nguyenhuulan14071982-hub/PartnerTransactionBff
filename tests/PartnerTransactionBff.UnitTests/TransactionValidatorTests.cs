using PartnerTransactionBff.Contracts;
using PartnerTransactionBff.Domain;
using Xunit;

namespace PartnerTransactionBff.UnitTests;

public sealed class TransactionValidatorTests
{
    private readonly TransactionValidator _sut = new();
    private static PartnerTransactionRequest Valid() => new("P-1001", "TXN-99823", 250m, "USD", DateTimeOffset.Parse("2024-05-10T14:30:00Z"));

    [Fact] public void Valid_request_passes() => Assert.True(_sut.Validate(Valid()).IsValid);
    [Fact] public void Zero_amount_fails() => Assert.Contains(nameof(PartnerTransactionRequest.Amount), _sut.Validate(Valid() with { Amount = 0 }).Errors.Keys);
    [Fact] public void Negative_amount_fails() => Assert.False(_sut.Validate(Valid() with { Amount = -1 }).IsValid);
    [Fact] public void Invalid_currency_fails() => Assert.False(_sut.Validate(Valid() with { Currency = "ZZZ" }).IsValid);
    [Fact] public void Missing_required_fields_fails() => Assert.False(_sut.Validate(new(null, null, null, null, null)).IsValid);
    [Fact] public void Invalid_partner_format_fails() => Assert.False(_sut.Validate(Valid() with { PartnerId = "partner" }).IsValid);
    [Fact] public void Long_reference_fails() => Assert.False(_sut.Validate(Valid() with { TransactionReference = new string('X', 101) }).IsValid);
}
