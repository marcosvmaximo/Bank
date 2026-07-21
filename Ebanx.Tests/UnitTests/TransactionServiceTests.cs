using Ebanx.Application;
using Ebanx.Application.DTOs;
using Ebanx.Domain;
using Ebanx.Infrastructure;

namespace Ebanx.Tests.UnitTests;

public class TransactionServiceTests
{
    private static TransactionService CreateService(out IAccountRepository repo, out IUnitOfWork uow)
    {
        repo = new AccountRepository();
        uow = new UnitOfWork();
        return new TransactionService(repo, uow);
    }

    [Fact]
    public void Deposit_NewAccount_CreatesAccountAndStoresBalance()
    {
        var service = CreateService(out var repo, out _);

        var account = service.Deposit("100", 10.50m);

        Assert.Equal("100", account.Id);
        Assert.Equal(1050L, account.BalanceInCents);
        Assert.Equal(1050L, repo.GetById("100")!.BalanceInCents);
    }

    [Fact]
    public void Deposit_ExistingAccount_AccumulatesBalanceCorrectly()
    {
        var service = CreateService(out _, out _);
        service.Deposit("100", 20m);

        var account = service.Deposit("100", 15.25m);

        Assert.Equal(3525L, account.BalanceInCents);
        Assert.Equal(35.25m, service.GetBalance("100"));
    }

    [Fact]
    public void Withdraw_ExistingAccountWithSufficientFunds_ReturnsUpdatedAccount()
    {
        var service = CreateService(out _, out _);
        service.Deposit("100", 50m);

        var account = service.Withdraw("100", 20m);

        Assert.NotNull(account);
        Assert.Equal(3000L, account.BalanceInCents);
    }

    [Fact]
    public void Withdraw_NonExistentAccount_ReturnsNull()
    {
        var service = CreateService(out _, out _);

        var account = service.Withdraw("999", 10m);

        Assert.Null(account);
    }

    [Fact]
    public void Withdraw_InsufficientFunds_ReturnsNullAndDoesNotModifyBalance()
    {
        var service = CreateService(out var repo, out _);
        service.Deposit("100", 5m);

        var account = service.Withdraw("100", 20m);

        Assert.Null(account);
        Assert.Equal(500L, repo.GetById("100")!.BalanceInCents);
    }

    [Fact]
    public void Transfer_ValidAccounts_ReturnsUpdatedOriginAndDestination()
    {
        var service = CreateService(out var repo, out _);
        service.Deposit("100", 100m);
        service.Deposit("200", 20m);

        var (origin, destination) = service.Transfer("100", "200", 30m);

        Assert.NotNull(origin);
        Assert.NotNull(destination);
        Assert.Equal(7000L, origin.BalanceInCents);
        Assert.Equal(5000L, destination.BalanceInCents);
        Assert.Equal(70m, service.GetBalance("100"));
        Assert.Equal(50m, service.GetBalance("200"));
    }

    [Fact]
    public void Transfer_NonExistentDestination_CreatesDestinationAndTransfersFunds()
    {
        var service = CreateService(out var repo, out _);
        service.Deposit("100", 50m);

        var (origin, destination) = service.Transfer("100", "300", 15m);

        Assert.NotNull(origin);
        Assert.NotNull(destination);
        Assert.Equal("300", destination.Id);
        Assert.Equal(3500L, origin.BalanceInCents);
        Assert.Equal(1500L, destination.BalanceInCents);
    }

    [Fact]
    public void Transfer_NonExistentOrigin_ReturnsNullTupleAndDoesNotCreateDestination()
    {
        var service = CreateService(out var repo, out _);

        var (origin, destination) = service.Transfer("999", "300", 10m);

        Assert.Null(origin);
        Assert.Null(destination);
        Assert.Null(repo.GetById("300"));
    }

    [Fact]
    public void Transfer_InsufficientFunds_ReturnsNullTupleAndMaintainsState()
    {
        var service = CreateService(out var repo, out _);
        service.Deposit("100", 10m);
        service.Deposit("200", 50m);

        var (origin, destination) = service.Transfer("100", "200", 30m);

        Assert.Null(origin);
        Assert.Null(destination);
        Assert.Equal(1000L, repo.GetById("100")!.BalanceInCents);
        Assert.Equal(5000L, repo.GetById("200")!.BalanceInCents);
    }

    [Fact]
    public void ProcessEvent_AmountZeroOrNegative_ThrowsArgumentException()
    {
        var service = CreateService(out _, out _);

        Assert.Throws<ArgumentException>(() =>
            service.ProcessEvent(new EventRequest { Type = "deposit", Destination = "100", Amount = 0 }));

        Assert.Throws<ArgumentException>(() =>
            service.ProcessEvent(new EventRequest { Type = "deposit", Destination = "100", Amount = -10 }));
    }

    [Fact]
    public void ProcessEvent_SelfTransfer_ThrowsArgumentException()
    {
        var service = CreateService(out _, out _);
        service.Deposit("100", 50m);

        Assert.Throws<ArgumentException>(() =>
            service.ProcessEvent(new EventRequest { Type = "transfer", Origin = "100", Destination = "100", Amount = 10 }));
    }

    [Fact]
    public void Reset_ShouldClearAllAccounts()
    {
        var service = CreateService(out var repo, out _);
        service.Deposit("100", 50m);
        service.Deposit("200", 30m);

        service.Reset();

        Assert.Null(service.GetBalance("100"));
        Assert.Null(service.GetBalance("200"));
    }
}
