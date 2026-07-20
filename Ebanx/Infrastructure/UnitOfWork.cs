using System.Collections.Concurrent;
using Ebanx.Domain;

namespace Ebanx.Infrastructure;

public class UnitOfWork : IUnitOfWork
{
    private readonly ConcurrentDictionary<string, object> _locks = new();

    public T Execute<T>(IEnumerable<string> accountIds, Func<T> operation)
    {
        // Ordena os IDs para garantir ordem de aquisição de locks consistente — previne deadlock
        var locks = accountIds
            .Distinct()
            .OrderBy(id => id, StringComparer.Ordinal)
            .Select(id => _locks.GetOrAdd(id, _ => new object()))
            .ToArray();

        var locksAcquired = 0;
        try
        {
            foreach (var lockObj in locks)
            {
                Monitor.Enter(lockObj);
                locksAcquired++;
            }

            return operation();
        }
        finally
        {
            // Libera de trás para frente, na ordem inversa da aquisição
            for (var i = locksAcquired - 1; i >= 0; i--)
            {
                Monitor.Exit(locks[i]);
            }
        }
    }
}
