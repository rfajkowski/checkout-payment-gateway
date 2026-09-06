namespace PaymentGateway.Api.Payments.Models;

public sealed record PostPaymentResponse(Guid Id, PaymentStatus Status, string CardNumberLastFour, int ExpiryMonth, int ExpiryYear, string Currency, int Amount);
