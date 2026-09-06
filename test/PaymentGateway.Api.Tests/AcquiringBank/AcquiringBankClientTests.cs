using System.Net;
using System.Net.Http.Json;
using PaymentGateway.Api.AcquiringBank;

namespace PaymentGateway.Api.Tests.AcquiringBank;

public sealed class AcquiringBankClientTests
{
    [Fact]
    public async Task AuthorizeAsync_SendsSimulatorContractAndMapsAuthorization()
    {
        var handler = new CapturingHandler();
        var client = new AcquiringBankClient(new HttpClient(handler) { BaseAddress = new Uri("http://bank/") });

        var authorized = await client.AuthorizeAsync(new AcquiringBankPaymentRequest("2222405343248877", 4, 2027, "GBP", 100, "123"), CancellationToken.None);

        Assert.True(authorized);
        Assert.Contains("\"card_number\":\"2222405343248877\"", handler.Body);
        Assert.Contains("\"expiry_date\":\"04/2027\"", handler.Body);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string Body { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("/payments", request.RequestUri!.AbsolutePath);
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { authorized = true, authorization_code = "code" }) };
        }
    }
}
