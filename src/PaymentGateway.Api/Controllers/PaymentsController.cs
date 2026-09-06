using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Payments;
using PaymentGateway.Api.Payments.Models;
using PaymentGateway.Api.Persistence;

namespace PaymentGateway.Api.Controllers;

[Route("api/payments")]
[ApiController]
public sealed class PaymentsController(
    PaymentService paymentService,
    IPaymentRepository repository) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(PostPaymentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(RejectedPaymentResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<PostPaymentResponse>> ProcessPaymentAsync(
        PostPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await paymentService.ProcessAsync(request, cancellationToken);
        if (result.IsRejected)
        {
            return BadRequest(new RejectedPaymentResponse("Rejected", result.Errors));
        }

        var payment = result.Payment!;

        return CreatedAtRoute(
            "GetPayment",
            new { id = payment.Id },
            ToPostResponse(payment));
    }

    [HttpGet("{id:guid}", Name = "GetPayment")]
    [ProducesResponseType(typeof(GetPaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public ActionResult<GetPaymentResponse> GetPayment(Guid id)
    {
        var payment = repository.Get(id);
        if (payment is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Payment not found.");
        }

        return Ok(
            new GetPaymentResponse(
                payment.Id,
                payment.Status,
                payment.CardNumberLastFour,
                payment.ExpiryMonth,
                payment.ExpiryYear,
                payment.Currency,
                payment.Amount));
    }

    private static PostPaymentResponse ToPostResponse(Payment payment)
    {
        return new PostPaymentResponse(
            payment.Id,
            payment.Status,
            payment.CardNumberLastFour,
            payment.ExpiryMonth,
            payment.ExpiryYear,
            payment.Currency,
            payment.Amount);
    }
}