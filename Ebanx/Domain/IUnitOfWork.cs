namespace Ebanx.Domain;


public interface IUnitOfWork
{
    T Execute<T>(IEnumerable<string> accountIds, Func<T> operation);
}
