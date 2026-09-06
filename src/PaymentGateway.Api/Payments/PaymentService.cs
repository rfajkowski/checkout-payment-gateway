using PaymentGateway.Api.AcquiringBank;
using PaymentGateway.Api.Payments.Models;
using PaymentGateway.Api.Persistence;

namespace PaymentGateway.Api.Payments;

public sealed class PaymentService(PaymentValidator validator, IAcquiringBankClient bank, IPaymentRepository repository, ILogger<PaymentService> logger)
{
    public async Task<PaymentProcessingResult> ProcessAsync(PostPaymentRequest request, CancellationToken cancellationToken)
    {
        var errors = validator.Validate(request);
        if (errors.Count > 0) 
            return PaymentProcessingResult.Rejected(errors);

        var bankRequest = new AcquiringBankPaymentRequest(
            request.CardNumber!,
            request.ExpiryMonth!.Value,
            request.ExpiryYear!.Value,
            request.Currency!.ToUpperInvariant(),
            request.Amount!.Value,
            request.Cvv!);


        var authorized = await bank.AuthorizeAsync(bankRequest, cancellationToken);
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            Status = authorized ? PaymentStatus.Authorized : PaymentStatus.Declined,
            CardNumberLastFour = request.CardNumber![^4..],
            ExpiryMonth = request.ExpiryMonth!.Value,
            ExpiryYear = request.ExpiryYear!.Value,
            Currency = request.Currency!.ToUpperInvariant(),
            Amount = request.Amount!.Value
        };
        repository.Add(payment);

        logger.LogInformation("Payment {PaymentId} received bank outcome {PaymentStatus}", payment.Id, payment.Status);
        return PaymentProcessingResult.Completed(payment);
    }
}

public sealed record PaymentProcessingResult(Payment? Payment, IReadOnlyDictionary<string, string[]>? Errors)
{
    public bool IsRejected => Errors is not null;
    public static PaymentProcessingResult Rejected(IReadOnlyDictionary<string, string[]> errors) => new(null, errors);
    public static PaymentProcessingResult Completed(Payment payment) => new(payment, null);
}
