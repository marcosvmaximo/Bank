using Ebanx.DTOs;
using Ebanx.Models;
using Ebanx.Repositories;

namespace Ebanx.Services;

public class TransactionService(IAccountRepository repository)
{
    public EventResponse? ProcessEvent(EventRequest request)
    {
        if (request.Amount <= 0)
            throw new ArgumentException("Amount must be greater than zero.");

        return request.Type switch
        {
            "deposit" => HandleDeposit(request),
            "withdraw" => HandleWithdraw(request),
            "transfer" => HandleTransfer(request),
            _ => throw new ArgumentException("Unknown event type.")
        };
    }
    
    public Account Deposit(string destinationId, decimal amount)
    {
        var existing = repository.GetById(destinationId);
        var newBalance = (existing?.Balance ?? 0m) + amount;
        return repository.Upsert(new Account(destinationId, newBalance));
    }

    public Account? Withdraw(string originId, decimal amount)
    {
        var existing = repository.GetById(originId);
        if (existing is null)
            return null;

        if (existing.Balance < amount)
            return null;

        return repository.Upsert(existing with { Balance = existing.Balance - amount });
    }

    public (Account? Origin, Account? Destination) Transfer(
        string originId, string destinationId, decimal amount)
    {
        var success = repository.Transfer(originId, destinationId, amount,
            out var origin, out var destination);

        return success ? (origin, destination) : (null, null);
    }

    public Account? GetAccount(string id) => repository.GetById(id);

    public void Reset() => repository.Reset();
    
    private EventResponse HandleDeposit(EventRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Destination))
            throw new ArgumentException("Destination is required.");

        var account = Deposit(request.Destination, request.Amount);
        return new EventResponse 
        { 
            Destination = new AccountDto { Id = account.Id, Balance = account.Balance } 
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
            Origin = new AccountDto { Id = account.Id, Balance = account.Balance } 
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
            Origin = new AccountDto { Id = origin.Id, Balance = origin.Balance },
            Destination = new AccountDto { Id = destination!.Id, Balance = destination.Balance }
        };
    }
}
