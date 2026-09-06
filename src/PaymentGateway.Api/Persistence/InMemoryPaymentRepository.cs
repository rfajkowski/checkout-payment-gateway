using System.Collections.Concurrent;

using PaymentGateway.Api.Payments;

namespace PaymentGateway.Api.Persistence;

public sealed class InMemoryPaymentRepository : IPaymentRepository
{
    private readonly ConcurrentDictionary<Guid, Payment> _payments = new();

    public void Add(Payment payment)
    {
        if (!_payments.TryAdd(payment.Id, payment))
        {
            throw new InvalidOperationException($"Payment {payment.Id} already exists.");
        }
    }

    public Payment? Get(Guid id)
    {
        return _payments.GetValueOrDefault(id);
    }
}