using PaymentGateway.Api.AcquiringBank;
using PaymentGateway.Api.Payments;
using PaymentGateway.Api.Payments.Models;
using PaymentGateway.Api.Persistence;

namespace PaymentGateway.Api.Tests;

internal static class PaymentRequestFactory
{
    public static PostPaymentRequest Valid()
    {
        return new PostPaymentRequest
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = 4,
            ExpiryYear = 2027,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };
    }
}

internal sealed class FakeBankClient(bool authorized) : IAcquiringBankClient
{
    public int CallCount { get; private set; }

    public AcquiringBankPaymentRequest? LastRequest { get; private set; }

    public Task<bool> AuthorizeAsync(
        AcquiringBankPaymentRequest request,
        CancellationToken cancellationToken)
    {
        CallCount++;
        LastRequest = request;

        return Task.FromResult(authorized);
    }
}

internal sealed class FailingBankClient : IAcquiringBankClient
{
    public Task<bool> AuthorizeAsync(
        AcquiringBankPaymentRequest request,
        CancellationToken cancellationToken)
    {
        throw new AcquiringBankException("The acquiring bank is unavailable.");
    }
}

internal sealed class RecordingPaymentRepository : IPaymentRepository
{
    private readonly Dictionary<Guid, Payment> _payments = [];

    public int AddCount { get; private set; }

    public void Add(Payment payment)
    {
        AddCount++;
        _payments.Add(payment.Id, payment);
    }

    public Payment? Get(Guid id)
    {
        return _payments.GetValueOrDefault(id);
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow()
    {
        return now;
    }
}