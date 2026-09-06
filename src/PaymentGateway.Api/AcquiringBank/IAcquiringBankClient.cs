namespace PaymentGateway.Api.AcquiringBank;

public interface IAcquiringBankClient
{
    Task<bool> AuthorizeAsync(
        AcquiringBankPaymentRequest request,
        CancellationToken cancellationToken);
}