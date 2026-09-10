# Partner Transaction BFF (.NET 8) 

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
<img width="952" height="722" alt="image" src="https://github.com/user-attachments/assets/e3aabdb2-ea16-463d-9ea7-604ff23cafce" />

<img width="1467" height="442" alt="image" src="https://github.com/user-attachments/assets/8f687c84-0c78-46a0-9611-c1dc6e2c4fab" />
