using Ebanx.Infrastructure;

namespace Ebanx.Tests.UnitTests;

public class UnitOfWorkTests
{
    [Fact]
    public async Task Execute_ConcurrentExecutionOnSameAccount_ShouldExecuteSequentially()
    {
        var uow = new UnitOfWork();
        var executionOrder = new List<int>();

        var task1 = Task.Run(() =>
        {
            uow.Execute(["A"], () =>
            {
                Thread.Sleep(100);
                executionOrder.Add(1);
                return true;
            });
        });

        await Task.Delay(20);

        var task2 = Task.Run(() =>
        {
            uow.Execute(["A"], () =>
            {
                executionOrder.Add(2);
                return true;
            });
        });

        await Task.WhenAll(task1, task2);

        Assert.Equal([1, 2], executionOrder);
    }

    [Fact]
    public async Task Execute_WhenOperationThrowsException_ShouldReleaseAllLocksAndRethrow()
    {
        var uow = new UnitOfWork();

        Assert.Throws<InvalidOperationException>(() =>
            uow.Execute<int>(["A", "B"], () => throw new InvalidOperationException("Erro simulado")));

        var completed = false;
        var task = Task.Run(() =>
        {
            uow.Execute(["A", "B"], () => completed = true);
        });

        await task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.True(completed);
    }

    [Fact]
    public void Execute_WithDuplicateIds_ShouldOnlyAcquireDistinctLocks()
    {
        var uow = new UnitOfWork();

        var result = uow.Execute(["A", "A"], () => 42);

        Assert.Equal(42, result);
    }
}
