using Ebanx.Application.DTOs;
using Ebanx.Domain;

namespace Ebanx.Application;

public class TransactionService(IAccountRepository repository, IUnitOfWork unitOfWork)
{
    // Fator de conversão: 1 unidade monetária = 100 centavos
    private const long CentsFactor = 100L;

    public EventResponse? ProcessEvent(EventRequest request)
    {
        if (request.Amount <= 0)
            throw new ArgumentException("Amount must be greater than zero.");

        return request.Type switch
        {
            "deposit"  => HandleDeposit(request),
            "withdraw" => HandleWithdraw(request),
            "transfer" => HandleTransfer(request),
            _          => throw new ArgumentException("Unknown event type.")
        };
    }

    public Account Deposit(string destinationId, decimal amount)
    {
        var amountInCents = ToCents(amount);
        Account? result = null;

        unitOfWork.Execute([destinationId], () =>
        {
            var account = repository.GetById(destinationId) ?? Account.CreateNew(destinationId);
            account.Deposit(amountInCents);
            result = repository.Upsert(account);
            return true;
        });

        return result!;
    }

    public Account? Withdraw(string originId, decimal amount)
    {
        var amountInCents = ToCents(amount);
        Account? result = null;

        unitOfWork.Execute([originId], () =>
        {
            var account = repository.GetById(originId);
            if (account is null) return false;

            try
            {
                account.Withdraw(amountInCents);
            }
            catch (InvalidOperationException)
            {
                return false; // saldo insuficiente → 404
            }

            result = repository.Upsert(account);
            return true;
        });

        return result;
    }

    public (Account? Origin, Account? Destination) Transfer(
        string originId, string destinationId, decimal amount)
    {
        var amountInCents = ToCents(amount);
        Account? origin = null;
        Account? destination = null;

        var success = unitOfWork.Execute([originId, destinationId], () =>
        {
            origin = repository.GetById(originId);
            if (origin is null) return false;

            var dest = repository.GetById(destinationId) ?? Account.CreateNew(destinationId);

            try
            {
                origin.Withdraw(amountInCents);
                dest.Deposit(amountInCents);
            }
            catch (InvalidOperationException)
            {
                return false; // saldo insuficiente
            }

            repository.Upsert(origin);
            repository.Upsert(dest);
            destination = dest;
            return true;
        });

        return success ? (origin, destination) : (null, null);
    }

    public decimal? GetBalance(string id)
    {
        var account = repository.GetById(id);
        return account is null ? null : ToDecimal(account.BalanceInCents);
    }

    public void Reset() => repository.Reset();

    // Converte decimal → long centavos (fronteira API → domínio)
    private static long ToCents(decimal amount) => (long)(amount * CentsFactor);

    // Converte long centavos → decimal (fronteira domínio → API)
    private static decimal ToDecimal(long cents) => cents / (decimal)CentsFactor;

    private EventResponse HandleDeposit(EventRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Destination))
            throw new ArgumentException("Destination is required.");

        var account = Deposit(request.Destination, request.Amount);
        return new EventResponse
        {
            Destination = new AccountDto { Id = account.Id, Balance = ToDecimal(account.BalanceInCents) }
        };
    }

    private EventResponse? HandleWithdraw(EventRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Origin))
            throw new ArgumentException("Origin is required.");

        var account = Withdraw(request.Origin, request.Amount);
        if (account is null) return null;

        return new EventResponse
        {
            Origin = new AccountDto { Id = account.Id, Balance = ToDecimal(account.BalanceInCents) }
        };
    }

    private EventResponse? HandleTransfer(EventRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Origin) || string.IsNullOrWhiteSpace(request.Destination))
            throw new ArgumentException("Origin and Destination are required.");

        if (request.Origin == request.Destination)
            throw new ArgumentException("Origin and Destination must be different accounts.");

        var (origin, destination) = Transfer(request.Origin, request.Destination, request.Amount);
        if (origin is null) return null;

        return new EventResponse
        {
            Origin      = new AccountDto { Id = origin.Id,       Balance = ToDecimal(origin.BalanceInCents) },
            Destination = new AccountDto { Id = destination!.Id, Balance = ToDecimal(destination.BalanceInCents) }
        };
    }
}
