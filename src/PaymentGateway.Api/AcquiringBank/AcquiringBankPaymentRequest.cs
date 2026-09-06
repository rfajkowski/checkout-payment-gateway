namespace PaymentGateway.Api.AcquiringBank;

public sealed record AcquiringBankPaymentRequest(
    string CardNumber,
    int ExpiryMonth,
    int ExpiryYear,
    string Currency,
    int Amount,
    string Cvv);
