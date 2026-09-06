using PaymentGateway.Api.AcquiringBank;
using PaymentGateway.Api.Payments.Models;

namespace PaymentGateway.Api.Tests;

internal static class PaymentRequestFactory
{
    public static PostPaymentRequest Valid() => new() { CardNumber = "2222405343248877", ExpiryMonth = 4, ExpiryYear = 2027, Currency = "GBP", Amount = 100, Cvv = "123" };
}

internal sealed class FakeBankClient(bool authorized) : IAcquiringBankClient
{
    public bool WasCalled { get; private set; }
    public Task<bool> AuthorizeAsync(AcquiringBankPaymentRequest request, CancellationToken cancellationToken) { WasCalled = true; return Task.FromResult(authorized); }
}

internal sealed class FailingBankClient : IAcquiringBankClient
{
    public Task<bool> AuthorizeAsync(AcquiringBankPaymentRequest request, CancellationToken cancellationToken) => throw new HttpRequestException("bank unavailable");
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
