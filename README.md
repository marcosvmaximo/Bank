# EBANX – Financial Transactions API

A simple in-memory financial transactions API built with .NET 10. Implements deposit, withdraw, and transfer operations with thread-safe state management.

---

## How to Run

**Prerequisites:** .NET 10 SDK

```bash
# Navigate to project root
cd Bank

# Run the API (listens on http://localhost:5270 by default)
dotnet run --project Ebanx/Ebanx.csproj
```

### Exposing for Automated Evaluator (`ngrok`)
If you need to expose the local API to the internet to run the EBANX automated evaluator (`ipkiss.pragmazero.com`):

```bash
# In a separate terminal while the API is running:
ngrok http 5270
```
Copy the generated `https://xxxx.ngrok-free.app` URL and paste it into the EBANX automated test suite interface.

---

## How to Test

### Automated Test Suite (`xUnit`)
```bash
# Run all 43 tests (unit + integration + concurrency)
dotnet test
```

Expected output: **43 tests passing**, 0 failures.

### Load & Concurrency Testing (`k6`)
A custom [`k6`](https://grafana.com/docs/k6/latest/) script (`k6_test.js`) is included in the root directory to stress-test race conditions, idempotency, and deadlock freedom against the live running API:

**Prerequisites:** Install k6 (`brew install k6` on macOS).

```bash
# Make sure the API is running in another terminal (`dotnet run`), then:
k6 run k6_test.js
```

The script executes two simultaneous high-concurrency scenarios across **80 Virtual Users (VUs)**:
1. `idempotency_race_condition`: **50 VUs** concurrently sending 200 requests with the exact same `Idempotency-Key` (`"k6-idempotency-key-2026"`). Verifies that exactly one deposit executes and all remaining concurrent requests safely receive the cached `201 Created` response without double-counting.
2. `concurrent_transfers_deadlock`: **30 VUs** performing cross-transfers (`A→B` and `B→A`) over 300 iterations. Verifies canonical lock ordering (`UnitOfWork`) prevents deadlocks under heavy contention and maintains state integrity.

---

## API Reference

### `POST /reset`
Clears all account state. Returns `200 OK`.

### `GET /balance?account_id={id}`
Returns the account balance. Read-only — no side effects.
- `200 {balance}` — account exists (e.g. `20`)
- `404 0` — account does not exist

### `POST /event`
Handles financial events. Payload varies by `type`:

**Deposit** — creates account if it doesn't exist:
```json
{ "type": "deposit", "destination": "100", "amount": 10 }
```
Response `201`:
```json
{ "destination": { "id": "100", "balance": 10 } }
```

**Withdraw**:
```json
{ "type": "withdraw", "origin": "100", "amount": 5 }
```
Response `201`:
```json
{ "origin": { "id": "100", "balance": 15 } }
```
Returns `404 0` if account doesn't exist or has insufficient funds.

**Transfer**:
```json
{ "type": "transfer", "origin": "100", "destination": "300", "amount": 15 }
```
Response `201`:
```json
{ "origin": { "id": "100", "balance": 0 }, "destination": { "id": "300", "balance": 15 } }
```
Returns `404 0` if origin doesn't exist or has insufficient funds.

---

## Architecture

```
Bank/
├── Ebanx/                               # Web API project
│   ├── Api/
│   │   ├── Controllers/
│   │   │   └── EventController.cs       # HTTP dispatcher — zero business logic
│   │   └── Filters/
│   │       └── IdempotencyFilter.cs     # Thread-safe async check-then-act serialization
│   ├── Application/
│   │   ├── DTOs/                        # Polymorphic event payloads & representations
│   │   └── TransactionService.cs        # Core business rules & input validation
│   ├── Domain/
│   │   ├── Account.cs                   # Rich domain model with encapsulation
│   │   ├── IAccountRepository.cs        # Storage contract
│   │   └── IUnitOfWork.cs               # Transaction boundary contract
│   ├── Infrastructure/
│   │   ├── AccountRepository.cs         # Thread-safe ConcurrentDictionary storage
│   │   ├── InMemoryIdempotencyRepository.cs
│   │   └── UnitOfWork.cs                # Canonical ordered lock acquisition
│   └── Program.cs                       # DI wiring + routing
└── Ebanx.Tests/                         # xUnit test project
    ├── UnitTests/
    │   ├── AccountTests.cs              # Domain invariants & validations
    │   ├── TransactionServiceTests.cs   # Business logic rules
    │   └── UnitOfWorkTests.cs           # Lock serialization & exception safety
    └── IntegrationTests/
        ├── EventApiTests.cs             # Full HTTP lifecycle & state persistence
        └── ConcurrencyTests.cs          # Deadlock, race condition & stress testing
```

**Four layers following Clean Architecture principles:**
- **Api** — handles HTTP transport, status codes, and `Idempotency-Key` filter serialization.
- **Application** — orchestrates operations (`TransactionService`) and validates business invariants.
- **Domain** — encapsulates core business entities (`Account`) and defines storage/transaction contracts.
- **Infrastructure** — implements high-performance thread-safe storage and `Monitor`-based `UnitOfWork`.

---

## Key Technical Decisions

### Thread-Safe Storage with `ConcurrentDictionary`
The spec requires no database persistence. `ConcurrentDictionary<string, Account>` provides thread-safe reads and atomic lookups without requiring a global application lock.

### Atomic Transfers with Canonical Ordered Locking (`UnitOfWork`)
A financial transfer modifies two accounts simultaneously. To prevent race conditions and deadlocks under heavy contention (`ConcurrencyTests.ConcurrentReverseTransfers_ShouldNotDeadlock`):
- `UnitOfWork` acquires dedicated `Monitor` lock instances for each account ID.
- Lock objects are sorted in **canonical lexicographic order** (`StringComparer.Ordinal`) before acquisition. This guarantees that two concurrent threads performing reverse transfers (`A→B` and `B→A`) acquire locks in the exact same order, completely eliminating deadlocks.

### Race-Condition-Free Idempotency (`IdempotencyFilter`)
To support exactly-once processing under high concurrent load:
- The `IdempotencyFilter` uses a per-key `SemaphoreSlim(1,1)` combined with a double-check locking pattern (`TryGetValue`).
- When multiple requests arrive simultaneously with the same `Idempotency-Key`, only the first thread executes the operation. Subsequent threads wait for the lock and immediately receive the cached `201 Created` response without duplicating transactions.

### Read-Only `GET` Operations
`GetBalance` invokes read-only queries against the repository (`GetById`). It has zero side effects and never mutates internal state.

