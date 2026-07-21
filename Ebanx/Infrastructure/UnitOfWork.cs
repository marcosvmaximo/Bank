using System.Collections.Concurrent;
using Ebanx.Domain;

namespace Ebanx.Infrastructure;

public class UnitOfWork : IUnitOfWork
{
    private readonly ConcurrentDictionary<string, object> _locks = new();

    public T Execute<T>(IEnumerable<string> accountIds, Func<T> operation)
    {
        var locks = accountIds
            .Distinct()
            .OrderBy(id => id, StringComparer.Ordinal)
            .Select(id => _locks.GetOrAdd(id, _ => new object()))
            .ToArray();

        var acquired = 0;
        try
        {
            foreach (var gate in locks) { Monitor.Enter(gate); acquired++; }
            return operation();
        }
        finally
        {
            for (var i = acquired - 1; i >= 0; i--)
                Monitor.Exit(locks[i]);
        }
    }
}
