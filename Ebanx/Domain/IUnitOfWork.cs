namespace Ebanx.Domain;

/// <summary>
/// Garante atomicidade ao executar operações que envolvem múltiplas contas.
/// Em sistemas com banco de dados, equivale a uma transação.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Executa <paramref name="operation"/> sob lock exclusivo das contas indicadas em <paramref name="accountIds"/>.
    /// Os locks são adquiridos em ordem determinística para prevenir deadlock.
    /// </summary>
    T Execute<T>(IEnumerable<string> accountIds, Func<T> operation);
}
