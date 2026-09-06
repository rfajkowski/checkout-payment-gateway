using System.Net;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;

using PaymentGateway.Api.AcquiringBank;
using PaymentGateway.Api.Observability;

namespace PaymentGateway.Api.Tests.AcquiringBank;

public sealed class AcquiringBankClientTests
{
    [Fact]
    public async Task AuthorizeAsync_SendsExpectedSimulatorContract()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync(cancellationToken);

            return JsonResponse(HttpStatusCode.OK, """{"authorized":true}""");
        });
        var client = CreateClient(handler);
        var request = new AcquiringBankPaymentRequest(
            "2222405343248877",
            4,
            2027,
            "gbp",
            100,
            "012");

        await client.AuthorizeAsync(request, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("/payments", capturedRequest.RequestUri!.AbsolutePath);

        using var document = JsonDocument.Parse(capturedBody!);
        var root = document.RootElement;
        Assert.Equal("2222405343248877", root.GetProperty("card_number").GetString());
        Assert.Equal("04/2027", root.GetProperty("expiry_date").GetString());
        Assert.Equal("GBP", root.GetProperty("currency").GetString());
        Assert.Equal(100, root.GetProperty("amount").GetInt32());
        Assert.Equal("012", root.GetProperty("cvv").GetString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AuthorizeAsync_MapsAuthorizedValue(bool authorized)
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(
                JsonResponse(
                    HttpStatusCode.OK,
                    $$"""{"authorized":{{authorized.ToString().ToLowerInvariant()}}}""")));
        var client = CreateClient(handler);

        var result = await client.AuthorizeAsync(BankRequest(), CancellationToken.None);

        Assert.Equal(authorized, result);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenBankReturns503_ThrowsAcquiringBankException()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.ServiceUnavailable, "{}")));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<AcquiringBankException>(() =>
            client.AuthorizeAsync(BankRequest(), CancellationToken.None));
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    public async Task AuthorizeAsync_WithInvalidResponse_ThrowsAcquiringBankException(string responseBody)
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, responseBody)));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<AcquiringBankException>(() =>
            client.AuthorizeAsync(BankRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task AuthorizeAsync_WhenNetworkFails_ThrowsAcquiringBankException()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("network failed")));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<AcquiringBankException>(() =>
            client.AuthorizeAsync(BankRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task AuthorizeAsync_WhenRequestTimesOut_ThrowsAcquiringBankException()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new OperationCanceledException("timed out")));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<AcquiringBankException>(() =>
            client.AuthorizeAsync(BankRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task AuthorizeAsync_WhenCallerCancels_PropagatesCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var handler = new StubHttpMessageHandler((_, cancellationToken) =>
            Task.FromCanceled<HttpResponseMessage>(cancellationToken));
        var client = CreateClient(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.AuthorizeAsync(BankRequest(), cancellationSource.Token));
    }

    private static AcquiringBankClient CreateClient(HttpMessageHandler handler)
    {
        return new AcquiringBankClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://bank/") },
            NullLogger<AcquiringBankClient>.Instance,
            new PaymentMetrics());
    }

    private static AcquiringBankPaymentRequest BankRequest()
    {
        return new AcquiringBankPaymentRequest(
            "2222405343248877",
            4,
            2027,
            "GBP",
            100,
            "123");
    }

    private static HttpResponseMessage JsonResponse(
        HttpStatusCode statusCode,
        string body)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return handler(request, cancellationToken);
        }
    }
}