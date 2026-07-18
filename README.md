# EBANX – Financial Transactions API

A simple in-memory financial transactions API built with .NET 10. Implements deposit, withdraw, and transfer operations with thread-safe state management.

---

## How to Run

**Prerequisites:** .NET 10 SDK

```bash
# Clone / navigate to project root
cd Ebanx

# Run the API (listens on http://localhost:5000 by default)
dotnet run --project Ebanx/Ebanx.csproj
```

## How to Test

```bash
# Run all tests (unit + integration)
dotnet test
```

Expected output: **20 tests passing**, 0 failures.

---

## API Reference

### `POST /reset`
Clears all account state. Returns `200 OK`.

### `GET /balance?account_id={id}`
Returns the account balance.
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
Ebanx/
├── Domain/
│   └── Account.cs                  # Entity: immutable record
├── DTOs/
│   ├── EventRequest.cs             # Incoming event payload
│   └── AccountDto.cs               # Response shape
├── Repositories/
│   ├── IAccountRepository.cs       # Storage contract
│   └── InMemoryAccountRepository.cs # Thread-safe implementation
├── Services/
│   └── TransactionService.cs       # Business rules
├── Controllers/
│   └── EventController.cs          # HTTP layer (pure dispatcher)
└── Program.cs                      # DI wiring + routing
```

**Three layers, no framework magic:**
- **Controller** — maps HTTP ↔ service calls only. Zero business logic.
- **Service** — all business rules (sufficient funds, upsert on deposit, etc.).
- **Repository** — all state management, completely isolated.

---

## Key Technical Decisions

### In-memory storage with `ConcurrentDictionary`
The spec requires no persistence. `ConcurrentDictionary<string, decimal>` gives thread-safe reads and writes for independent account operations without a global lock.

### Atomic transfers with ordered locks
A transfer touches two accounts simultaneously. To prevent race conditions, the repository acquires locks on both account IDs **in lexicographic order**. This ensures that two concurrent reverse transfers (A→B and B→A) never deadlock, while still being fully atomic.

### Singleton repository
Registered as `AddSingleton<IAccountRepository>` so the same in-memory store is shared across all requests for the lifetime of the application.

### Plain numeric responses for balance/errors
The test script expects `20` and `0` as raw numbers, not JSON. The controller writes these as plain content to match the contract exactly.

### Real repository in unit tests (no mocks)
Unit tests instantiate a real `InMemoryAccountRepository`. This validates actual state mutations, not wired return values — aligned with the challenge's requirement that tests validate real behavior.
