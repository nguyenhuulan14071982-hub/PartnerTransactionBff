using System.Text.RegularExpressions;
using PartnerTransactionBff.Contracts;

namespace PartnerTransactionBff.Domain;

public sealed record ValidationResult(bool IsValid, IReadOnlyDictionary<string, string[]> Errors)
{
    public static ValidationResult Success() => new(true, new Dictionary<string, string[]>());
}

public interface ITransactionValidator
{
    ValidationResult Validate(PartnerTransactionRequest request);
}

public sealed class TransactionValidator : ITransactionValidator
{
    private static readonly Regex PartnerIdPattern = new("^P-[A-Za-z0-9]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly HashSet<string> Currencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "AED","AUD","CAD","CHF","CNY","DKK","EUR","GBP","HKD","IDR","INR","JPY","KRW","MYR","NOK","NZD","PHP","SEK","SGD","THB","USD","VND"
    };

    public ValidationResult Validate(PartnerTransactionRequest request)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        AddRequired(request.PartnerId, nameof(request.PartnerId), errors);
        AddRequired(request.TransactionReference, nameof(request.TransactionReference), errors);
        AddRequired(request.Currency, nameof(request.Currency), errors);

        if (!string.IsNullOrWhiteSpace(request.PartnerId) && !PartnerIdPattern.IsMatch(request.PartnerId))
            Add(nameof(request.PartnerId), "PartnerId format is invalid.", errors);
        if (!string.IsNullOrWhiteSpace(request.TransactionReference) && request.TransactionReference.Length > 100)
            Add(nameof(request.TransactionReference), "TransactionReference must be 100 characters or fewer.", errors);
        if (request.Amount is null) Add(nameof(request.Amount), "Amount is required.", errors);
        else if (request.Amount <= 0) Add(nameof(request.Amount), "Amount must be greater than 0.", errors);
        if (!string.IsNullOrWhiteSpace(request.Currency) && !Currencies.Contains(request.Currency))
            Add(nameof(request.Currency), "Currency is not a supported ISO 4217 currency.", errors);
        if (request.Timestamp is null) Add(nameof(request.Timestamp), "Timestamp is required.", errors);

        return errors.Count == 0
            ? ValidationResult.Success()
            : new(false, errors.ToDictionary(x => x.Key, x => x.Value.ToArray(), StringComparer.OrdinalIgnoreCase));
    }

    private static void AddRequired(string? value, string field, Dictionary<string, List<string>> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) Add(field, "Field is required.", errors);
    }

    private static void Add(string field, string message, Dictionary<string, List<string>> errors)
    {
        if (!errors.TryGetValue(field, out var list)) errors[field] = list = [];
        list.Add(message);
    }
}
