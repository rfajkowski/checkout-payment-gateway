using System.Collections.Concurrent;
namespace PaymentGateway.Api.Persistence;

public sealed class InMemoryPaymentRepository : IPaymentRepository
{
    private readonly ConcurrentDictionary<Guid, Payment> _payments = new(); 
    public void Add(Payment payment) => _payments.TryAdd(payment.Id, payment); 
    public Payment? Get(Guid id) => _payments.GetValueOrDefault(id);
}
