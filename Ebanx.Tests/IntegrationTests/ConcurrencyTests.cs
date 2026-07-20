using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Ebanx.Tests.IntegrationTests;

public class ConcurrencyTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ConcurrencyTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    private Task ResetAsync() => _client.PostAsync("/reset", null);

    private Task<HttpResponseMessage> PostEventAsync(object payload) =>
        _client.PostAsJsonAsync("/event", payload);

    private async Task<decimal> GetBalanceAsync(string accountId)
    {
        var response = await _client.GetAsync($"/balance?account_id={accountId}");
        var body = await response.Content.ReadAsStringAsync();
        return decimal.Parse(body);
    }

    [Fact]
    public async Task ConcurrentDeposits_AllDepositsMustBeAccounted()
    {
        await ResetAsync();

        const int threads = 50;
        const decimal depositAmount = 10m;

        var tasks = Enumerable.Range(0, threads)
            .Select(_ => PostEventAsync(new { type = "deposit", destination = "ACC", amount = depositAmount }));

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));

        var balance = await GetBalanceAsync("ACC");
        Assert.Equal(threads * depositAmount, balance);
    }

    [Fact]
    public async Task ConcurrentReverseTransfers_ShouldNotDeadlock()
    {
        await ResetAsync();

        await PostEventAsync(new { type = "deposit", destination = "A", amount = 10000 });
        await PostEventAsync(new { type = "deposit", destination = "B", amount = 10000 });

        const int threads = 30;

        var tasks = Enumerable.Range(0, threads)
            .SelectMany(_ => new[]
            {
                PostEventAsync(new { type = "transfer", origin = "A", destination = "B", amount = 1 }),
                PostEventAsync(new { type = "transfer", origin = "B", destination = "A", amount = 1 })
            });

        var results = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task ConcurrentTransfers_TotalMoneyMustBeConserved()
    {
        await ResetAsync();

        const decimal initialBalance = 1000m;
        const int threads = 40;

        await PostEventAsync(new { type = "deposit", destination = "X", amount = initialBalance });
        await PostEventAsync(new { type = "deposit", destination = "Y", amount = initialBalance });

        var tasks = Enumerable.Range(0, threads)
            .SelectMany(i => new[]
            {
                PostEventAsync(new { type = "transfer", origin = "X", destination = "Y", amount = 1 }),
                PostEventAsync(new { type = "transfer", origin = "Y", destination = "X", amount = 1 })
            });

        await Task.WhenAll(tasks);

        var balanceX = await GetBalanceAsync("X");
        var balanceY = await GetBalanceAsync("Y");

        Assert.Equal(initialBalance * 2, balanceX + balanceY);
    }

    [Fact]
    public async Task Transfer_InsufficientFunds_NeitherAccountIsModified()
    {
        await ResetAsync();

        await PostEventAsync(new { type = "deposit", destination = "RICH", amount = 5 });
        await PostEventAsync(new { type = "deposit", destination = "POOR", amount = 5 });

        var response = await PostEventAsync(new { type = "transfer", origin = "POOR", destination = "RICH", amount = 100 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        Assert.Equal(5m, await GetBalanceAsync("RICH"));
        Assert.Equal(5m, await GetBalanceAsync("POOR"));
    }

    [Fact]
    public async Task StressTest_MixedOperations_StateRemainsConsistent()
    {
        await ResetAsync();

        await PostEventAsync(new { type = "deposit", destination = "S1", amount = 500 });
        await PostEventAsync(new { type = "deposit", destination = "S2", amount = 500 });

        var tasks = Enumerable.Range(0, 20).SelectMany(_ => new[]
        {
            PostEventAsync(new { type = "deposit", destination = "S1", amount = 10 }),
            PostEventAsync(new { type = "withdraw", origin = "S1", amount = 10 }),
            PostEventAsync(new { type = "transfer", origin = "S1", destination = "S2", amount = 5 }),
            PostEventAsync(new { type = "transfer", origin = "S2", destination = "S1", amount = 5 }),
        });

        await Task.WhenAll(tasks);

        var s1 = await GetBalanceAsync("S1");
        var s2 = await GetBalanceAsync("S2");

        Assert.True(s1 >= 0);
        Assert.True(s2 >= 0);
        Assert.True(s1 + s2 >= 0);
    }
}
