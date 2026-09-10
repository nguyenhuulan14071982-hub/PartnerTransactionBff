# Partner Transaction BFF (.NET 8) — Senior Interview Version

A production-minded .NET 8 partner-facing transaction ingestion API. It validates incoming transactions, verifies and enriches partner data through an HTTP dependency, applies resilience, protects against duplicate submissions, and publishes a versioned message to RabbitMQ.

## Architecture

```text
Partner
  |
  | POST /api/v1/partner/transactions
  v
ASP.NET Core API
  |
  |-- API key (optional/configurable) + rate limiting
  |-- validate request
  |-- idempotency guard
  |-- HTTP -> Partner Verification API
  |       `-- retry transient failures: 3 attempts, exponential backoff + jitter
  |-- enrich from external response
  `-- RabbitMQ publisher
          |-- durable queue
          |-- persistent message
          |-- publisher confirms
          `-- automatic connection recovery
```

### Project structure

```text
src/PartnerTransactionBff/
├── Application/
│   ├── IIdempotencyStore.cs
│   └── InMemoryIdempotencyStore.cs
├── Configuration/
│   ├── IdempotencyOptions.cs
│   ├── PartnerVerificationOptions.cs
│   ├── RabbitMqOptions.cs
│   └── SecurityOptions.cs
├── Contracts/
├── Controllers/
├── Domain/
├── Infrastructure/
│   ├── ITransactionQueue.cs
│   ├── RabbitMqConnectionProvider.cs
│   ├── RabbitMqHealthCheck.cs
│   └── RabbitMqTransactionQueue.cs
├── Middleware/
├── Services/
└── Program.cs
```

## Key design decisions

### 1. 202 Accepted

The endpoint returns `202 Accepted` because it acknowledges that the transaction has been accepted for asynchronous processing. It does not claim that the downstream legacy system has completed the transaction.

### 2. External enrichment

The verification API returns partner metadata (`PartnerName`, `PartnerTier`). The message published to RabbitMQ uses that external response rather than fabricating enrichment data inside the BFF.

### 3. Resilience

`Microsoft.Extensions.Http.Resilience` retries transient HTTP failures up to three times using exponential backoff and jitter. The dependency is a GET operation, so retrying does not introduce a write-side-effect retry.

### 4. RabbitMQ lifecycle

The API does **not** establish a RabbitMQ connection during application startup. The connection is created lazily when it is first required. Therefore, a temporary RabbitMQ outage does not prevent the HTTP application from starting.

The RabbitMQ .NET client is configured for automatic connection and topology recovery. Publishing uses a long-lived channel rather than opening a new channel for every request. RabbitMQ recommends long-lived connections/channels and explicitly discourages opening a channel for every operation. urlRabbitMQ .NET/C# Client API Guidehttps://www.rabbitmq.com/client-libraries/dotnet-api-guide

### 5. Publisher confirms

The publisher channel enables publisher confirmations and confirmation tracking. `BasicPublishAsync` is awaited, so the application does not return `202` until the broker confirms the publish. RabbitMQ documents publisher confirms as the mechanism for detecting whether published messages were handled by the broker. urlRabbitMQ Publisher Confirms tutorialhttps://www.rabbitmq.com/tutorials/tutorial-seven-dotnet

Publisher confirms improve broker-delivery reliability, but they do **not** provide end-to-end exactly-once processing. The next level for high-value transactional systems is a durable outbox plus idempotent consumers.

### 6. Idempotency

The service derives a default idempotency key from `partnerId + transactionReference`. Clients may also provide an explicit `Idempotency-Key` header.

The included store is process-local and intentionally simple for the assessment. It prevents duplicate submissions within one application instance. In a multi-instance production deployment it should be replaced by a durable/distributed implementation, for example PostgreSQL with a unique constraint or Redis with an atomic operation.

### 7. Message contract

The RabbitMQ message contains:

- `MessageId`
- `SchemaVersion`
- transaction data
- external partner enrichment
- `EnrichedAt`

This makes message evolution and correlation easier for downstream consumers.

### 8. Health checks

- `/health/live` — process liveness only.
- `/health/ready` — readiness including RabbitMQ connectivity.

The readiness check is intentionally allowed to fail while the API process remains alive.

### 9. Security

The assessment version includes:

- optional `X-API-Key` protection for the transaction endpoint;
- fixed-window rate limiting: 100 requests/minute per source IP;
- no secrets committed as production credentials.

For production B2B integrations, replace the simple API key with OAuth2/OIDC client credentials or mTLS as appropriate, and store credentials in a managed secret store.

## Run locally without Docker

Prerequisites:

- .NET 8 SDK
- RabbitMQ running locally on port `5672`

The API can start even when RabbitMQ is temporarily unavailable, but transaction publishing will fail until RabbitMQ is reachable.

```powershell
dotnet restore
dotnet build PartnerTransactionBff.sln
dotnet test PartnerTransactionBff.sln

dotnet run --project src/PartnerTransactionBff
```

Swagger: `http://localhost:5080/swagger`

## Run with Docker Compose

```bash
docker compose up --build
```

API: `http://localhost:5080/swagger`

RabbitMQ management UI: `http://localhost:15672`

The Compose configuration enables the assessment API key:

```text
X-API-Key: dev-assessment-key
```

Example:

```bash
curl -X POST http://localhost:5080/api/v1/partner/transactions \
  -H "Content-Type: application/json" \
  -H "X-API-Key: dev-assessment-key" \
  -H "Idempotency-Key: P-1001-TXN-99823" \
  -d '{
    "partnerId":"P-1001",
    "transactionReference":"TXN-99823",
    "amount":250.00,
    "currency":"USD",
    "timestamp":"2024-05-10T14:30:00Z"
  }'
```

Expected response: `202 Accepted`.

## Mock verification API

```text
GET /mock/partners/{partnerId}/verify
```

It deliberately throws a `TimeoutException` 30% of the time. The caller's resilience pipeline receives that as a transient HTTP failure and retries it.

For a partner beginning with `P-`, the mock returns verification and enrichment data such as:

```json
{
  "verified": true,
  "partnerId": "P-1001",
  "partnerName": "Partner P-1001",
  "partnerTier": "STANDARD"
}
```

## Tests

The unit test suite covers:

- validation rules;
- service orchestration;
- external enrichment mapping;
- duplicate/idempotency behavior;
- queue failure behavior;
- retry behavior;
- partner verification HTTP responses;
- idempotency store behavior.

Run:

```bash
dotnet test PartnerTransactionBff.sln --collect:"XPlat Code Coverage"
```

## Senior interview discussion points

### What if RabbitMQ is down?

The API remains running. A request that needs publishing receives a dependency failure instead of preventing the entire service from starting.

### Can publisher confirms guarantee exactly-once processing?

No. Publisher confirms provide broker-side publish acknowledgement. They do not guarantee exactly-once business processing. Exactly-once semantics are normally approached with idempotency, durable storage/outbox patterns, and idempotent consumers.

### What is still intentionally outside this assessment?

A real production deployment would normally add:

- durable distributed idempotency;
- transactional outbox;
- OAuth2/OIDC or mTLS instead of a demo API key;
- OpenTelemetry metrics/traces;
- integration tests using a real RabbitMQ instance/Testcontainers;
- consumer-side retry/DLQ strategy;
- secret management;
- deployment/CI/CD and SAST/dependency scanning.

These are deliberately documented trade-offs rather than adding infrastructure solely for the sake of the demo.
