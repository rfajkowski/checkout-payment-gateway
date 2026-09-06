namespace PaymentGateway.Api.Payments.Models;

public sealed record RejectedPaymentResponse(string Status, IReadOnlyDictionary<string, string[]>? Errors);
