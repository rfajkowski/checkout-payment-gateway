using System.Diagnostics.Metrics;

using PaymentGateway.Api.Payments;

namespace PaymentGateway.Api.Observability;

public sealed class PaymentMetrics
{
    private static readonly Meter Meter = new("PaymentGateway");
    private static readonly Counter<long> PaymentsProcessed = Meter.CreateCounter<long>("payments.processed");
    private static readonly Counter<long> PaymentsRejected = Meter.CreateCounter<long>("payments.rejected");
    private static readonly Counter<long> BankRequests = Meter.CreateCounter<long>("acquiring_bank.requests");
    private static readonly Counter<long> BankFailures = Meter.CreateCounter<long>("acquiring_bank.failures");
    private static readonly Histogram<double> BankDuration = Meter.CreateHistogram<double>("acquiring_bank.duration", "ms");

    public void RecordPaymentProcessed(PaymentStatus status, string currency)
    {
        PaymentsProcessed.Add(
            1,
            new KeyValuePair<string, object?>("status", status.ToString()),
            new KeyValuePair<string, object?>("currency", currency));
    }

    public void RecordPaymentRejected()
    {
        PaymentsRejected.Add(1);
    }

    public void RecordBankRequest(string outcome)
    {
        BankRequests.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
    }

    public void RecordBankFailure(string reason)
    {
        BankFailures.Add(1, new KeyValuePair<string, object?>("reason", reason));
    }

    public void RecordBankDuration(double milliseconds)
    {
        BankDuration.Record(milliseconds);
    }
}