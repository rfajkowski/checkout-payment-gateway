using System.Text.Json.Serialization;

namespace PaymentGateway.Api.Payments;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentStatus
{
    Authorized,
    Declined,
    Rejected
}
