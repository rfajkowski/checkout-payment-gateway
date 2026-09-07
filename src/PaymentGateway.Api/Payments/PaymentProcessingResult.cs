namespace PaymentGateway.Api.Payments;

public sealed record PaymentProcessingResult(Payment? Payment, IReadOnlyDictionary<string, string[]>? Errors)
{
    public bool IsRejected => Errors is not null;

    public static PaymentProcessingResult Rejected(IReadOnlyDictionary<string, string[]> errors) => new(null, errors);

    public static PaymentProcessingResult Completed(Payment payment) => new(payment, null);
}