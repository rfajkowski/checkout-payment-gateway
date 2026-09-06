using PaymentGateway.Api.Payments;
using PaymentGateway.Api.Persistence;

namespace PaymentGateway.Api.Tests.Persistence;

public sealed class InMemoryPaymentRepositoryTests
{
    [Fact]
    public void Add_WhenPaymentIdAlreadyExists_ThrowsInvalidOperationException()
    {
        var repository = new InMemoryPaymentRepository();
        var payment = CreatePayment(Guid.NewGuid());
        repository.Add(payment);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            repository.Add(payment));

        Assert.Contains(payment.Id.ToString(), exception.Message);
    }

    private static Payment CreatePayment(Guid id)
    {
        return new Payment
        {
            Id = id,
            Status = PaymentStatus.Authorized,
            CardNumberLastFour = "8877",
            ExpiryMonth = 4,
            ExpiryYear = 2027,
            Currency = "GBP",
            Amount = 100
        };
    }
}