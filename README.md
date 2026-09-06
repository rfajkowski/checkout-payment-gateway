# Payment Gateway

.NET 8 implementation of the Checkout.com payment gateway challenge.

The API processes card payments through the supplied bank simulator and retrieves previously processed payments by ID.

## Run locally

Start the bank simulator:

```powershell
docker compose up bank_simulator
```

In another terminal, start the API:

```powershell
dotnet run --project src/PaymentGateway.Api
```

The bank URL is configured through `AcquiringBank:BaseUrl` and defaults to `http://localhost:8080/`.

To run the API and bank together:

```powershell
docker compose up --build
```

The API is available at `http://localhost:8081` and the bank simulator at `http://localhost:8080`. Inside Compose, the API calls `http://bank_simulator:8080/`.

To build and run the API container directly while the bank runs on the host:

```powershell
docker build -f src/PaymentGateway.Api/Dockerfile -t checkout-payment-gateway .
docker run --rm -p 8081:8080 `
  -e AcquiringBank__BaseUrl=http://host.docker.internal:8080/ `
  checkout-payment-gateway
```

## API

### POST /api/payments

Example request:

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

The amount is an integer in minor currency units. For example, GBP `10.50` is sent as `1050`.

Possible responses:

- `201 Created` — the bank returned `Authorized` or `Declined`;
- `400 Bad Request` — gateway validation failed;
- `502 Bad Gateway` — no valid bank response was received.

A `201` response includes a `Location` header for the created payment.

### GET /api/payments/{id}

Returns the stored payment or `404 Not Found`. Only the final four card digits are returned. Full PAN and CVV are not stored.

### GET /health

A basic liveness endpoint. It does not check the bank simulator.

## Tests

```powershell
dotnet test PaymentGateway.sln
```

The test suite covers validation, payment processing, repository behaviour, the bank HTTP contract and API behaviour. Docker is not required to run it.

## Design

The request flow is:

```text
PaymentsController
    -> PaymentService
        -> PaymentValidator
        -> IAcquiringBankClient
        -> IPaymentRepository
```

I used interfaces for the bank and repository because both can be replaced independently. `PaymentService` and `PaymentValidator` each have one implementation, so they remain concrete classes as adding abstraction does not add any benefit.

The repository uses an in-memory `ConcurrentDictionary`, which is enough for this exercise.

## Assumptions

- Cards are valid through their expiry month. For example, `09/2026` remains valid during September 2026.
- Supported currencies are `GBP`, `USD` and `EUR`. Input is case-insensitive and stored in uppercase.
- Invalid requests are not sent to the bank and are not stored.
- A bank failure is a technical failure rather than a decline because no authorization decision was received.
- Only the last four card digits are stored. Full PAN and CVV are used only to call the bank.
- Card-number validation follows the brief: 14–19 ASCII digits. I did not add Luhn validation as it is not part of the requirments.
- The specs does not define an amount range, so I did not add a minimum or maximum.

## Out of scope

The challenge does not require:

- merchant authentication;
- durable shared storage;
- idempotency;
- card tokenization or vaulting;
- rate limiting;
- retry and reconciliation workflows;
- monitoring exporters or alerting infrastructure.

## Observability

The API uses structured logs and `System.Diagnostics.Metrics`.

Logs include validation rejection counts, completed payment ID/status, and bank outcome, duration and failures. PAN, CVV and raw payment payloads are not logged. Declines are logged at `Information` because they are normal business results.

The application emits:

- `payments.processed` with `status` and `currency` tags;
- `payments.rejected`;
- `acquiring_bank.requests` with an `outcome` tag;
- `acquiring_bank.failures` with a bounded `reason` tag;
- `acquiring_bank.duration` in milliseconds.

The tags are low-cardinality. A deployed service would connect these instruments to its monitoring or OpenTelemetry pipeline; this project does not include an exporter.

## Production considerations

### Persistence and scaling

The in-memory repository is process-local. Multiple API replicas would need shared durable storage  with appropriate consistency, audit and availability guarantees before horizontal scaling behind a load balancer.

### Idempotency

A payment API would normally use an `Idempotency-Key` header backed by shared storage. This does not solve every retry case: if the bank processes a request but its response is lost, safe retrying requires compatible bank idempotency or reconciliation. This implementation does not automatically retry authorizations.

### Card data

If reusable card credentials were required, I would use tokenization or card vaulting rather than storing PAN in this service. CVV would not be persisted.

A deployed service would also use managed secrets and configuration, readiness checks, TLS termination, and the platform's standard HTTP tracing and metrics.

### Resilience, hosting and operations

A deployed version would set an explicit timeout for bank calls and reconcile payments whose outcome is unknown. It would run behind TLS termination, with secrets and configuration managed by the hosting platform rather than stored in source control.

I would use separate liveness and readiness checks, and connect the existing metrics to the platform's monitoring and alerting tools. Standard HTTP tracing and metrics would provide visibility across the merchant request and bank call.
