using PaymentGateway.Api.Payments;
using PaymentGateway.Api.Payments.Models;

namespace PaymentGateway.Api.Tests.Payments;

public sealed class PaymentValidatorTests
{
    private readonly PaymentValidator _validator = new(new FixedTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));

    [Fact]
    public void ValidRequest_HasNoErrors() => Assert.Empty(_validator.Validate(PaymentRequestFactory.Valid()));

    [Fact]
    public void NonNumericCardNumber_IsRejected()
    {
        var request = new PostPaymentRequest { CardNumber = "12A", ExpiryMonth = 4, ExpiryYear = 2027, Currency = "GBP", Amount = 100, Cvv = "123" };
        Assert.Contains("cardNumber", _validator.Validate(request).Keys);
    }

    [Fact]
    public void ExpiredCard_IsRejected()
    {
        var request = new PostPaymentRequest { CardNumber = "2222405343248877", ExpiryMonth = 12, ExpiryYear = 2025, Currency = "GBP", Amount = 100, Cvv = "123" };
        Assert.Contains("expiry", _validator.Validate(request).Keys);
    }
}
