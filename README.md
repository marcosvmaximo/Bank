# EBANX – Financial Transactions API

Simple in-memory financial API built with .NET 10.

---

## How to Run

**Prerequisites:** .NET 10 SDK

```bash
dotnet run --project Ebanx/Ebanx.csproj
```

API listens on `http://localhost:5270` by default.

### Exposing via ngrok (for automated evaluator)

**Prerequisites:** [ngrok](https://ngrok.com/download) account + authtoken configured

```bash
ngrok config add-authtoken <your-token>
ngrok http 5270
```

Copy the generated URL and paste it into the EBANX test suite at [`ipkiss.pragmazero.com`](ipkiss.pragmazero.com).

---

## How to Test

```bash
dotnet test
```

Expected: **43 tests passing**, 0 failures.

### Load & Concurrency Testing (k6)

**Prerequisites:** `brew install k6`

```bash
k6 run k6_test.js
```

Two concurrent scenarios run simultaneously:
- **50 VUs** fire 200 requests with the same `Idempotency-Key` — verifies exactly-once execution
- **30 VUs** run cross-transfers (`A→B` and `B→A`) in parallel — verifies no deadlocks

---

## API Reference

### `POST /reset`
Clears all state. Returns `200 OK`.

### `GET /balance?account_id={id}`
- `200 {balance}` — account exists
- `404 0` — account not found

### `POST /event`

**Deposit:**
```json
{ "type": "deposit", "destination": "100", "amount": 10 }
```
`201 { "destination": { "id": "100", "balance": 10 } }`

**Withdraw:**
```json
{ "type": "withdraw", "origin": "100", "amount": 5 }
```
`201 { "origin": { "id": "100", "balance": 15 } }` — returns `404 0` if account doesn't exist or insufficient funds.

**Transfer:**
```json
{ "type": "transfer", "origin": "100", "destination": "300", "amount": 15 }
```
`201 { "origin": { "id": "100", "balance": 0 }, "destination": { "id": "300", "balance": 15 } }` — returns `404 0` if origin doesn't exist or insufficient funds.

---

## Project Structure

```
Bank/
├── Ebanx/
│   ├── Api/
│   │   ├── Controllers/EventController.cs
│   │   └── Filters/IdempotencyFilter.cs
│   ├── Application/
│   │   ├── DTOs/
│   │   └── TransactionService.cs
│   ├── Domain/
│   │   ├── Account.cs
│   │   ├── IAccountRepository.cs
│   │   └── IUnitOfWork.cs
│   ├── Infrastructure/
│   │   ├── AccountRepository.cs
│   │   ├── InMemoryIdempotencyRepository.cs
│   │   └── UnitOfWork.cs
│   └── Program.cs
└── Ebanx.Tests/
    ├── UnitTests/
    │   ├── AccountTests.cs
    │   ├── TransactionServiceTests.cs
    │   └── UnitOfWorkTests.cs
    └── IntegrationTests/
        ├── EventApiTests.cs
        └── ConcurrencyTests.cs
```

---

## Design Notes

I kept the solution as simple as possible, but introduced a light layer separation that felt proportional to the scope: HTTP transport, business rules, and infrastructure are clearly divided — easy to navigate and extend without over-engineering.

**Idempotency** was a deliberate addition. It's not in the spec, but it's a real concern in financial systems and felt like the right thing to demonstrate. The `IdempotencyFilter` uses a `SemaphoreSlim(1,1)` per key with double-check locking — so concurrent requests with the same key execute the operation exactly once and return a cached response to the rest.

**Concurrency** is handled at two levels. The repository uses `ConcurrentDictionary` for safe structural operations. For multi-account transactions (transfers), I built a `UnitOfWork` that acquires `Monitor` locks in canonical lexicographic order. This eliminates deadlocks — two threads doing `A→B` and `B→A` simultaneously always acquire locks in the same order.

The k6 script exists to prove this works in practice, not just in unit tests. Running it against the live server with 80 virtual users and checking the final balance tells the real story.

I applied the YAGNI, KISS, and DRY principles, prioritizing simplicity and problem solving.