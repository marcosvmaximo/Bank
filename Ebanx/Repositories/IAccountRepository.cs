using Ebanx.Models;

namespace Ebanx.Repositories;

public interface IAccountRepository
{
    Account? GetById(string id);
    Account Upsert(Account account);
    bool Transfer(string originId, string destinationId, decimal amount,
        out Account? origin, out Account? destination);
    void Reset();
}
