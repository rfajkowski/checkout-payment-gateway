using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using PaymentGateway.Api.AcquiringBank;
using PaymentGateway.Api.Payments.Models;

namespace PaymentGateway.Api.Tests.Api;

public sealed class PaymentsApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PaymentsApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostPayment_WhenAuthorized_ReturnsCreatedResourceWithoutSensitiveData()
    {
        using var client = CreateClient(new FakeBankClient(true));

        var response = await client.PostAsJsonAsync(
            "/api/payments",
            PaymentRequestFactory.Valid());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payment = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();
        Assert.NotNull(payment);
        Assert.Equal("Authorized", payment!.Status.ToString());
        Assert.Equal("8877", payment.CardNumberLastFour);
        Assert.Equal($"/api/payments/{payment.Id}", response.Headers.Location!.AbsolutePath);

        var responseBody = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(responseBody);
        Assert.Equal("Authorized", document.RootElement.GetProperty("status").GetString());
        Assert.False(document.RootElement.TryGetProperty("cardNumber", out _));
        Assert.False(document.RootElement.TryGetProperty("cvv", out _));
        Assert.DoesNotContain("2222405343248877", responseBody);
    }

    [Fact]
    public async Task PostPayment_WhenDeclined_ReturnsCreatedDeclinedResource()
    {
        using var client = CreateClient(new FakeBankClient(false));

        var response = await client.PostAsJsonAsync(
            "/api/payments",
            PaymentRequestFactory.Valid());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payment = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();
        Assert.Equal("Declined", payment!.Status.ToString());
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task PostPayment_WhenRejected_ReturnsValidationErrors()
    {
        var bank = new FakeBankClient(true);
        using var client = CreateClient(bank);

        var response = await client.PostAsJsonAsync(
            "/api/payments",
            new PostPaymentRequest());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var rejection = await response.Content.ReadFromJsonAsync<RejectedPaymentResponse>();
        Assert.Equal("Rejected", rejection!.Status);
        Assert.NotEmpty(rejection.Errors!);
        Assert.Equal(0, bank.CallCount);
    }

    [Fact]
    public async Task PostPayment_WhenBankUnavailable_ReturnsBadGatewayProblemDetails()
    {
        using var client = CreateClient(new FailingBankClient());

        var response = await client.PostAsJsonAsync(
            "/api/payments",
            PaymentRequestFactory.Valid());

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(StatusCodes.Status502BadGateway, problem!.Status);
        Assert.Equal("Acquiring bank request failed.", problem.Title);
    }

    [Fact]
    public async Task GetPayment_WhenPaymentExists_ReturnsPersistedPayment()
    {
        using var client = CreateClient(new FakeBankClient(true));
        var postResponse = await client.PostAsJsonAsync(
            "/api/payments",
            PaymentRequestFactory.Valid());
        var created = await postResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        var response = await client.GetAsync($"/api/payments/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payment = await response.Content.ReadFromJsonAsync<GetPaymentResponse>();
        Assert.Equal(created, new PostPaymentResponse(
            payment!.Id,
            payment.Status,
            payment.CardNumberLastFour,
            payment.ExpiryMonth,
            payment.ExpiryYear,
            payment.Currency,
            payment.Amount));
    }

    [Fact]
    public async Task GetPayment_WhenPaymentDoesNotExist_ReturnsNotFoundProblemDetails()
    {
        using var client = CreateClient(new FakeBankClient(true));

        var response = await client.GetAsync($"/api/payments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(StatusCodes.Status404NotFound, problem!.Status);
        Assert.Equal("Payment not found.", problem.Title);
    }

    private HttpClient CreateClient(IAcquiringBankClient bankClient)
    {
        return _factory
            .WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IAcquiringBankClient>();
                    services.AddSingleton(bankClient);
                }))
            .CreateClient();
    }
}