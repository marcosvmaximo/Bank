using System.Collections.Concurrent;
using Ebanx.Models;

namespace Ebanx.Repositories;

public class InMemoryAccountRepository : IAccountRepository
{
    private readonly ConcurrentDictionary<string, decimal> _store = new();
    private readonly ConcurrentDictionary<string, object> _lockObjects = new();

    public Account? GetById(string id)
    {
        return _store.TryGetValue(id, out var balance)
            ? new Account(id, balance)
            : null;
    }

    public Account Upsert(Account account)
    {
        _store[account.Id] = account.Balance;
        return account;
    }

    public bool Transfer(string originId, string destinationId, decimal amount,
        out Account? origin, out Account? destination)
    {
        origin = null;
        destination = null;

        var (firstId, secondId) = string.Compare(originId, destinationId, StringComparison.Ordinal) < 0
            ? (originId, destinationId)
            : (destinationId, originId);

        var firstLock = _lockObjects.GetOrAdd(firstId, _ => new object());
        var secondLock = _lockObjects.GetOrAdd(secondId, _ => new object());

        lock (firstLock)
        {
            lock (secondLock)
            {
                if (!_store.TryGetValue(originId, out var originBalance))
                    return false;

                if (originBalance < amount)
                    return false;

                var newOriginBalance = originBalance - amount;
                var newDestinationBalance = _store.GetOrAdd(destinationId, 0m) + amount;

                _store[originId] = newOriginBalance;
                _store[destinationId] = newDestinationBalance;

                origin = new Account(originId, newOriginBalance);
                destination = new Account(destinationId, newDestinationBalance);
                return true;
            }
        }
    }

    public void Reset() => _store.Clear();
}
