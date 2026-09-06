using Microsoft.Extensions.Logging.Abstractions;

using PaymentGateway.Api.AcquiringBank;
using PaymentGateway.Api.Observability;
using PaymentGateway.Api.Payments;
using PaymentGateway.Api.Payments.Models;
using PaymentGateway.Api.Persistence;

namespace PaymentGateway.Api.Tests.Payments;

public sealed class PaymentServiceTests
{
    [Fact]
    public async Task ProcessAsync_WhenAuthorized_PersistsSafeNormalizedPayment()
    {
        var bank = new FakeBankClient(true);
        var repository = new RecordingPaymentRepository();
        var service = CreateService(bank, repository);
        var request = new PostPaymentRequest
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = 4,
            ExpiryYear = 2027,
            Currency = "gbp",
            Amount = 100,
            Cvv = "012"
        };

        var result = await service.ProcessAsync(request, CancellationToken.None);

        Assert.False(result.IsRejected);
        Assert.Equal(1, bank.CallCount);
        Assert.Equal(1, repository.AddCount);
        Assert.Equal(PaymentStatus.Authorized, result.Payment!.Status);
        Assert.Equal("8877", result.Payment.CardNumberLastFour);
        Assert.Equal("GBP", result.Payment.Currency);
        Assert.Same(result.Payment, repository.Get(result.Payment.Id));
        Assert.DoesNotContain(nameof(PostPaymentRequest.CardNumber), PaymentPropertyNames());
        Assert.DoesNotContain(nameof(PostPaymentRequest.Cvv), PaymentPropertyNames());
    }

    [Fact]
    public async Task ProcessAsync_WhenDeclined_PersistsDeclinedPayment()
    {
        var bank = new FakeBankClient(false);
        var repository = new RecordingPaymentRepository();
        var service = CreateService(bank, repository);

        var result = await service.ProcessAsync(
            PaymentRequestFactory.Valid(),
            CancellationToken.None);

        Assert.False(result.IsRejected);
        Assert.Equal(1, bank.CallCount);
        Assert.Equal(1, repository.AddCount);
        Assert.Equal(PaymentStatus.Declined, result.Payment!.Status);
        Assert.Same(result.Payment, repository.Get(result.Payment.Id));
    }

    [Fact]
    public async Task ProcessAsync_WhenRejected_DoesNotCallBankOrPersistPayment()
    {
        var bank = new FakeBankClient(true);
        var repository = new RecordingPaymentRepository();
        var service = CreateService(bank, repository);

        var result = await service.ProcessAsync(
            new PostPaymentRequest(),
            CancellationToken.None);

        Assert.True(result.IsRejected);
        Assert.Equal(0, bank.CallCount);
        Assert.Equal(0, repository.AddCount);
    }

    [Fact]
    public async Task ProcessAsync_WhenBankFails_PropagatesAndDoesNotPersistPayment()
    {
        var repository = new RecordingPaymentRepository();
        var service = CreateService(new FailingBankClient(), repository);

        await Assert.ThrowsAsync<AcquiringBankException>(() =>
            service.ProcessAsync(PaymentRequestFactory.Valid(), CancellationToken.None));

        Assert.Equal(0, repository.AddCount);
    }

    private static PaymentService CreateService(
        IAcquiringBankClient bank,
        IPaymentRepository repository)
    {
        return new PaymentService(
            new PaymentValidator(
                new FixedTimeProvider(
                    new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))),
            bank,
            repository,
            NullLogger<PaymentService>.Instance,
            new PaymentMetrics());
    }

    private static IReadOnlyCollection<string> PaymentPropertyNames()
    {
        return typeof(Payment)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();
    }
}