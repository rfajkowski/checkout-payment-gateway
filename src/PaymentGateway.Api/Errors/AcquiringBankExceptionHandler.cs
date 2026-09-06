using Microsoft.AspNetCore.Diagnostics;

using PaymentGateway.Api.AcquiringBank;

namespace PaymentGateway.Api.Errors;

public sealed class AcquiringBankExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not AcquiringBankException)
        {
            return false;
        }

        await Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Acquiring bank request failed.")
            .ExecuteAsync(httpContext);

        return true;
    }
}