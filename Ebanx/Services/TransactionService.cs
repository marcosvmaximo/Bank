using Ebanx.Domain;
using Ebanx.Repositories;

namespace Ebanx.Services;

/// <summary>
/// Encapsulates all financial transaction business rules.
/// The HTTP layer delegates here — no business logic lives in the controller.
/// </summary>
public class TransactionService(IAccountRepository repository)
{
    /// <summary>
    /// Deposits <paramref name="amount"/> into <paramref name="destinationId"/>.
    /// Creates the account if it does not exist (upsert semantics per the spec).
    /// </summary>
    public Account Deposit(string destinationId, decimal amount)
    {
        var existing = repository.GetById(destinationId);
        var newBalance = (existing?.Balance ?? 0m) + amount;
        return repository.Upsert(new Account(destinationId, newBalance));
    }

    /// <summary>
    /// Withdraws <paramref name="amount"/> from <paramref name="originId"/>.
    /// Returns null when the account does not exist or has insufficient funds.
    /// </summary>
    public Account? Withdraw(string originId, decimal amount)
    {
        var existing = repository.GetById(originId);
        if (existing is null)
            return null;

        if (existing.Balance < amount)
            return null;

        return repository.Upsert(existing with { Balance = existing.Balance - amount });
    }

    /// <summary>
    /// Atomically transfers <paramref name="amount"/> from origin to destination.
    /// Returns null for both when origin does not exist or has insufficient funds.
    /// </summary>
    public (Account? Origin, Account? Destination) Transfer(
        string originId, string destinationId, decimal amount)
    {
        var success = repository.Transfer(originId, destinationId, amount,
            out var origin, out var destination);

        return success ? (origin, destination) : (null, null);
    }

    /// <summary>
    /// Returns the account by ID. Pure read — no side effects.
    /// </summary>
    public Account? GetAccount(string id) => repository.GetById(id);

    /// <summary>
    /// Clears all accounts from the store.
    /// </summary>
    public void Reset() => repository.Reset();
}
