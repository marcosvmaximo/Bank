# EBANX – Financial Transactions API

A simple in-memory financial transactions API built with .NET 10. Implements deposit, withdraw, and transfer operations with thread-safe state management.

---

## How to Run

**Prerequisites:** .NET 10 SDK

```bash
# Navigate to project root
cd Bank

# Run the API (listens on http://localhost:5000 by default)
dotnet run --project Ebanx/Ebanx.csproj
```

## How to Test

```bash
# Run all tests (unit + integration)
dotnet test
```

Expected output: **25 tests passing**, 0 failures.

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
├── Ebanx/                              # Web API project
│   ├── Controllers/
│   │   └── EventController.cs          # HTTP dispatcher — zero business logic
│   ├── DTOs/
│   │   ├── EventRequest.cs             # Incoming event payload (polymorphic)
│   │   ├── EventResponse.cs            # Response shape (nullable fields for partial output)
│   │   └── AccountDto.cs              # Account representation in responses
│   ├── Models/
│   │   └── Account.cs                  # Immutable record: Id + Balance
│   ├── Repositories/
│   │   ├── IAccountRepository.cs       # Storage contract
│   │   └── InMemoryAccountRepository.cs # Thread-safe implementation
│   ├── Services/
│   │   └── TransactionService.cs       # All business rules live here
│   └── Program.cs                      # DI wiring + routing
└── Ebanx.Tests/                        # xUnit test project
    ├── UnitTests/
    │   └── TransactionServiceTests.cs  # 13 unit tests — real repository, no mocks
    └── IntegrationTests/
        └── EventApiTests.cs            # 12 integration tests — full HTTP pipeline
```

**Three layers, no framework magic:**
- **Controller** — maps HTTP ↔ service calls only. Zero business logic.
- **Service** — all business rules (sufficient funds, upsert on deposit, input validation).
- **Repository** — all state management, completely isolated.

---

## Key Technical Decisions

### In-memory storage with `ConcurrentDictionary`
The spec requires no persistence. `ConcurrentDictionary<string, decimal>` gives thread-safe reads and writes for independent account operations without a global lock.

### Atomic transfers with ordered locks
A transfer touches two accounts simultaneously. To prevent race conditions, the repository acquires **dedicated lock objects** for both account IDs **in lexicographic order**. This ensures:
- Two concurrent reverse transfers (A→B and B→A) never deadlock, because both threads always acquire the same lock first.
- Lock objects are isolated per account — no shared global contention.

> **Why not `lock(string.Intern(id))`?** The CLR's intern pool is global. Locking on interned strings can create contention with other parts of the runtime that intern the same strings. Dedicated `object` instances scoped to each account are the correct approach.

### Singleton repository
Registered as `AddSingleton<IAccountRepository>` so the same in-memory store is shared across all requests for the application lifetime.

### Plain numeric responses for balance/errors
The test script expects `20` and `0` as raw numbers, not JSON objects. The controller returns these as plain `Ok(balance)` / `NotFound(0)`, which ASP.NET serializes as bare values.

### Input validation
The service layer rejects invalid inputs before touching state:
- `amount <= 0` → `400 Bad Request`
- `origin == destination` on transfer → `400 Bad Request`
- Missing required fields (e.g., no `destination` on deposit) → `400 Bad Request`

### Real repository in unit tests (no mocks)
Unit tests instantiate a real `InMemoryAccountRepository`. This validates actual state mutations — not just wired return values — aligned with the challenge's requirement that tests prove real behavior changes.
