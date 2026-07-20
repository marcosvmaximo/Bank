using Ebanx.Domain;

namespace Ebanx.Tests.UnitTests;

public class AccountTests
{
    [Fact]
    public void CreateNew_WithValidId_ShouldInitializeZeroBalance()
    {
        var account = Account.CreateNew("100");

        Assert.Equal("100", account.Id);
        Assert.Equal(0L, account.BalanceInCents);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null!)]
    public void CreateNew_WithEmptyOrNullId_ShouldThrowArgumentException(string? id)
    {
        Assert.Throws<ArgumentException>(() => Account.CreateNew(id!));
    }

    [Fact]
    public void Deposit_ValidAmount_ShouldIncreaseBalance()
    {
        var account = Account.CreateNew("100");

        account.Deposit(1500L);

        Assert.Equal(1500L, account.BalanceInCents);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-100L)]
    public void Deposit_ZeroOrNegativeAmount_ShouldThrowArgumentException(long invalidAmount)
    {
        var account = Account.CreateNew("100");

        Assert.Throws<ArgumentException>(() => account.Deposit(invalidAmount));
        Assert.Equal(0L, account.BalanceInCents);
    }

    [Fact]
    public void Withdraw_SufficientFunds_ShouldDecreaseBalance()
    {
        var account = Account.CreateNew("100");
        account.Deposit(5000L);

        account.Withdraw(2000L);

        Assert.Equal(3000L, account.BalanceInCents);
    }

    [Fact]
    public void Withdraw_InsufficientFunds_ShouldThrowInvalidOperationExceptionAndNotAlterBalance()
    {
        var account = Account.CreateNew("100");
        account.Deposit(1000L);

        Assert.Throws<InvalidOperationException>(() => account.Withdraw(2000L));
        Assert.Equal(1000L, account.BalanceInCents);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-500L)]
    public void Withdraw_ZeroOrNegativeAmount_ShouldThrowArgumentException(long invalidAmount)
    {
        var account = Account.CreateNew("100");
        account.Deposit(1000L);

        Assert.Throws<ArgumentException>(() => account.Withdraw(invalidAmount));
        Assert.Equal(1000L, account.BalanceInCents);
    }
}
