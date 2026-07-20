using System.Collections.Concurrent;
using Ebanx.Domain;

namespace Ebanx.Infrastructure;

public class AccountRepository : IAccountRepository
{
    private readonly ConcurrentDictionary<string, Account> _store = new();

    public Account? GetById(string id)
        => _store.TryGetValue(id, out var account) ? account : null;

    public Account Upsert(Account account)
    {
        _store[account.Id] = account;
        return account;
    }

    public void Reset() => _store.Clear();
}
