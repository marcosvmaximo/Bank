namespace Ebanx.Domain;

public class Account
{
    public string Id { get; }
    public long BalanceInCents { get; private set; }

    private Account(string id, long balanceInCents = 0L)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Account id cannot be empty.");
        if (balanceInCents < 0)
            throw new ArgumentException("Balance cannot be negative.");

        Id = id;
        BalanceInCents = balanceInCents;
    }

    public static Account CreateNew(string id) => new(id);

    public void Deposit(long amountInCents)
    {
        if (amountInCents <= 0)
            throw new ArgumentException("Deposit amount must be greater than zero.");

        BalanceInCents += amountInCents;
    }

    public void Withdraw(long amountInCents)
    {
        if (amountInCents <= 0)
            throw new ArgumentException("Withdrawal amount must be greater than zero.");

        if (BalanceInCents < amountInCents)
            throw new InvalidOperationException("Insufficient funds.");

        BalanceInCents -= amountInCents;
    }
}
