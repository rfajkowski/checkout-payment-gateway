using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PaymentGateway.Api.AcquiringBank;
using PaymentGateway.Api.Payments.Models;
using PaymentGateway.Api.Tests.Payments;

namespace PaymentGateway.Api.Tests.Api;

public sealed class PaymentsApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public PaymentsApiTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task PostThenGet_ReturnsOnlySafePaymentDetails()
    {
        using var client = CreateClient(true);
        var post = await client.PostAsJsonAsync("/api/payments", PaymentRequestFactory.Valid());
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        var payment = await post.Content.ReadFromJsonAsync<PostPaymentResponse>();
        Assert.NotNull(payment);
        Assert.Equal("8877", payment!.CardNumberLastFour);
        var get = await client.GetAsync($"/api/payments/{payment.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var body = await get.Content.ReadAsStringAsync();
        Assert.DoesNotContain("2222405343248877", body);
        using var document = JsonDocument.Parse(body);
        Assert.False(document.RootElement.TryGetProperty("cardNumber", out _));
        Assert.False(document.RootElement.TryGetProperty("cvv", out _));
    }

    [Fact]
    public async Task GetPaymentAsync_ForUnknownId_ReturnsNotFound()
    {
        using var client = CreateClient(true);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/payments/{Guid.NewGuid()}")).StatusCode);
    }

    private HttpClient CreateClient(bool authorized) => _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
    {
        services.RemoveAll<IAcquiringBankClient>();
        services.AddSingleton<IAcquiringBankClient>(new FakeBankClient(authorized));
    })).CreateClient();
}
