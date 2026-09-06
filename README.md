# Payment Gateway

A deliberately small .NET 8 implementation of the Checkout.com gateway challenge. It accepts card payments and retrieves completed payments. The design keeps merchant validation, the acquiring-bank HTTP boundary, and storage separate without adding framework-heavy architecture.

## Run

Start the supplied bank simulator from the repository root:

```powershell
docker compose up
```

In another terminal, run the API:

```powershell
dotnet run --project src/PaymentGateway.Api
```

The acquiring-bank URL is configuration (`AcquiringBank:BaseUrl`) and defaults to `http://localhost:8080/`. The health endpoint is `GET /health`.

## API

`POST /api/payments` accepts:

```json
{
  "cardNumber": "2222405343248877",
  "expiryMonth": 4,
  "expiryYear": 2027,
  "currency": "GBP",
  "amount": 100,
  "cvv": "123"
}
```

Valid requests return `201 Created` with either `Authorized` or `Declined`. Both outcomes are persisted and can be retrieved with `GET /api/payments/{id}`. An unknown ID returns `404`. Invalid merchant input returns `400` with `status: Rejected`; it is never sent to the bank. Bank technical failures return `502` and are deliberately not represented as a decline or rejection.

The response exposes only the last four card digits. Full PAN and CVV are neither returned nor persisted.

## Tests

```powershell
dotnet test PaymentGateway.sln
```

The suite covers validation and payment-workflow unit tests, bank JSON contract serialization through a fake HTTP handler, and an end-to-end API POST-then-GET flow through the ASP.NET test host.

## Design decisions and assumptions

- Validation follows the stated contract: 14–19 ASCII digits for PAN, a 3–4 digit CVV, a future expiry month/year, integer amount, and three supported ISO codes (`GBP`, `USD`, `EUR`). The specification does not require `amount > 0`, so that rule was intentionally not added.
- Luhn validation is intentionally omitted: it would silently strengthen the supplied merchant contract.
- The expiry card is usable through the final day of its expiry month.
- The in-memory concurrent dictionary is sufficient for this exercise, but is process-local. Production would use shared durable storage before horizontal scaling.
- No automatic authorization retries are attempted. After a bank timeout/503, an outcome may be unknown; blind retrying can duplicate a payment without compatible downstream idempotency and reconciliation semantics.
- Idempotency, merchant authentication, tokenization/vaulting, durable storage, tracing/metrics, and resilient hosting are production follow-ups rather than unstated scope added to the challenge.

## Observability and packaging

The API logs a payment ID and its business result, and logs acquiring-bank failures as errors. It intentionally never logs raw request data, PAN, or CVV. `src/PaymentGateway.Api/Dockerfile` provides a small multi-stage .NET 8 container build. In production, add structured metrics/traces, secret management, health probes, rate limiting, and PCI-appropriate card tokenization where reusable credentials are needed.
