using PaymentGateway.Api.Payments;
using PaymentGateway.Api.Payments.Models;

namespace PaymentGateway.Api.Tests.Payments;

public sealed class PaymentValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly PaymentValidator _validator = new(new FixedTimeProvider(Now));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234567890123")]
    [InlineData("12345678901234567890")]
    [InlineData("1234567890123A")]
    [InlineData("1234567890123-")]
    [InlineData("1234567890123 ")]
    public void Validate_WithInvalidCardNumber_ReturnsCardNumberError(string? cardNumber)
    {
        var errors = _validator.Validate(CreateRequest(cardNumber: cardNumber));

        Assert.Contains("cardNumber", errors.Keys);
    }

    [Theory]
    [InlineData("12345678901234")]
    [InlineData("1234567890123456789")]
    public void Validate_WithValidCardNumberLength_ReturnsNoCardNumberError(string cardNumber)
    {
        var errors = _validator.Validate(CreateRequest(cardNumber: cardNumber));

        Assert.DoesNotContain("cardNumber", errors.Keys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(13)]
    public void Validate_WithInvalidExpiryMonth_ReturnsExpiryMonthError(int? expiryMonth)
    {
        var errors = _validator.Validate(CreateRequest(expiryMonth: expiryMonth));

        Assert.Contains("expiryMonth", errors.Keys);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    public void Validate_WithValidExpiryMonth_ReturnsNoExpiryMonthError(int expiryMonth)
    {
        var errors = _validator.Validate(CreateRequest(expiryMonth: expiryMonth, expiryYear: 2027));

        Assert.DoesNotContain("expiryMonth", errors.Keys);
    }

    [Fact]
    public void Validate_WithMissingExpiryYear_ReturnsExpiryYearError()
    {
        var errors = _validator.Validate(CreateRequest(expiryYear: null));

        Assert.Contains("expiryYear", errors.Keys);
    }

    [Theory]
    [InlineData(8, 2026, true)]
    [InlineData(9, 2026, false)]
    [InlineData(10, 2026, false)]
    [InlineData(9, 2025, true)]
    [InlineData(1, 2027, false)]
    public void Validate_UsesEndOfMonthExpirySemantics(
        int expiryMonth,
        int expiryYear,
        bool expectedExpired)
    {
        var errors = _validator.Validate(
            CreateRequest(expiryMonth: expiryMonth, expiryYear: expiryYear));

        Assert.Equal(expectedExpired, errors.ContainsKey("expiry"));
    }

    [Theory]
    [InlineData("GBP")]
    [InlineData("USD")]
    [InlineData("EUR")]
    [InlineData("gbp")]
    public void Validate_WithSupportedCurrency_ReturnsNoCurrencyError(string currency)
    {
        var errors = _validator.Validate(CreateRequest(currency: currency));

        Assert.DoesNotContain("currency", errors.Keys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("GB")]
    [InlineData("GBPX")]
    [InlineData("CAD")]
    public void Validate_WithInvalidCurrency_ReturnsCurrencyError(string? currency)
    {
        var errors = _validator.Validate(CreateRequest(currency: currency));

        Assert.Contains("currency", errors.Keys);
    }

    [Fact]
    public void Validate_WithMissingAmount_ReturnsAmountError()
    {
        var errors = _validator.Validate(CreateRequest(amount: null));

        Assert.Contains("amount", errors.Keys);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(-1)]
    public void Validate_WithPresentIntegerAmount_ReturnsNoAmountError(int amount)
    {
        var errors = _validator.Validate(CreateRequest(amount: amount));

        Assert.DoesNotContain("amount", errors.Keys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("12")]
    [InlineData("12345")]
    [InlineData("12A")]
    [InlineData("1 3")]
    public void Validate_WithInvalidCvv_ReturnsCvvError(string? cvv)
    {
        var errors = _validator.Validate(CreateRequest(cvv: cvv));

        Assert.Contains("cvv", errors.Keys);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("1234")]
    [InlineData("012")]
    public void Validate_WithValidCvv_ReturnsNoCvvError(string cvv)
    {
        var errors = _validator.Validate(CreateRequest(cvv: cvv));

        Assert.DoesNotContain("cvv", errors.Keys);
    }

    private static PostPaymentRequest CreateRequest(
        string? cardNumber = "2222405343248877",
        int? expiryMonth = 10,
        int? expiryYear = 2026,
        string? currency = "GBP",
        int? amount = 100,
        string? cvv = "123")
    {
        return new PostPaymentRequest
        {
            CardNumber = cardNumber,
            ExpiryMonth = expiryMonth,
            ExpiryYear = expiryYear,
            Currency = currency,
            Amount = amount,
            Cvv = cvv
        };
    }
}