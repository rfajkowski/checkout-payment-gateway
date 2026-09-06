# Checkout.com Payment Gateway Take-Home — Codex Implementation Guide

> Working implementation brief for the Checkout.com `.NET` payment gateway challenge.
>
> **Purpose:** give Codex enough context to help implement the solution without inventing requirements, over-engineering the architecture, or losing sight of what will be discussed in the interview.
>
> **Primary principle:** implement the smallest production-minded solution that fully satisfies the stated requirements and is easy to explain. Prefer KISS. Apply SOLID where it creates a real boundary or testing benefit; do not add abstractions merely to demonstrate SOLID.

---

## 1. Source of truth

The requirements come from:

- Checkout.com recruitment assessment:
  - https://github.com/cko-recruitment/
- .NET starter repository:
  - https://github.com/cko-recruitment/payment-gateway-challenge-dotnet

When this guide conflicts with the written Checkout.com requirements, **the Checkout.com requirements win**.

When the requirements are ambiguous:

1. Do not silently invent a business rule.
2. Identify the ambiguity.
3. Prefer the smallest reasonable behaviour.
4. Add the decision to the README under assumptions/design decisions.
5. If the choice could materially change acceptance behaviour, flag it before implementing it.

---

## 2. Interview context

This is the **offline/take-home** variation.

The implementation will later be demonstrated and reviewed with a Checkout.com Senior Engineer. During the interview, I will lead the exercise while the interviewer asks questions and probes decisions.

The competencies Checkout.com told me they will assess are:

1. **Observability**
2. **Packaging / Hosting**
3. **Technical Documentation**
4. **API Design**
5. **Code Design**
6. **Testing Mindset**

Therefore, do not optimize only for "making the tests green". The codebase should give me good, defensible design decisions to discuss across all six competencies.

---

## 3. Checkout.com's explicit implementation expectations

Checkout.com states that:

- Code must compile.
- Code must be covered by automated tests.
- The type and number of tests are left to the candidate.
- Code should be **simple and maintainable**.
- They explicitly do **not** want to encourage over-engineering.
- API design and architecture should focus on the stated functional requirements.
- For the offline exercise, key design considerations and assumptions must be documented.
- A real database/storage engine is **not required**; the supplied in-memory/test-double repository can represent storage.

These points should drive all implementation decisions.

---

# 4. Functional requirements

There are only two required merchant capabilities:

1. Process a card payment.
2. Retrieve the details of a previously made payment using its identifier.

---

## 4.1 Processing a payment

A merchant submits a payment request containing:

| Field | Required validation |
|---|---|
| Card number | Required |
| Card number | 14–19 characters |
| Card number | Numeric characters only |
| Expiry month | Required |
| Expiry month | 1–12 |
| Expiry year | Required |
| Expiry | Month + year combination must be in the future |
| Currency | Required |
| Currency | Exactly 3 characters |
| Currency | Validate against **no more than 3** ISO currency codes |
| Amount | Required |
| Amount | Integer |
| CVV | Required |
| CVV | 3–4 characters |
| CVV | Numeric characters only |

Amount is supplied in **minor currency units**:

- USD $0.01 -> `1`
- USD $10.50 -> `1050`

### Important: do not add unstated validation rules casually

The specification does **not** explicitly require:

- Luhn validation.
- Card-scheme/IIN/BIN validation.
- `Amount > 0`.
- A maximum amount.
- Currency-specific minor-unit rules.
- Cardholder name.
- Billing address.
- 3DS.
- Authentication/authorization of the merchant.
- Idempotency.

Some of these would matter in a production payment system, but they are outside the stated exercise unless explicitly adopted as a documented assumption.

---

## 4.2 Payment outcomes

A merchant should receive one of three conceptual outcomes:

### `Authorized`

- Gateway validation succeeded.
- Request was sent to the acquiring bank.
- Acquiring bank authorized the payment.

### `Declined`

- Gateway validation succeeded.
- Request was sent to the acquiring bank.
- Acquiring bank declined the payment.
- This is a **business outcome**, not an application/system error.

### `Rejected`

Checkout explicitly defines this as:

- Invalid information was supplied to the payment gateway.
- No payment could be created.
- The gateway rejects the request.
- The acquiring bank must **not** be called.

This distinction is important in the code, tests, logging, API behaviour, and interview explanation.

---

## 4.3 Successful payment response

For payments that were successfully sent to the acquiring bank, the merchant response must contain:

- Payment `Id`.
- `Status`, either `Authorized` or `Declined`.
- Last four card digits.
- Expiry month.
- Expiry year.
- Currency.
- Amount.

A GUID is explicitly acceptable as the payment identifier.

The full card number must not be returned.

---

## 4.4 Retrieving a payment

The merchant must be able to retrieve a previously made payment by ID.

The response should contain:

- Id.
- Status.
- Last four card digits / masked card information.
- Expiry month.
- Expiry year.
- Currency.
- Amount.

The supplied starter tests already indicate that an unknown payment should return HTTP `404 Not Found`.

---

# 5. Bank simulator contract

The supplied simulator uses Mountebank.

Start it from the repository root:

```powershell
docker compose up
```

The README uses the legacy spelling:

```text
docker-compose up
```

With current Docker Desktop, `docker compose up` is appropriate.

The simulator endpoint is:

```http
POST http://localhost:8080/payments
```

Expected JSON request:

```json
{
  "card_number": "2222405343248877",
  "expiry_date": "04/2027",
  "currency": "GBP",
  "amount": 100,
  "cvv": "123"
}
```

Expected successful response shape:

```json
{
  "authorized": true,
  "authorization_code": "0bb07405-6d44-4b50-a14f-7ae0beff13ad"
}
```

---

## 5.1 Simulator behaviour

The simulator is deterministic:

### Missing required bank-request field

Returns:

```http
400 Bad Request
```

### Card number ending in odd digit

Ending in:

```text
1, 3, 5, 7, 9
```

returns:

```http
200 OK
```

with:

```json
{
  "authorized": true,
  "authorization_code": "<generated-guid>"
}
```

### Card number ending in non-zero even digit

Ending in:

```text
2, 4, 6, 8
```

returns:

```http
200 OK
```

with:

```json
{
  "authorized": false,
  "authorization_code": ""
}
```

### Card number ending in `0`

Returns:

```http
503 Service Unavailable
```

This is a **technical/downstream failure**, not a `Declined` payment and not a gateway-validation `Rejected` payment.

Do not collapse these distinct concepts.

---

# 6. Existing .NET starter project

The starter targets:

```text
.NET 8
```

with nullable reference types and implicit usings enabled.

Current high-level structure:

```text
src/
└── PaymentGateway.Api/
    ├── Controllers/
    │   └── PaymentsController.cs
    ├── Enums/
    │   └── PaymentStatus.cs
    ├── Models/
    │   ├── Requests/
    │   │   └── PostPaymentRequest.cs
    │   └── Responses/
    │       ├── GetPaymentResponse.cs
    │       └── PostPaymentResponse.cs
    ├── Services/
    │   └── PaymentsRepository.cs
    ├── Program.cs
    └── PaymentGateway.Api.csproj

test/
└── PaymentGateway.Api.Tests/
    ├── PaymentsControllerTests.cs
    └── PaymentGateway.Api.Tests.csproj

imposters/
└── bank_simulator.ejs

docker-compose.yml
```

The provided code is **scaffolding**, not an architecture that must be preserved.

The challenge explicitly permits changing the structure.

---

# 7. Problems/gaps already present in the starter

These should be fixed deliberately.

## 7.1 `PostPaymentRequest` does not match the requirements

Starter currently uses:

```csharp
public int CardNumberLastFour { get; set; }
public int ExpiryMonth { get; set; }
public int ExpiryYear { get; set; }
public string Currency { get; set; }
public int Amount { get; set; }
public int Cvv { get; set; }
```

Problems:

### It has only `CardNumberLastFour`

The gateway needs the **full card number** to validate the 14–19 digit requirement and send the request to the acquiring bank.

The input must therefore contain the full card number.

### Card number must be represented as a string

It is a digit sequence/identifier, not a number used in arithmetic.

Using a string:

- preserves leading zeroes;
- makes length validation straightforward;
- avoids numeric size/overflow issues;
- matches the bank simulator contract.

### CVV must be represented as a string

Example:

```text
"012"
```

is a three-character CVV.

Representing it as an `int` converts it conceptually to `12` and loses the leading zero, which makes the required 3–4 character validation incorrect.

### Last four must also be a string

A last-four value such as:

```text
"0012"
```

must not become:

```text
12
```

---

## 7.2 Repository currently stores API response objects

The starter repository stores:

```csharp
List<PostPaymentResponse>
```

This couples persistence to an HTTP response DTO.

Prefer storing an internal `Payment` model/entity and mapping it to API responses.

---

## 7.3 Repository currently exposes a mutable public `List`

The repository is registered as a singleton but uses a mutable `List<T>`.

That is not a good concurrent storage structure for a web application.

Prefer:

```csharp
ConcurrentDictionary<Guid, Payment>
```

This also provides average O(1) lookup by payment ID rather than scanning a list.

Do not add a real database: it is explicitly unnecessary for the exercise.

---

## 7.4 Existing GET implementation returns `200` with null

The starter controller currently performs a lookup and returns `Ok(...)` regardless of whether the payment exists.

The supplied starter test expects:

```http
404 Not Found
```

when the payment cannot be found.

Fix this.

---

## 7.5 Existing enum JSON needs attention

`PaymentStatus` is an enum.

By default, ASP.NET/System.Text.Json may serialize enums numerically unless configured otherwise.

The external contract should return readable values such as:

```json
{
  "status": "Authorized"
}
```

not:

```json
{
  "status": 0
}
```

Configure string enum serialization, e.g. `JsonStringEnumConverter`.

---

## 7.6 Starter test package version

The API project targets `net8.0`.

The starter test project also targets `net8.0`, but currently references:

```text
Microsoft.AspNetCore.Mvc.Testing 6.0.24
```

Do not perform dependency churn just for appearance, but if integration-test behaviour or compatibility causes issues, align this package with an appropriate .NET 8 version and document/keep the change focused.

---

# 8. Implementation philosophy

## 8.1 KISS first

The code should look intentionally simple.

Do **not** create abstractions because an architecture diagram looks more sophisticated.

The interviewer should be able to open the main processing code and understand the payment flow immediately.

---

## 8.2 SOLID as a design guide, not an interface quota

Use SOLID where it solves an actual problem.

Good separation:

```text
Controller
    HTTP/API concerns

PaymentService
    payment-processing workflow/orchestration

AcquiringBankClient
    downstream HTTP integration

PaymentRepository
    persistence abstraction/storage

PaymentValidator
    stated merchant-input rules
```

### Recommended interfaces

Use an interface for the external bank boundary:

```text
IAcquiringBankClient
```

This is justified because:

- it wraps an external system;
- it is independently replaceable;
- unit tests need deterministic bank outcomes;
- failure behaviour is important.

An interface for persistence is also defensible/recommended:

```text
IPaymentRepository
```

The exercise explicitly says the in-memory repository stands in for real persistence, so this is a genuine boundary.

### Do not add interfaces without a reason

Do not create these merely for SOLID:

```text
IPaymentService
IPaymentValidator
IPaymentMapper
IPaymentFactory
```

unless the implementation later creates a concrete reason.

One implementation alone is not an automatic reason for an interface.

---

## 8.3 Avoid speculative architecture

Do not introduce without a concrete requirement:

- Clean Architecture with multiple projects/layers.
- CQRS.
- MediatR.
- Commands and handlers for two endpoints.
- Generic repository.
- Unit of Work.
- DDD aggregates/value objects everywhere.
- Factories/builders with one implementation.
- Event bus/message broker.
- Database/EF Core.
- Redis/cache.
- AutoMapper solely to avoid trivial explicit mapping.
- Kubernetes/Terraform.
- Complex resilience packages.
- Retry policies for payment authorization.
- Authentication system.
- Idempotency implementation.
- Luhn validation.

These can be discussed as production extensions where relevant.

---

# 9. Recommended project structure

Keep the solution small. A single API project plus tests is enough.

Recommended structure:

```text
src/
└── PaymentGateway.Api/
    ├── Controllers/
    │   └── PaymentsController.cs
    │
    ├── Payments/
    │   ├── Payment.cs
    │   ├── PaymentStatus.cs
    │   ├── PaymentService.cs
    │   ├── PaymentValidator.cs
    │   └── Models/
    │       ├── PostPaymentRequest.cs
    │       ├── PaymentResponse.cs
    │       └── RejectedPaymentResponse.cs
    │
    ├── AcquiringBank/
    │   ├── IAcquiringBankClient.cs
    │   ├── AcquiringBankClient.cs
    │   ├── AcquiringBankOptions.cs
    │   ├── BankPaymentRequest.cs
    │   └── BankPaymentResponse.cs
    │
    ├── Persistence/
    │   ├── IPaymentRepository.cs
    │   └── InMemoryPaymentRepository.cs
    │
    ├── Program.cs
    ├── appsettings.json
    └── PaymentGateway.Api.csproj
```

Tests:

```text
test/
└── PaymentGateway.Api.Tests/
    ├── Payments/
    │   ├── PaymentValidatorTests.cs
    │   └── PaymentServiceTests.cs
    │
    ├── AcquiringBank/
    │   └── AcquiringBankClientTests.cs
    │
    └── Api/
        └── PaymentsApiTests.cs
```

This is guidance, not a requirement. Do not reorganize files merely to satisfy this tree if the current implementation is already cleaner with fewer folders.

---

# 10. Recommended data models

## 10.1 Merchant request

Conceptually:

```csharp
public sealed class PostPaymentRequest
{
    public string? CardNumber { get; init; }
    public int? ExpiryMonth { get; init; }
    public int? ExpiryYear { get; init; }
    public string? Currency { get; init; }
    public int? Amount { get; init; }
    public string? Cvv { get; init; }
}
```

Why nullable value types may be useful:

The specification says fields are **required**.

With a plain non-nullable:

```csharp
int Amount
```

a missing JSON property becomes difficult to distinguish from an explicit `0` if manual validation is used.

Using nullable request fields gives validation enough information to distinguish "missing" from "present".

Alternative ASP.NET model-validation approaches are acceptable if they provide equivalent, clear behaviour.

Do not use nullable types in the internal `Payment` object after successful validation.

---

## 10.2 Internal persisted payment

Conceptually:

```csharp
public sealed class Payment
{
    public Guid Id { get; init; }
    public PaymentStatus Status { get; init; }
    public required string CardNumberLastFour { get; init; }
    public int ExpiryMonth { get; init; }
    public int ExpiryYear { get; init; }
    public required string Currency { get; init; }
    public int Amount { get; init; }
}
```

Do not persist fields that are not required for retrieval merely because they arrived in the original request.

In particular:

- Do not persist CVV.
- Prefer not to persist the full PAN for this exercise.
- Persist only the last four digits required by the merchant-facing retrieval contract.

Rationale: minimize sensitive data and keep persistence scoped to what the exercise requires.

---

## 10.3 Acquiring-bank DTOs are separate contracts

Do not reuse the merchant request DTO as the bank request DTO.

These are two separate boundaries:

```text
Merchant
   ↓ merchant API contract
Payment Gateway
   ↓ bank integration contract
Acquiring Bank
```

The simulator expects snake_case fields:

```json
{
  "card_number": "...",
  "expiry_date": "MM/yyyy",
  "currency": "GBP",
  "amount": 100,
  "cvv": "123"
}
```

Use explicit JSON property names or a serializer policy for the bank DTO.

Do not change the public merchant API contract just because the bank contract changes.

---

# 11. Validation design

Implement only the stated validation rules plus clearly documented assumptions.

A simple `PaymentValidator` is enough.

It does not need an interface.

Return validation errors in a form the controller can convert into a clear `400` response.

---

## 11.1 Card number

Required:

- present;
- 14–19 characters;
- numeric characters only.

Use a string.

Avoid overly permissive checks such as Unicode numeric characters if the intent is ASCII card digits. A simple character check against `'0'..'9'` is clear.

---

## 11.2 Luhn algorithm — explicitly NOT required

Luhn is a checksum algorithm commonly used to detect accidental errors in identification/card-number strings.

It is **not** the same as:

- numeric-only validation;
- length validation;
- checking whether a card account is real;
- checking whether the card is active;
- checking available funds.

### Decision for this exercise

Do **not** add Luhn validation unless we explicitly change this decision.

Reason:

Checkout.com's written requirement defines card-number validation as:

- 14–19 characters;
- numeric only.

Adding Luhn would make the gateway stricter than the stated contract and could reject a 14–19 digit test number that Checkout.com's requirements otherwise treat as valid.

This is not omitted because Luhn is difficult. It is omitted because we should not silently strengthen a business contract.

README wording can be approximately:

> Card-number validation intentionally follows the exercise specification (14–19 numeric characters). Additional scheme-specific validation such as a Luhn checksum is outside the stated scope.

If asked in interview, explain that Luhn could be considered as an additional early validation step in a real system once agreed as a product/contract requirement.

---

## 11.3 Expiry

Requirements:

- month is 1–12;
- expiry month/year combination is in the future.

Make this deterministic in tests.

Prefer using .NET's:

```csharp
TimeProvider
```

rather than inventing `IClock`.

### Recommended assumption

Treat a card as valid through its expiry month, i.e. the current month/year is not expired until the month ends.

Document this assumption because the phrase "in the future" can be interpreted more strictly.

Tests must cover:

- past year;
- past month in current year;
- current month/current year based on the chosen assumption;
- future month/current year;
- future year;
- invalid month 0;
- invalid month 13.

Do not use real wall-clock dates directly in tests.

---

## 11.4 Currency

Validate against **no more than three** currencies.

Recommended set:

```text
GBP
USD
EUR
```

Document the chosen set.

Recommended behaviour:

- accept exact ISO-style uppercase codes;
- reject unsupported values;
- reject values not exactly three characters.

Do not add a package containing every ISO currency if the exercise explicitly asks for no more than three.

If we choose case-insensitive input normalization instead, document it. Do not silently change this behaviour later.

---

## 11.5 Amount

The explicit requirement is:

- required;
- integer;
- minor currency units.

The specification does **not** explicitly say `> 0`.

### Recommended decision

Before enforcing `Amount > 0`, treat it as an explicit assumption and document it.

A positive-only rule is a reasonable payment-domain decision, but it is still stricter than the written exercise.

Codex must not silently introduce amount-range rules.

---

## 11.6 CVV

Required:

- present;
- 3–4 characters;
- numeric characters only.

Use `string`, not `int`.

Never return it.

Do not persist it.

Do not log it.

---

# 12. API design

The exact HTTP status codes are not comprehensively prescribed by the challenge, so choices should be deliberate and documented.

Recommended API:

```http
POST /api/payments
GET  /api/payments/{id}
```

Keeping the starter `/api/Payments` casing is functionally acceptable; prefer a consistent lowercase route contract in docs/examples.

---

## 12.1 POST — Authorized

Recommended:

```http
201 Created
```

Response body includes:

- id;
- `Authorized`;
- last four;
- expiry month;
- expiry year;
- currency;
- amount.

Prefer `CreatedAtAction(...)` so the response includes a `Location` header pointing to:

```text
GET /api/payments/{id}
```

This is a design choice, not an explicit challenge requirement. Document it.

---

## 12.2 POST — Declined

A decline is a successfully processed payment request with a business outcome.

Recommended:

```http
201 Created
```

with:

```json
{
  "status": "Declined"
}
```

plus the required payment fields.

Do not return `400`, `422`, or `500` merely because the payment was declined.

Persist the declined payment so it can be retrieved/reconciled.

---

## 12.3 POST — Rejected

Validation failure:

- do not call bank;
- do not create/persist a payment;
- do not generate a retrievable payment record.

Recommended:

```http
400 Bad Request
```

Use a small, consistent response such as:

```json
{
  "status": "Rejected",
  "errors": {
    "cardNumber": [
      "Card number must contain between 14 and 19 numeric characters."
    ]
  }
}
```

The exact rejected-response schema is not fully prescribed by the challenge. Keep it small and document it.

Do not expose internal exception details.

---

## 12.4 GET — existing payment

Return:

```http
200 OK
```

with the required stored payment representation.

---

## 12.5 GET — missing payment

Return:

```http
404 Not Found
```

This is already expected by the supplied starter test.

A minimal ProblemDetails response is acceptable.

---

# 13. Downstream-bank failure semantics

The simulator deliberately returns `503` when a card number ends in `0`.

This must not be represented as:

```text
Declined
```

because the bank did not give us a decline decision.

It must not be represented as:

```text
Rejected
```

because the gateway request was valid.

This is a technical/downstream failure.

### Recommended handling

Return an HTTP gateway/service error to the merchant, e.g.:

```http
502 Bad Gateway
```

A `503 Service Unavailable` mapping is also defensible. If we choose one, document why and keep it consistent.

The key correctness requirement is semantic:

> Do not invent an `Authorized` or `Declined` payment outcome when the bank outcome is unknown/unavailable.

Recommended storage behaviour:

- do not persist an Authorized/Declined payment record for a bank request that failed technically;
- do not create a normal retrievable successful-payment representation.

---

## 13.1 Do not blindly retry payment authorization

Do **not** add automatic HTTP retries to `POST /payments` just because retry policies are common for downstream HTTP calls.

Reason:

```text
Gateway -> Bank
Bank processes authorization
Network/response fails
Gateway believes request failed
Gateway retries
```

Without idempotency semantics, this can risk duplicate processing.

This is an important interview talking point.

Production considerations:

- idempotency keys;
- bank/provider retry guarantees;
- duplicate-detection semantics;
- unknown payment state;
- reconciliation;
- carefully scoped retries only where safe.

For this exercise, prefer an explicit timeout and clear failure mapping over automatic retries.

---

## 13.2 Idempotency — important production concern, intentionally not implemented by default

Idempotency is highly relevant to a payment-creation endpoint because merchants may retry a request when they do not receive a response.

A typical production API could accept an optional header such as:

```http
POST /api/payments
Idempotency-Key: 8d219268-4c5c-4f76-9e4d-9d83f5f55bf2
```

Prefer a header rather than adding the idempotency key to the payment JSON payload. The payment payload describes the payment itself; idempotency is request-processing metadata.

Making the header optional would also be a backward-compatible extension: clients and hidden tests that do not send it would continue to work.

### What a gateway-side implementation would require

Conceptually:

```text
Idempotency-Key
       |
       v
atomically lookup/reserve key
       |
       +-- new key
       |      -> process request
       |      -> store request fingerprint + resulting response/payment
       |
       +-- existing key + same request
       |      -> return the original result without calling bank again
       |
       +-- existing key + different request
              -> reject conflict (for example 409)
```

A robust implementation would need:

- an idempotency store;
- an atomic reservation/in-progress state so two simultaneous requests with the same key cannot both call the bank;
- a stable fingerprint/hash of the relevant request fields;
- storage of the resulting payment/response against the key;
- defined behaviour for key reuse with a different payload;
- a retention/expiry policy in production;
- behaviour for requests that fail before or after the downstream bank call;
- shared durable storage if the API is horizontally scaled.

A naive sequence such as:

```csharp
if (!store.ContainsKey(key))
{
    await ProcessPayment();
}
```

is not sufficient because two concurrent requests can both pass the check before either stores the result.

### Important limitation of this challenge

Gateway-side idempotency alone cannot guarantee exactly-once processing with the supplied acquiring-bank simulator.

Failure window:

```text
1. Gateway sends authorization request to bank
2. Bank processes/authorizes it
3. Network response is lost or gateway times out
4. Gateway has no confirmed result to persist against the idempotency key
5. Merchant retries the same request
6. Gateway cannot know whether calling the bank again is safe
```

The supplied simulator does not expose an idempotency key or a lookup/reconciliation mechanism.

Therefore, even if we implemented merchant -> gateway idempotency, we must **not claim exactly-once payment processing** end-to-end.

### Decision for this exercise

Do **not** implement idempotency unless we explicitly choose to expand the solution.

Reason:

- it is not part of the supplied API contract;
- implementing it correctly adds meaningful concurrency, storage, expiry, conflict, and failure-state behaviour;
- the downstream simulator cannot provide end-to-end idempotency guarantees;
- documenting the limitation demonstrates the architectural reasoning without adding a partial feature that may imply stronger guarantees than it actually provides.

If we later decide to implement it as an intentional extra feature, use an optional `Idempotency-Key` header rather than changing the payment payload, keep the behaviour backward-compatible, and document the downstream limitation prominently.

Suggested README wording:

> Payment creation is not currently idempotent because idempotency is outside the supplied contract and the acquiring-bank simulator provides no idempotency mechanism. In production, the API would accept an `Idempotency-Key`, atomically associate it with a request fingerprint and resulting payment, return the original result for safe repeats, reject reuse with a different payload, and use shared durable storage. End-to-end retry safety would also require compatible idempotency or reconciliation semantics from the acquiring bank; gateway-side idempotency alone cannot guarantee exactly-once processing when the downstream outcome is unknown.

---

# 14. `AcquiringBankClient`

This is a genuine external boundary.

Recommended:

```csharp
IAcquiringBankClient
AcquiringBankClient
```

Use `HttpClientFactory` / typed `HttpClient`, rather than constructing a new `HttpClient` per request.

Configure the bank base URL externally.

Example configuration:

```json
{
  "AcquiringBank": {
    "BaseUrl": "http://localhost:8080"
  }
}
```

Environment-variable override in containers:

```text
AcquiringBank__BaseUrl=http://bank_simulator:8080
```

Use cancellation tokens through the controller -> service -> bank HTTP call.

Use a sensible, documented timeout.

Do not log bank request bodies because they contain PAN and CVV.

---

## 14.1 Bank request mapping

Map:

```text
CardNumber       -> card_number
ExpiryMonth/Year -> expiry_date formatted MM/yyyy
Currency         -> currency
Amount           -> amount
Cvv              -> cvv
```

Formatting expiry with a two-digit month is important:

```text
4 / 2027 -> "04/2027"
```

---

## 14.2 Bank response mapping

`authorized = true` -> `PaymentStatus.Authorized`.

`authorized = false` -> `PaymentStatus.Declined`.

`authorization_code` is supplied by the simulator but is **not required in the merchant response**.

Do not expose or persist it merely because it exists unless we decide there is a concrete purpose.

---

## 14.3 Unexpected bank `400`

If our gateway has correctly validated and mapped the required request fields, a simulator `400` should generally indicate an integration/programming problem.

Do not map it to merchant `Rejected` automatically.

`Rejected` is specifically gateway validation before the acquiring-bank call.

Treat unexpected downstream statuses as downstream/integration failures and log them safely.

---

# 15. Payment service

`PaymentService` should contain the business orchestration and be easy to read.

Conceptual flow:

```text
Validate merchant request
        |
        +-- invalid
        |      |
        |      -> Rejected result
        |      -> bank NOT called
        |      -> payment NOT persisted
        |
        +-- valid
               |
               -> call acquiring bank
               |
               +-- authorized
               |      -> create Authorized Payment
               |      -> persist
               |
               +-- declined
               |      -> create Declined Payment
               |      -> persist
               |
               +-- technical failure
                      -> propagate/map safe downstream error
                      -> no fake payment outcome
```

Keep this flow visible. Avoid distributing it across handlers/factories unless there is an actual benefit.

---

# 16. Persistence

A simple in-memory repository is explicitly allowed.

Recommended:

```text
IPaymentRepository
InMemoryPaymentRepository
```

Backing storage:

```csharp
ConcurrentDictionary<Guid, Payment>
```

Operations can remain simple:

```text
Add(Payment payment)
Get(Guid id)
```

There is no need to make in-memory operations artificially async just to look database-ready.

If later discussing production, explain that durable persistence would replace this boundary.

---

# 17. Observability

Observability is an explicit interview competency, so implement **some** observability rather than putting all of it in the README.

Keep it proportionate.

---

## 17.1 Structured logging

Use `ILogger<T>` and structured message templates.

Useful events:

### Payment request accepted for processing

Safe context:

- generated/correlation/payment ID where appropriate;
- amount;
- currency.

Do not log full card data.

### Validation rejected

Log:

- that validation failed;
- safe validation categories/count;
- correlation/trace ID.

Avoid logging the request object.

### Acquiring-bank call

Log:

- start/end or outcome;
- duration;
- authorized/declined technical outcome;
- downstream HTTP status on failure;
- trace/correlation/payment ID.

### Payment persisted

Log:

- payment ID;
- status.

### Downstream failure

Log at warning/error level with:

- downstream status;
- exception type;
- duration;
- trace ID.

Do not log sensitive request bodies.

---

## 17.2 Logging levels

Normal business decline:

```text
Information
```

A card decline is not an operational error.

Validation rejection:

```text
Information / Warning
```

Choose one and be consistent; this is normally not an application exception.

Bank 503/timeout:

```text
Warning / Error
```

depending on policy.

Unexpected application exception:

```text
Error
```

---

## 17.3 Sensitive values — never log

Do not log:

- full PAN/card number;
- CVV;
- raw merchant request;
- raw bank request.

Even "debug-only" sensitive logging should be avoided.

If card context is needed, last four may be used cautiously, but payment ID/trace ID should be preferred for correlation.

---

## 17.4 Metrics to discuss/document

It is acceptable to document these rather than build a full metrics stack.

Production metrics worth discussing:

- payment attempts;
- authorization count/rate;
- decline count/rate;
- gateway rejection count/rate;
- acquiring-bank failure count/rate;
- acquiring-bank latency;
- API latency;
- API error rate;
- requests by endpoint/status;
- timeout count.

Important distinction:

A rising **decline rate** may be a business/risk signal.

A rising **bank error rate** is an operational/dependency signal.

Do not treat them as the same metric.

---

## 17.5 Tracing

Production discussion:

```text
merchant request
   -> payment gateway request trace
      -> acquiring bank HTTP dependency span
```

Distributed tracing/OpenTelemetry is a reasonable production enhancement.

Do not introduce a large observability stack purely for the take-home unless there is time and a clear value.

ASP.NET's existing trace identifiers plus structured logs are sufficient for a small implementation if clearly documented.

---

## 17.6 Health endpoint

A basic ASP.NET health endpoint is worthwhile and small:

```text
/health
```

Do not make a basic liveness endpoint fail solely because the bank simulator is unavailable.

If discussing production:

- liveness = is this process alive?
- readiness = can it currently serve traffic/dependencies?
- dependency health may be modeled separately.

Avoid building a complex health system for the exercise.

---

# 18. Packaging / Hosting

Packaging/Hosting is an explicit interview competency.

The starter `docker-compose.yml` currently starts only Mountebank/bank simulator.

At minimum, add a straightforward multi-stage `Dockerfile` for the API.

Example concept:

```text
.NET SDK image
    -> restore
    -> build
    -> publish

ASP.NET runtime image
    -> copy published output
    -> run gateway
```

Do not add Kubernetes/Terraform.

---

## 18.1 Docker Compose decision

Two acceptable approaches:

### Option A — preserve current compose for the bank

Keep:

```text
docker compose up
```

for the simulator and run the API separately.

Add a Dockerfile and document how to run it.

### Option B — add the gateway service to Compose

If doing this, ensure internal Docker networking uses:

```text
http://bank_simulator:8080
```

not:

```text
http://localhost:8080
```

because `localhost` inside the API container refers to the API container itself.

Keep the configuration external via:

```text
AcquiringBank__BaseUrl
```

Do not make local non-container development awkward merely to demonstrate Compose.

---

## 18.2 Hosting discussion

Be ready to explain that the API itself should be stateless except for the exercise's in-memory store.

Important caveat:

The exercise's in-memory repository is process-local.

Therefore horizontal scaling with multiple API replicas would give inconsistent retrieval behaviour.

For production:

```text
multiple gateway instances
       |
       -> shared durable persistence
```

This is an excellent Packaging/Hosting trade-off to mention.

Do **not** solve it by adding a database to the take-home.

---

## 18.3 Configuration and secrets

Use configuration/environment variables for downstream base URLs and timeouts.

Do not hard-code environment-specific URLs into business logic.

There are no real secrets required by the supplied simulator.

Production discussion can mention secret management without inventing credentials for the exercise.

---

# 19. Testing mindset

Testing is a first-class part of the submission.

Tests should prove business behaviour and boundaries, not just chase line coverage.

The automated suite should not require a locally running Docker simulator unless clearly separated as optional end-to-end/smoke tests.

---

## 19.1 Validator tests

Cover at least:

### Card number

- null/missing;
- too short;
- 14 digits valid format;
- 19 digits valid format;
- too long;
- contains letter;
- contains punctuation/space;
- leading zero preservation if applicable.

### Expiry

- month 0;
- month 13;
- missing month/year;
- past year;
- past month in current year;
- current month according to documented assumption;
- future month;
- future year.

Use deterministic time via `TimeProvider`.

### Currency

- supported GBP;
- supported USD;
- supported EUR;
- unsupported code;
- wrong length;
- case behaviour according to documented decision;
- missing.

### Amount

- missing;
- valid integer/minor unit;
- any extra assumption such as zero/negative only if deliberately adopted.

### CVV

- missing;
- 2 digits;
- 3 digits;
- 4 digits;
- 5 digits;
- non-numeric;
- leading zero, e.g. `"012"`.

---

## 19.2 PaymentService tests

Critical behaviours:

### Authorized

Given valid request + bank authorizes:

- bank called exactly once;
- payment status Authorized;
- payment persisted;
- full PAN not persisted;
- CVV not persisted;
- last four correct.

### Declined

Given valid request + bank declines:

- bank called exactly once;
- payment status Declined;
- payment persisted;
- merchant can later retrieve it.

### Rejected

Given invalid request:

- returns Rejected;
- bank is **never called**;
- repository is **never written**.

This is one of the highest-value tests in the solution because it proves a stated product rule.

### Bank technical failure

Given bank unavailable:

- do not map to Declined;
- do not map to Rejected;
- no Authorized/Declined record persisted;
- technical failure is surfaced/mapped safely.

---

## 19.3 AcquiringBankClient tests

Use a fake/custom `HttpMessageHandler` or equivalent self-contained test double.

Test:

- correct endpoint `/payments`;
- HTTP POST;
- correct JSON property names;
- full card number sent;
- expiry formatted `MM/yyyy`;
- amount unchanged;
- CVV remains a string/preserves leading zero;
- authorized response mapped correctly;
- declined response mapped correctly;
- 503 handled as technical failure;
- cancellation/timeout behaviour if implemented.

Avoid adding a heavy mocking package solely for these tests if a small fake is clearer.

---

## 19.4 API/integration tests

Use `WebApplicationFactory`.

Prefer testing the application through HTTP while replacing the acquiring-bank boundary with a deterministic fake.

Cover:

- POST valid/authorized -> chosen success HTTP status + required response.
- POST valid/declined -> chosen success HTTP status + Declined response.
- POST invalid -> `400` + Rejected.
- GET existing -> `200`.
- GET missing -> `404`.
- status enum serialized as strings.
- response does not include full card number.
- response does not include CVV.
- downstream technical failure -> chosen safe `502`/`503`.

If using `WebApplicationFactory<Program>`, top-level `Program` may need to be exposed for the test assembly, commonly via:

```csharp
public partial class Program { }
```

Keep this change minimal.

---

## 19.5 Optional real-simulator smoke test

The supplied Mountebank simulator is useful for manual/optional end-to-end validation.

Exercise manually against cards ending:

```text
...1 -> Authorized
...2 -> Declined
...0 -> bank 503
```

Do not make the normal test suite fragile by requiring developers/reviewers to have Docker running unless explicitly designed as a separate integration-test category.

---

# 20. Technical documentation / README

The README is part of the assessment, not an afterthought.

It should allow a reviewer to understand and run the solution quickly.

Recommended sections:

```text
# Payment Gateway

## Prerequisites

## Running the bank simulator

## Running the API

## Running with Docker

## Running tests

## API
### POST /api/payments
### GET /api/payments/{id}

## Architecture

## Validation rules

## Assumptions

## Design decisions and trade-offs

## Observability

## Failure handling

## Security / sensitive-data handling

## Production considerations

## Out of scope
```

---

## 20.1 README assumptions to document

At minimum document decisions around:

- supported currencies (`GBP`, `USD`, `EUR` if selected);
- whether current expiry month is considered valid;
- whether amount must be positive if that rule is added;
- rejected requests are not persisted;
- full PAN is not persisted;
- CVV is not persisted;
- only the last four PAN digits required by the retrieval API are retained;
- a production requirement to reuse a payment instrument would use tokenization/vaulting rather than casually storing encrypted PAN in the gateway database;
- Luhn intentionally not implemented because it is not in the stated validation contract;
- downstream bank failure does not become Declined;
- chosen HTTP response for bank unavailability;
- no automatic retries;
- idempotency is intentionally not implemented by default, including the downstream limitation that prevents an end-to-end exactly-once guarantee;
- in-memory repository is deliberately used because durable persistence is outside scope.

---

## 20.2 README design decisions to explain

Good examples:

### Why only selected interfaces?

> Interfaces are used at external/replacement boundaries (acquiring bank and persistence). The payment service/validator remain concrete because there is currently no substitution requirement that would justify additional abstractions.

### Why in-memory persistence?

> The challenge explicitly permits a test-double repository. Durable persistence would add infrastructure without improving assessment of the required payment flow.

### Why separate merchant and bank DTOs?

> They represent independent external contracts and should be able to evolve independently.

### Why no Luhn?

> The implementation follows the supplied card validation contract rather than silently making it stricter.

### Why no retries?

> Retrying a payment authorization without explicit idempotency/retry guarantees can create duplicate-processing risk when the downstream outcome is unknown.

### Why no full PAN/CVV persistence?

> They are not needed for the required retrieval response. The implementation minimizes sensitive data and stores only the last four PAN digits plus required payment attributes. CVV is never persisted.

### Why not store an encrypted PAN anyway?

> Encryption is useful when sensitive data must be retained; it is not a reason to retain data that the application does not need. Storing encrypted PAN would introduce key-management, rotation, authorization, auditing, backup, and compromise-recovery responsibilities without supporting any requirement in this challenge. If a production feature required reusable card credentials, I would prefer a PCI-appropriate tokenization/card-vault design and store the resulting token/reference in the gateway rather than making the ordinary gateway database a store of raw PAN.

### Why no idempotency implementation?

> Idempotency is an important production requirement for payment creation, but it is outside the supplied contract and the acquiring-bank simulator provides no idempotency semantics. A production design would accept an `Idempotency-Key`, atomically store a request fingerprint and result, return the original result for safe repeated requests, reject reuse with a different payload, and use shared durable storage. However, gateway-side idempotency alone cannot guarantee exactly-once processing if the bank processed a request but its response was lost. That requires compatible downstream idempotency/reconciliation semantics.

---

# 21. Production considerations — discuss, don't implement all of them

A good take-home demonstrates awareness without turning the exercise into a production platform.

Document/discuss:

- durable shared database;
- merchant authentication/authorization;
- idempotency;
- duplicate-payment protection;
- tokenization/card vaulting where reusable card credentials are required; encryption/key management only where retaining PAN is genuinely necessary;
- secrets management;
- rate limiting;
- structured logs;
- metrics and alerting;
- distributed tracing;
- safe timeout/retry policy;
- circuit breaking where appropriate;
- reconciliation for unknown outcomes;
- horizontal scaling;
- high availability;
- database consistency/concurrency;
- auditability;
- PCI/security controls;
- deployment strategy;
- readiness/liveness;
- API versioning if/when contract evolution requires it.

Do not implement all of these.

---

# 22. API/security details to watch

## Never return

- full PAN;
- CVV.

## Never log

- full PAN;
- CVV;
- raw payment request body;
- raw bank request body.

## Prefer string for

- full card number;
- last four;
- CVV;
- currency.

## Prefer integer for

- amount in minor currency units.

Do not introduce `decimal` for the supplied amount contract; the requirement explicitly uses an integer in minor units.

---

# 23. Code-style guidance

Use idiomatic, readable .NET 8/C#.

Prefer:

- nullable reference types;
- file-scoped namespaces;
- `sealed` where inheritance is not intended, if it improves clarity;
- constructor injection;
- `async`/`await` for HTTP operations;
- `CancellationToken` on async request flow;
- small methods with clear names;
- explicit mapping where only a handful of fields exist;
- immutable/init-only DTOs where practical;
- `TimeProvider` for time-dependent validation;
- `HttpClientFactory`/typed HTTP client;
- options/configuration for downstream URL;
- `ConcurrentDictionary` for singleton in-memory storage;
- string enum serialization.

Avoid:

- clever generic abstractions;
- reflection-based mapping;
- unnecessary inheritance;
- static global mutable state;
- fire-and-forget calls;
- `.Result` / `.Wait()` on async HTTP code;
- swallowing exceptions;
- broad catch-all exception blocks that convert every failure to the same outcome.

---

# 24. Error-handling principles

Keep three categories distinct:

```text
1. Merchant validation problem
   -> Rejected
   -> 400
   -> bank not called

2. Acquiring-bank business decision
   -> Authorized / Declined
   -> normal payment result

3. Technical/system failure
   -> timeout / 503 / unexpected bank response
   -> operational error response
   -> NOT Declined
   -> NOT Rejected
```

This separation is central to the design.

---

# 25. Proposed implementation sequence for Codex

Do not perform a giant rewrite in one uncontrolled step.

Work incrementally and keep the solution compiling/tests passing.

## Phase 0 — baseline

1. Inspect repository.
2. Run:
   ```powershell
   dotnet build
   dotnet test
   ```
3. Record pre-existing failing test(s) rather than assuming new changes caused them.
4. Verify bank simulator independently if useful.

## Phase 1 — contracts/models

1. Correct merchant request model.
2. Make card/CVV types strings.
3. Add internal `Payment`.
4. Ensure last four is a string.
5. Configure string enum serialization.
6. Add/update response models.

Run tests/build.

## Phase 2 — repository

1. Replace response-object storage with `Payment`.
2. Use thread-safe in-memory dictionary.
3. Introduce `IPaymentRepository` only as a meaningful persistence boundary.
4. Fix GET 404.

Run tests/build.

## Phase 3 — validation

1. Implement exact stated validation.
2. Add `TimeProvider` for expiry.
3. Add validator unit tests.
4. Do not add Luhn.
5. Do not add unstated amount rules without documenting the decision.

Run tests/build.

## Phase 4 — acquiring bank

1. Add separate bank DTOs.
2. Add `IAcquiringBankClient`.
3. Implement typed `HttpClient`.
4. Externalize base URL.
5. Map authorized/declined.
6. Explicitly handle technical failures.
7. Add bank-client tests with fake HTTP handler.

Run tests/build.

## Phase 5 — orchestration

1. Implement `PaymentService`.
2. Validation before bank call.
3. Persist Authorized and Declined.
4. Do not persist Rejected.
5. Do not invent a business outcome on bank technical failure.
6. Add service tests.

Run tests/build.

## Phase 6 — API

1. Implement POST endpoint.
2. Return consistent HTTP responses.
3. Use `CreatedAtAction` if using `201`.
4. Verify GET.
5. Add API tests.

Run tests/build.

## Phase 7 — observability

1. Add safe structured logs.
2. Ensure no PAN/CVV logging.
3. Add basic health endpoint if appropriate.
4. Document production metrics/tracing.

Run tests/build.

## Phase 8 — packaging

1. Add multi-stage Dockerfile.
2. Optionally integrate API into Compose without breaking simple bank-only local usage.
3. Verify environment-based bank URL.
4. Document commands.

Run tests/build.

## Phase 9 — documentation/review

1. Finish README.
2. Document assumptions.
3. Document trade-offs.
4. Document production considerations.
5. Review for accidental over-engineering.
6. Review for sensitive-data leakage.
7. Run full test suite.
8. Run manual simulator smoke test.
9. Run formatter if configured.
10. Check `git diff` for unrelated changes.

---

# 26. Codex operating rules

When using this guide, Codex should follow these constraints:

## Before changing architecture

Explain briefly:

- what problem the change solves;
- why the existing simpler design is insufficient;
- whether the change is required by the challenge.

Do not add architectural patterns as demonstrations.

## Before adding a NuGet package

Ask:

- can this be done clearly with the .NET BCL/framework?
- does the package materially simplify or improve correctness?
- does it create unnecessary reviewer overhead?

Prefer framework functionality for this small exercise.

## Before adding a validation rule

Check it against the written requirements.

If not explicit:

- flag it as an assumption;
- do not implement silently.

## Before adding retries

Do not.

Raise the idempotency/unknown-outcome concern first.

## Before storing/logging data

Check whether it includes:

- full PAN;
- CVV.

Do not persist/log them unless an explicit requirement later overrides this guide.

## After each meaningful change

Run:

```powershell
dotnet build
dotnet test
```

Fix compiler warnings/errors relevant to our code.

Do not hide failures by weakening/removing tests.

## Existing tests

Do not delete a starter test merely because the starter implementation fails it.

Understand the expected behaviour and make the implementation correct.

---

# 27. Things Codex must NOT do without explicit approval

Do not introduce:

- Luhn validation.
- EF Core/database.
- Redis.
- Kafka/message bus.
- CQRS.
- MediatR.
- Clean Architecture multi-project split.
- generic repository.
- Unit of Work.
- AutoMapper.
- authentication implementation.
- merchant API keys.
- idempotency implementation.
- automatic bank retries.
- Polly retry pipeline for authorization.
- full OpenTelemetry stack/exporter.
- Kubernetes.
- Terraform.
- API gateway infrastructure.
- more than three supported currencies.
- card-scheme detection.
- BIN/IIN lookup.
- 3DS.
- refunds/captures/voids.
- webhooks.
- payment events.
- background workers.

These may be discussed as future/production considerations.

---

# 28. Potential interview probes and the intended reasoning

## "Why didn't you use an interface for `PaymentService`?"

Answer direction:

There is currently one implementation and no meaningful substitution boundary. Adding an interface would add indirection without helping the required design/tests. Interfaces are used where they have clear value: the external bank and persistence boundaries.

---

## "Why did you use an interface for the bank?"

It is an external dependency with independent failure behaviour and a different contract. The abstraction makes the payment workflow deterministic to unit test and prevents HTTP concerns from leaking into business orchestration.

---

## "Why did you use an interface for repository/storage?"

The supplied in-memory repository is explicitly a stand-in for persistence. Persistence is therefore a genuine replaceable boundary. In production it could be replaced with durable storage without coupling payment orchestration to storage mechanics.

---

## "Why not use a database?"

The exercise explicitly says a real storage engine is unnecessary. Adding one increases infrastructure and setup while not materially improving demonstration of the required flow. I would use shared durable persistence in production, especially before horizontal scaling.

---

## "Why not use Luhn?"

The specification only requires 14–19 numeric characters. Luhn would strengthen the business contract beyond the written requirement and could reject otherwise valid challenge inputs. I would add it only if product/API requirements specify it.

---

## "Why is CVV a string?"

It is a fixed-length digit sequence, not a numeric quantity. `"012"` must remain three characters. Integer representation loses leading zeroes.

---

## "Why is last four a string?"

Same reason: `"0012"` is valid last-four data and must not become `"12"`.

---

## "Why minor units as integer?"

That is the supplied API contract and avoids floating-point/decimal representation concerns for the request amount.

---

## "Why don't you store CVV/full card number?"

They are not needed for the required retrieval contract. The implementation follows data minimization: the full PAN exists only long enough to validate and send the authorization request, then only the last four digits are retained. CVV is never persisted.

If a future feature required reusable card credentials, I would prefer a token/card-vault reference rather than simply storing encrypted PAN in the gateway database.

---

## "Wouldn't encrypting the full PAN be more production-like?"

Encryption is appropriate when sensitive data genuinely has to be stored. Here, the gateway does not need the PAN after authorization, so the safer and simpler design is not to retain it. Persisting encrypted PAN would still create key-management, access-control, rotation, audit, backup, and breach-impact responsibilities. For reusable credentials I would prefer tokenization/vaulting.

---

## "Why didn't you implement idempotency?"

Idempotency is highly relevant for payment creation, but it is not in the supplied contract. A correct implementation also needs atomic handling of concurrent requests, payload/key conflict semantics, durable/shared storage, retention rules, and failure-state behaviour.

More importantly, the supplied bank simulator provides no idempotency or reconciliation contract. If the bank processes a payment but the response is lost, gateway-side idempotency alone cannot prove whether retrying the bank call is safe. I would therefore document the intended `Idempotency-Key` design rather than claim an incomplete implementation gives exactly-once processing.

---

## "Would adding an Idempotency-Key change the required API?"

It would extend the API contract, but an optional HTTP header can be backward-compatible:

```http
Idempotency-Key: <client-generated-key>
```

I would prefer that over adding the key to the payment JSON payload because it is request-processing metadata rather than payment-domain data. I would only implement it in this take-home if I deliberately chose the extra scope and documented the downstream limitation.

---

## "Why is a declined payment not an HTTP error?"

The gateway successfully accepted, validated, and processed the payment request with the acquiring bank. Decline is a valid business outcome from that operation, not a failure of the gateway API.

---

## "Why isn't bank 503 a Declined payment?"

Because the acquiring bank did not return a business authorization decision. Treating unknown/unavailable as Declined would misrepresent the payment state.

---

## "Why don't you retry bank 503?"

Without explicit idempotency/retry semantics we cannot safely assume the first authorization attempt was not processed. Blind retries can create duplicate processing. Production behaviour must be based on the provider's idempotency guarantees and reconciliation strategy.

---

## "What happens if you run multiple API instances?"

The exercise's process-local in-memory repository is not shared. GET requests could hit an instance that does not contain a payment created on another instance. Production horizontal scaling therefore requires shared durable storage.

---

## "What would you monitor?"

Separate business and operational signals:

Business:
- authorization rate;
- decline rate;
- rejection rate.

Operational:
- gateway error rate;
- bank error/timeout rate;
- bank latency;
- API latency;
- request rate;
- availability.

Correlate using payment/trace IDs without logging PAN/CVV.

---

## "How would you host it?"

Containerize the ASP.NET API, externalize configuration, use health probes, run multiple stateless API instances behind a load balancer in production, and use shared durable persistence. Secrets would come from a secrets-management facility rather than source/config files.

For this exercise, a Dockerfile plus the supplied bank simulator is enough.

---

## "What tests matter most?"

Tests that prove business boundaries:

- invalid request never reaches bank;
- Authorized and Declined are both persisted;
- bank technical failure does not become Declined;
- GET unknown returns 404;
- bank contract serialization is correct;
- sensitive fields are absent from merchant responses;
- expiry validation is deterministic.

---

# 29. Definition of done

The submission is ready when all of the following are true:

## Functional

- [ ] POST payment exists.
- [ ] GET payment by ID exists.
- [ ] Card number validation matches spec.
- [ ] Expiry validation matches documented interpretation.
- [ ] Currency validation uses no more than three codes.
- [ ] Amount remains integer minor units.
- [ ] CVV validation matches spec.
- [ ] Invalid merchant input is Rejected without bank call.
- [ ] Odd-ending simulator card maps to Authorized.
- [ ] Even non-zero ending simulator card maps to Declined.
- [ ] Bank 503 is handled as technical failure, not Declined/Rejected.
- [ ] Authorized payment can be retrieved.
- [ ] Declined payment can be retrieved.
- [ ] Unknown payment returns 404.
- [ ] Merchant response contains last four only, not full PAN.
- [ ] CVV is never returned.

## Code design

- [ ] Payment flow is easy to follow.
- [ ] Merchant DTO and bank DTO are separate.
- [ ] External bank integration is isolated.
- [ ] Persistence is isolated without generic-repository over-engineering.
- [ ] No pointless interfaces/patterns.
- [ ] Thread-safe in-memory storage.
- [ ] No full PAN/CVV persisted.
- [ ] String enum output.

## Testing

- [ ] Build passes.
- [ ] Automated tests pass.
- [ ] Validator edge cases covered.
- [ ] Authorized service path covered.
- [ ] Declined service path covered.
- [ ] Rejected path proves bank not called.
- [ ] Bank failure path covered.
- [ ] Bank contract serialization covered.
- [ ] GET 200/404 covered.
- [ ] POST API behaviour covered.
- [ ] Sensitive response fields checked.

## Observability

- [ ] Structured logging exists.
- [ ] PAN/CVV/raw requests not logged.
- [ ] Declines are not logged as application errors.
- [ ] Bank failures are observable.
- [ ] Production metrics/tracing documented.
- [ ] Health endpoint considered/implemented proportionately.

## Packaging

- [ ] API can be run locally with clear instructions.
- [ ] Bank simulator can be started with Docker Compose.
- [ ] Bank URL is configuration, not buried in business logic.
- [ ] Dockerfile exists if chosen for packaging competency.
- [ ] Container networking is correct if API is added to Compose.

## Documentation

- [ ] README has run instructions.
- [ ] README has test instructions.
- [ ] README documents API.
- [ ] README documents assumptions.
- [ ] README documents key design decisions.
- [ ] README explains Luhn omission.
- [ ] README explains downstream failure/retry decision.
- [ ] README explains why idempotency is not implemented and describes the intended `Idempotency-Key` production design.
- [ ] README explicitly states that gateway-side idempotency cannot guarantee exactly-once processing without compatible bank semantics.
- [ ] README explains storage trade-off.
- [ ] README explains why full PAN/CVV are not persisted and why tokenization/vaulting is preferred if reusable card credentials are later required.
- [ ] README describes observability.
- [ ] README describes production considerations without pretending they were implemented.

---

# 30. Final review principle

The submission should communicate:

> I can build a correct payment API, separate business outcomes from technical failures, test the important boundaries, operate and package the service sensibly, and explain production concerns — without turning a deliberately small exercise into an architecture showcase.

If a proposed change makes the solution substantially more complicated, ask:

> What concrete requirement, failure mode, testability problem, or maintainability problem does this solve **in this challenge**?

If there is no strong answer, do not add it.
