using System.Collections.Concurrent;
using Ebanx.Domain;

namespace Ebanx.Infrastructure;

public class UnitOfWork : IUnitOfWork
{
    // Um objeto de lock por conta, compartilhado entre todas as operações
    private readonly ConcurrentDictionary<string, object> _lockObjects = new();

    public T Execute<T>(IEnumerable<string> accountIds, Func<T> operation)
    {
        // Ordena os IDs para garantir ordem de aquisição de locks consistente — previne deadlock
        var orderedLocks = accountIds
            .Distinct()
            .OrderBy(id => id, StringComparer.Ordinal)
            .Select(id => _lockObjects.GetOrAdd(id, _ => new object()))
            .ToList();

        return AcquireAndExecute(orderedLocks, 0, operation);
    }

    // Aquisição recursiva: cada nível de recursão adquire um lock e passa para o próximo
    private static T AcquireAndExecute<T>(List<object> locks, int index, Func<T> operation)
    {
        if (index == locks.Count)
            return operation();

        lock (locks[index])
            return AcquireAndExecute(locks, index + 1, operation);
    }
}
