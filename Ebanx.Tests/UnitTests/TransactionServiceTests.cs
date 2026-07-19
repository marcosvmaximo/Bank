using Ebanx.DTOs;
using Ebanx.Repositories;
using Ebanx.Services;

namespace Ebanx.Tests.UnitTests;

public class TransactionServiceTests
{
    private static TransactionService CreateService(out InMemoryAccountRepository repo)
    {
        repo = new InMemoryAccountRepository();
        return new TransactionService(repo);
    }

    [Fact]
    public void Deposit_NewAccount_CreatesAccountWithInitialBalance()
    {
        var service = CreateService(out var repo);

        service.Deposit("100", 10);

        Assert.Equal(10, repo.GetById("100")!.Balance);
    }

    [Fact]
    public void Deposit_ExistingAccount_AccumulatesBalance()
    {
        var service = CreateService(out var repo);
        service.Deposit("100", 10);

        service.Deposit("100", 25);

        Assert.Equal(35, repo.GetById("100")!.Balance);
    }

    [Fact]
    public void Withdraw_ExistingAccount_DeductsBalance()
    {
        var service = CreateService(out var repo);
        service.Deposit("100", 30);

        var result = service.Withdraw("100", 10);

        Assert.NotNull(result);
        Assert.Equal(20, result.Balance);
        Assert.Equal(20, repo.GetById("100")!.Balance);
    }

    [Fact]
    public void Withdraw_NonExistentAccount_ReturnsNull()
    {
        var service = CreateService(out _);

        var result = service.Withdraw("999", 10);

        Assert.Null(result);
    }

    [Fact]
    public void Withdraw_InsufficientFunds_ReturnsNull()
    {
        var service = CreateService(out var repo);
        service.Deposit("100", 5);

        var result = service.Withdraw("100", 20);

        Assert.Null(result);
        Assert.Equal(5, repo.GetById("100")!.Balance);
    }

    [Fact]
    public void Transfer_ValidAccounts_DebitOriginAndCreditDestination()
    {
        var service = CreateService(out var repo);
        service.Deposit("100", 50);
        service.Deposit("300", 10);

        var (origin, destination) = service.Transfer("100", "300", 15);

        Assert.NotNull(origin);
        Assert.NotNull(destination);
        Assert.Equal(35, origin.Balance);
        Assert.Equal(25, destination.Balance);
        Assert.Equal(35, repo.GetById("100")!.Balance);
        Assert.Equal(25, repo.GetById("300")!.Balance);
    }

    [Fact]
    public void Transfer_NonExistentDestination_CreatesDestinationAccount()
    {
        var service = CreateService(out var repo);
        service.Deposit("100", 50);

        var (origin, destination) = service.Transfer("100", "300", 15);

        Assert.NotNull(origin);
        Assert.NotNull(destination);
        Assert.Equal(35, origin.Balance);
        Assert.Equal(15, destination.Balance);
        Assert.NotNull(repo.GetById("300"));
    }

    [Fact]
    public void Transfer_NonExistentOrigin_ReturnsBothNull()
    {
        var service = CreateService(out var repo);

        var (origin, destination) = service.Transfer("999", "300", 10);

        Assert.Null(origin);
        Assert.Null(destination);
        Assert.Null(repo.GetById("300"));
    }

    [Fact]
    public void Transfer_InsufficientFunds_ReturnsBothNullAndDoesNotMutateState()
    {
        var service = CreateService(out var repo);
        service.Deposit("100", 5);
        service.Deposit("300", 10);

        var (origin, destination) = service.Transfer("100", "300", 20);

        Assert.Null(origin);
        Assert.Null(destination);
        Assert.Equal(5, repo.GetById("100")!.Balance);
        Assert.Equal(10, repo.GetById("300")!.Balance);
    }

    [Fact]
    public void ProcessEvent_AmountZeroOrNegative_ThrowsArgumentException()
    {
        var service = CreateService(out _);

        Assert.Throws<ArgumentException>(() =>
            service.ProcessEvent(new EventRequest { Type = "deposit", Destination = "100", Amount = 0 }));

        Assert.Throws<ArgumentException>(() =>
            service.ProcessEvent(new EventRequest { Type = "deposit", Destination = "100", Amount = -5 }));
    }

    [Fact]
    public void ProcessEvent_AutoTransfer_ThrowsArgumentException()
    {
        var service = CreateService(out _);
        service.Deposit("100", 50);

        Assert.Throws<ArgumentException>(() =>
            service.ProcessEvent(new EventRequest { Type = "transfer", Origin = "100", Destination = "100", Amount = 10 }));
    }

    [Fact]
    public void ProcessEvent_UnknownType_ThrowsArgumentException()
    {
        var service = CreateService(out _);

        Assert.Throws<ArgumentException>(() =>
            service.ProcessEvent(new EventRequest { Type = "invalid", Amount = 10 }));
    }

    [Fact]
    public void Reset_ClearsAllAccounts()
    {
        var service = CreateService(out var repo);
        service.Deposit("100", 50);
        service.Deposit("200", 30);

        service.Reset();

        Assert.Null(repo.GetById("100"));
        Assert.Null(repo.GetById("200"));
    }
}
