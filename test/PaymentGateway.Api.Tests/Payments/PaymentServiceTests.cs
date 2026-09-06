using Microsoft.Extensions.Logging.Abstractions;
using PaymentGateway.Api.AcquiringBank;
using PaymentGateway.Api.Payments;
using PaymentGateway.Api.Payments.Models;
using PaymentGateway.Api.Persistence;

namespace PaymentGateway.Api.Tests.Payments;

public sealed class PaymentServiceTests
{
    [Fact]
    public async Task InvalidRequest_DoesNotCallBankOrPersistPayment()
    {
        var bank = new FakeBankClient(true);
        var service = CreateService(bank, new InMemoryPaymentRepository());

        var result = await service.ProcessAsync(new PostPaymentRequest(), CancellationToken.None);

        Assert.True(result.IsRejected);
        Assert.False(bank.WasCalled);
    }

    [Fact]
    public async Task DeclinedPayment_IsPersisted()
    {
        var repository = new InMemoryPaymentRepository();
        var service = CreateService(new FakeBankClient(false), repository);

        var result = await service.ProcessAsync(PaymentRequestFactory.Valid(), CancellationToken.None);

        Assert.Equal(PaymentStatus.Declined, result.Payment!.Status);
        Assert.NotNull(repository.Get(result.Payment.Id));
    }

    [Fact]
    public async Task BankFailure_IsNotConvertedToBusinessOutcome()
    {
        var service = CreateService(new FailingBankClient(), new InMemoryPaymentRepository());
        await Assert.ThrowsAsync<HttpRequestException>(() => service.ProcessAsync(PaymentRequestFactory.Valid(), CancellationToken.None));
    }

    private static PaymentService CreateService(IAcquiringBankClient bank, IPaymentRepository repository) =>
        new(new PaymentValidator(new FixedTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))), bank, repository, NullLogger<PaymentService>.Instance);
}
