using System.Collections.Concurrent;
using Ebanx.Models;

namespace Ebanx.Repositories;

public class InMemoryAccountRepository : IAccountRepository
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
