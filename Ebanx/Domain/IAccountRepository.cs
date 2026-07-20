namespace Ebanx.Domain;

public interface IAccountRepository
{
    Account? GetById(string id);
    Account Upsert(Account account);
    void Reset();
}
