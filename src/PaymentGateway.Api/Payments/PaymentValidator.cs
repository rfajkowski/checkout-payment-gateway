using PaymentGateway.Api.Payments.Models;

namespace PaymentGateway.Api.Payments;

public sealed class PaymentValidator(TimeProvider timeProvider)
{
    private static readonly HashSet<string> SupportedCurrencies =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "GBP",
            "USD",
            "EUR"
        };

    public IReadOnlyDictionary<string, string[]> Validate(PostPaymentRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        ValidateCardNumber(request.CardNumber, errors);
        ValidateExpiry(request.ExpiryMonth, request.ExpiryYear, errors);
        ValidateCurrency(request.Currency, errors);
        ValidateAmount(request.Amount, errors);
        ValidateCvv(request.Cvv, errors);

        return errors;
    }

    private static void ValidateCardNumber(
        string? cardNumber,
        IDictionary<string, string[]> errors)
    {
        Add(
            errors,
            "cardNumber",
            string.IsNullOrWhiteSpace(cardNumber),
            "Card number is required.");

        Add(
            errors,
            "cardNumber",
            !string.IsNullOrWhiteSpace(cardNumber) &&
            cardNumber.Length is < 14 or > 19,
            "Card number must be 14 to 19 characters.");

        Add(
            errors,
            "cardNumber",
            !string.IsNullOrWhiteSpace(cardNumber) &&
            cardNumber.Any(c => c is < '0' or > '9'),
            "Card number must contain only digits.");
    }

    private void ValidateExpiry(
        int? expiryMonth,
        int? expiryYear,
        IDictionary<string, string[]> errors)
    {
        Add(
            errors,
            "expiryMonth",
            expiryMonth is null,
            "Expiry month is required.");

        Add(
            errors,
            "expiryMonth",
            expiryMonth is < 1 or > 12,
            "Expiry month must be between 1 and 12.");

        Add(
            errors,
            "expiryYear",
            expiryYear is null,
            "Expiry year is required.");

        Add(
            errors,
            "expiryYear",
            expiryYear is < 1 or > 9999,
            "Expiry year must be a valid year.");

        if (expiryMonth is >= 1 and <= 12 &&
            expiryYear is >= 1 and <= 9999)
        {
            var now = timeProvider.GetUtcNow();

            var isExpired =
                expiryYear < now.Year ||
                (expiryYear == now.Year &&
                 expiryMonth < now.Month);

            Add(
                errors,
                "expiry",
                isExpired,
                "Expiry date must be in the future.");
        }
    }

    private static void ValidateCurrency(
        string? currency,
        IDictionary<string, string[]> errors)
    {
        Add(
            errors,
            "currency",
            string.IsNullOrWhiteSpace(currency),
            "Currency is required.");

        Add(
            errors,
            "currency",
            !string.IsNullOrWhiteSpace(currency) &&
            (currency.Length != 3 ||
             !SupportedCurrencies.Contains(currency)),
            "Currency must be one of GBP, USD or EUR.");
    }

    private static void ValidateAmount(
        int? amount,
        IDictionary<string, string[]> errors)
    {
        Add(
            errors,
            "amount",
            amount is null,
            "Amount is required.");
    }

    private static void ValidateCvv(
        string? cvv,
        IDictionary<string, string[]> errors)
    {
        Add(
            errors,
            "cvv",
            string.IsNullOrWhiteSpace(cvv),
            "CVV is required.");

        Add(
            errors,
            "cvv",
            !string.IsNullOrWhiteSpace(cvv) &&
            cvv.Length is < 3 or > 4,
            "CVV must be 3 or 4 characters.");

        Add(
            errors,
            "cvv",
            !string.IsNullOrWhiteSpace(cvv) &&
            cvv.Any(c => c is < '0' or > '9'),
            "CVV must contain only digits.");
    }

    private static void Add(
        IDictionary<string, string[]> errors,
        string key,
        bool invalid,
        string message)
    {
        if (invalid)
        {
            errors[key] = errors.TryGetValue(key, out var current)
                ? [.. current, message]
                : [message];
        }
    }
}