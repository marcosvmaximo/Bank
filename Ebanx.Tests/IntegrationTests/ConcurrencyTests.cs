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
    public async Task StressTest_MixedOperations_MoneyIsConservedAndBalancesAreNonNegative()
    {
        await ResetAsync();

        const decimal initial = 500m;
        const int rounds = 20;
        const decimal depositPerRound = 10m;

        await PostEventAsync(new { type = "deposit", destination = "S1", amount = initial });
        await PostEventAsync(new { type = "deposit", destination = "S2", amount = initial });

        var tasks = Enumerable.Range(0, rounds).SelectMany(_ => new[]
        {
            PostEventAsync(new { type = "deposit",  destination = "S1", amount = depositPerRound }),
            PostEventAsync(new { type = "withdraw", origin = "S1",      amount = depositPerRound }),
            PostEventAsync(new { type = "transfer", origin = "S1", destination = "S2", amount = 5 }),
            PostEventAsync(new { type = "transfer", origin = "S2", destination = "S1", amount = 5 }),
        });

        var responses = await Task.WhenAll(tasks);

        var s1 = await GetBalanceAsync("S1");
        var s2 = await GetBalanceAsync("S2");

        Assert.True(s1 >= 0, $"S1 ficou negativo: {s1}");
        Assert.True(s2 >= 0, $"S2 ficou negativo: {s2}");

        var minExpected = initial * 2;
        var maxExpected = initial * 2 + depositPerRound * rounds;
        Assert.InRange(s1 + s2, minExpected, maxExpected);
    }

    [Fact]
    public async Task IdempotencyFilter_ConcurrentRequestsSameKey_ShouldExecuteOperationOnlyOnce()
    {
        await ResetAsync();

        const int concurrentRequests = 30;
        const decimal depositAmount = 100m;
        var idempotencyKey = Guid.NewGuid().ToString();

        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(_ =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "/event");
                request.Headers.Add("Idempotency-Key", idempotencyKey);
                request.Content = JsonContent.Create(new
                {
                    type = "deposit",
                    destination = "IDM_ACCOUNT",
                    amount = depositAmount
                });
                return _client.SendAsync(request);
            });

        var responses = await Task.WhenAll(tasks);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));

        var balance = await GetBalanceAsync("IDM_ACCOUNT");
        Assert.Equal(depositAmount, balance);
    }
}
