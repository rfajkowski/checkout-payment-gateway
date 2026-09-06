using PaymentGateway.Api.Payments;

namespace PaymentGateway.Api.Persistence;

public interface IPaymentRepository
{
    void Add(Payment payment);

    Payment? Get(Guid id);
}