# Payment Gateway

A deliberately small .NET 8 implementation of the Checkout.com payment gateway challenge. It validates and processes card payments through the supplied acquiring-bank simulator and retrieves completed payment details.

## Run locally

Prerequisites:

- .NET 8 SDK or a later SDK capable of targeting .NET 8;
- Docker, for the supplied bank simulator.

Start only the bank simulator:

```powershell
docker compose up bank_simulator
```

In another terminal, start the API:

```powershell
dotnet run --project src/PaymentGateway.Api
```

The bank URL is configured through `AcquiringBank:BaseUrl` and defaults to `http://localhost:8080/`. The API URLs are printed by `dotnet run` and are also defined in `launchSettings.json`.

### Run the complete stack with Compose

```powershell
docker compose up --build
```

This starts:

- the payment gateway at `http://localhost:8081`;
- the bank simulator at `http://localhost:8080`.

The API container uses `http://bank_simulator:8080/` for container-to-container communication.

### Build and run the API container directly

With the bank simulator running on the host:

```powershell
docker build -f src/PaymentGateway.Api/Dockerfile -t checkout-payment-gateway .
docker run --rm -p 8081:8080 `
  -e AcquiringBank__BaseUrl=http://host.docker.internal:8080/ `
  checkout-payment-gateway
```

## API

### Process a payment

`POST /api/payments`

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

The amount is an integer in minor currency units. For example, GBP `10.50` is submitted as `1050`.

Outcomes:

- `201 Created` with status `Authorized` or `Declined`; the `Location` header identifies the created payment resource;
- `400 Bad Request` with status `Rejected` and validation errors;
- `502 Bad Gateway` with `ProblemDetails` when no valid acquiring-bank decision was received.

### Retrieve a payment

`GET /api/payments/{id}` returns `200 OK` for a stored payment or `404 Not Found` with `ProblemDetails` when the ID is unknown.

Merchant responses contain only the final four card digits. Full PAN and CVV are never returned.

### Health

`GET /health` is a basic ASP.NET liveness endpoint. It intentionally does not depend on the bank simulator; dependency health and production readiness would be separate operational signals.

## Tests

```powershell
dotnet test PaymentGateway.sln
```

The normal suite does not require Docker. It includes:

- validator boundary tests with deterministic time;
- payment workflow unit tests;
- acquiring-bank HTTP contract and failure tests using a fake message handler;
- repository behaviour tests;
- API tests using `WebApplicationFactory` and deterministic bank substitutes.

## Design

The request flow is intentionally direct:

```text
PaymentsController
    -> PaymentService
        -> PaymentValidator
        -> IAcquiringBankClient
        -> IPaymentRepository
```

The bank and repository interfaces represent genuine external boundaries. The validator and service have one implementation each, so additional interfaces would add indirection without a current testing or substitution benefit.

The in-memory repository stores only data required by the retrieval API and uses a concurrent dictionary. It is an exercise-appropriate substitute for persistence, not a production database.

## Assumptions

- A card remains valid throughout its expiry month. For example, `09/2026` is valid during September 2026.
- The supported currencies are GBP, USD and EUR. Input is case-insensitive and is normalized to uppercase.
- A request that fails gateway validation is neither submitted to the bank nor persisted.
- A downstream failure is a technical failure, not a decline, because no authorization decision was received. It maps to `502 Bad Gateway`.
- Full PAN and CVV are used only while validating and submitting the bank request. Only the required last four PAN digits are persisted.
- Card-number validation follows the challenge contract of 14–19 ASCII numeric characters. Luhn validation is not added because it would strengthen the supplied contract.
- The challenge specifies an integer amount in minor units but no range. No minimum or maximum amount rule is added.

## Out of scope

The following production concerns are intentionally not implemented because they are outside the exercise requirements:

- merchant authentication and authorization;
- durable shared persistence;
- idempotency;
- card tokenization or vaulting;
- rate limiting;
- Luhn and card-scheme validation;
- authorization retries and reconciliation workflows;
- tracing exporters and production alerting infrastructure.

## Observability

The gateway uses structured logging and built-in `System.Diagnostics.Metrics` instruments.

### Logging

The application logs:

- validation rejection counts;
- completed payment IDs and statuses;
- acquiring-bank outcomes, technical failures and request duration.

It does not log full card numbers, CVVs, or raw merchant/bank request bodies. Declined payments are normal business outcomes and are logged at `Information`, not as application errors.

### Metrics

The application defines:

- `payments.processed`, tagged by status and currency;
- `payments.rejected`;
- `acquiring_bank.requests`, tagged by outcome;
- `acquiring_bank.failures`, tagged by a bounded reason;
- `acquiring_bank.duration`, recorded in milliseconds.

Metric dimensions are deliberately low-cardinality. Payment IDs, trace IDs, card data and arbitrary exception messages are not metric tags. A production environment would attach its chosen OpenTelemetry or platform exporter and add standard ASP.NET/runtime metrics and distributed tracing.

## Production considerations

### Persistence and scaling

The current repository is process-local, so multiple API replicas would not share state. Production requires shared durable storage with appropriate consistency, audit and availability guarantees before horizontal scaling behind a load balancer.

### Idempotency and unknown outcomes

A production API would accept an `Idempotency-Key` header, atomically reserve the key with a request fingerprint, return the original result for an identical repeat, reject reuse with a different request, and apply a retention policy in shared durable storage.

Gateway-side idempotency alone cannot guarantee exactly-once processing if the bank processed a request but its response was lost. Safe retries require compatible downstream idempotency or reconciliation semantics. This implementation therefore does not blindly retry authorization requests.

### Card data

If reusable card credentials were required, a PCI-appropriate tokenization or card-vault reference would be preferable to storing encrypted PAN in the gateway database. Encryption does not justify retaining data that the gateway does not need, and CVV must never be persisted.

### Resilience, hosting and operations

A production deployment would add explicit timeout policy, reconciliation for unknown outcomes, carefully justified circuit breaking, environment-based secrets management, TLS termination, readiness probes, multiple replicas and rolling deployment controls. Distributed traces would correlate the merchant request with the acquiring-bank dependency call without recording sensitive data.
