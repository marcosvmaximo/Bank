using Ebanx.Domain;

namespace Ebanx.Repositories;

public interface IAccountRepository
{
    /// <summary>Returns the account or null if it doesn't exist. Pure read – no side effects.</summary>
    Account? GetById(string id);

    /// <summary>Creates the account if it doesn't exist, or replaces it if it does.</summary>
    Account Upsert(Account account);

    /// <summary>
    /// Atomically deducts <paramref name="amount"/> from origin and credits it to destination.
    /// Returns false when origin does not exist or has insufficient funds.
    /// </summary>
    bool Transfer(string originId, string destinationId, decimal amount,
        out Account? origin, out Account? destination);

    /// <summary>Wipes all accounts from the store.</summary>
    void Reset();
}
