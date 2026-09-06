using System.Text.Json.Serialization;

namespace PaymentGateway.Api.AcquiringBank;

public sealed class AcquiringBankClient(HttpClient httpClient) : IAcquiringBankClient
{
    public async Task<bool> AuthorizeAsync(AcquiringBankPaymentRequest request, CancellationToken cancellationToken)
    {
        var payload = new BankPaymentRequest
        {
            CardNumber = request.CardNumber,
            ExpiryDate = $"{request.ExpiryMonth:00}/{request.ExpiryYear}",
            Currency = request.Currency.ToUpperInvariant(),
            Amount = request.Amount,
            Cvv = request.Cvv
        };

        using var response = await httpClient.PostAsJsonAsync("payments", payload, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<BankPaymentResponse>(cancellationToken: cancellationToken);
        return result?.Authorized ?? throw new HttpRequestException("The acquiring bank returned an invalid response.");
    }

    private sealed class BankPaymentRequest
    {
        [JsonPropertyName("card_number")] public required string CardNumber { get; init; }
        [JsonPropertyName("expiry_date")] public required string ExpiryDate { get; init; }
        [JsonPropertyName("currency")] public required string Currency { get; init; }
        [JsonPropertyName("amount")] public int Amount { get; init; }
        [JsonPropertyName("cvv")] public required string Cvv { get; init; }
    }
    private sealed class BankPaymentResponse
    {
        [JsonPropertyName("authorized")] public bool Authorized { get; init; }
    }
}
