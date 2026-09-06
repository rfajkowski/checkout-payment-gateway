using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

using PaymentGateway.Api.Observability;

namespace PaymentGateway.Api.AcquiringBank;

public sealed class AcquiringBankClient(
    HttpClient httpClient,
    ILogger<AcquiringBankClient> logger,
    PaymentMetrics metrics) : IAcquiringBankClient
{
    public async Task<bool> AuthorizeAsync(
        AcquiringBankPaymentRequest request,
        CancellationToken cancellationToken)
    {
        long startTimestamp = Stopwatch.GetTimestamp();
        logger.LogInformation("Acquiring bank request started");

        try
        {
            var payload = new BankPaymentRequest
            {
                CardNumber = request.CardNumber,
                ExpiryDate = $"{request.ExpiryMonth:00}/{request.ExpiryYear}",
                Currency = request.Currency.ToUpperInvariant(),
                Amount = request.Amount,
                Cvv = request.Cvv
            };

            using var response = await httpClient.PostAsJsonAsync(
                "payments",
                payload,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                RecordFailure(
                    "http",
                    Stopwatch.GetElapsedTime(startTimestamp),
                    response.StatusCode);

                throw new AcquiringBankException(
                    $"The acquiring bank returned HTTP status {(int)response.StatusCode}.");
            }

            var result = await response.Content.ReadFromJsonAsync<BankPaymentResponse>(
                cancellationToken: cancellationToken);

            var authorized = result?.Authorized
                ?? throw CreateInvalidResponseException(
                    Stopwatch.GetElapsedTime(startTimestamp));

            var outcome = authorized ? "authorized" : "declined";
            TimeSpan elapsed = Stopwatch.GetElapsedTime(startTimestamp);
            metrics.RecordBankRequest(outcome);

            logger.LogInformation(
                "Acquiring bank request completed with outcome {BankOutcome} in {DurationMs} ms",
                outcome,
                elapsed.TotalMilliseconds);

            return authorized;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            RecordFailure(
                "timeout",
                Stopwatch.GetElapsedTime(startTimestamp),
                exception: exception);

            throw new AcquiringBankException(
                "The acquiring bank request timed out.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            RecordFailure(
                "http",
                Stopwatch.GetElapsedTime(startTimestamp),
                exception: exception);

            throw new AcquiringBankException(
                "The acquiring bank request failed.",
                exception);
        }
        catch (JsonException exception)
        {
            RecordFailure(
                "invalid_response",
                Stopwatch.GetElapsedTime(startTimestamp),
                exception: exception);

            throw new AcquiringBankException(
                "The acquiring bank returned an invalid response.",
                exception);
        }
        finally
        {
            TimeSpan elapsed = Stopwatch.GetElapsedTime(startTimestamp);
            metrics.RecordBankDuration(elapsed.TotalMilliseconds);
        }
    }

    private AcquiringBankException CreateInvalidResponseException(TimeSpan duration)
    {
        RecordFailure("invalid_response", duration);

        return new AcquiringBankException(
            "The acquiring bank returned an invalid response.");
    }

    private void RecordFailure(
        string reason,
        TimeSpan duration,
        System.Net.HttpStatusCode? statusCode = null,
        Exception? exception = null)
    {
        metrics.RecordBankRequest("failed");
        metrics.RecordBankFailure(reason);

        const string message =
            "Acquiring bank request failed for reason {FailureReason} " +
            "with status {BankStatusCode} after {DurationMs} ms";

        if (exception is null)
        {
            logger.LogWarning(
                message,
                reason,
                statusCode is null ? null : (int)statusCode,
                duration.TotalMilliseconds);
        }
        else
        {
            logger.LogWarning(
                exception,
                message,
                reason,
                statusCode is null ? null : (int)statusCode,
                duration.TotalMilliseconds);
        }
    }

    private sealed class BankPaymentRequest
    {
        [JsonPropertyName("card_number")]
        public required string CardNumber { get; init; }

        [JsonPropertyName("expiry_date")]
        public required string ExpiryDate { get; init; }

        [JsonPropertyName("currency")]
        public required string Currency { get; init; }

        [JsonPropertyName("amount")]
        public int Amount { get; init; }

        [JsonPropertyName("cvv")]
        public required string Cvv { get; init; }
    }

    private sealed class BankPaymentResponse
    {
        [JsonPropertyName("authorized")]
        public bool? Authorized { get; init; }
    }
}