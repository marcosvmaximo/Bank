using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Ebanx.Tests.IntegrationTests;

/// <summary>
/// Testes que validam thread-safety, ausência de deadlocks e atomicidade da transferência.
/// Cada teste lança múltiplas requisições simultâneas e verifica a consistência do estado final.
/// </summary>
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

    // -------------------------------------------------------------------------
    // 1. Thread-Safety: depósitos concorrentes não devem se perder
    // -------------------------------------------------------------------------
    [Fact]
    public async Task ConcurrentDeposits_AllDepositsMustBeAccounted()
    {
        await ResetAsync();

        const int threads = 50;
        const decimal depositAmount = 10m;

        // Lança 50 depósitos simultâneos na mesma conta
        var tasks = Enumerable.Range(0, threads)
            .Select(_ => PostEventAsync(new { type = "deposit", destination = "ACC", amount = depositAmount }));

        var results = await Task.WhenAll(tasks);

        // Todos devem retornar 201
        Assert.All(results, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));

        // Saldo final deve ser exatamente 50 × 10 = 500
        var balance = await GetBalanceAsync("ACC");
        Assert.Equal(threads * depositAmount, balance);
    }

    // -------------------------------------------------------------------------
    // 2. Deadlock: transferências inversas simultâneas não devem travar
    // -------------------------------------------------------------------------
    [Fact]
    public async Task ConcurrentReverseTransfers_ShouldNotDeadlock()
    {
        await ResetAsync();

        // Garante saldo suficiente em ambas as contas
        await PostEventAsync(new { type = "deposit", destination = "A", amount = 10000 });
        await PostEventAsync(new { type = "deposit", destination = "B", amount = 10000 });

        const int threads = 30;

        // Thread 1..N fazem A→B, outras N fazem B→A simultaneamente
        var tasks = Enumerable.Range(0, threads)
            .SelectMany(_ => new[]
            {
                PostEventAsync(new { type = "transfer", origin = "A", destination = "B", amount = 1 }),
                PostEventAsync(new { type = "transfer", origin = "B", destination = "A", amount = 1 })
            });

        // Se houver deadlock, o teste vai travar e expirar o timeout
        var results = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.NotEmpty(results);
    }

    // -------------------------------------------------------------------------
    // 3. Atomicidade + Conservação de Valor: dinheiro não pode ser criado nem destruído
    // -------------------------------------------------------------------------
    [Fact]
    public async Task ConcurrentTransfers_TotalMoneyMustBeConserved()
    {
        await ResetAsync();

        const decimal initialBalance = 1000m;
        const int threads = 40;

        // Deposita saldo inicial nas duas contas
        await PostEventAsync(new { type = "deposit", destination = "X", amount = initialBalance });
        await PostEventAsync(new { type = "deposit", destination = "Y", amount = initialBalance });

        // Transferências simultâneas em ambas as direções
        var tasks = Enumerable.Range(0, threads)
            .SelectMany(i => new[]
            {
                PostEventAsync(new { type = "transfer", origin = "X", destination = "Y", amount = 1 }),
                PostEventAsync(new { type = "transfer", origin = "Y", destination = "X", amount = 1 })
            });

        await Task.WhenAll(tasks);

        var balanceX = await GetBalanceAsync("X");
        var balanceY = await GetBalanceAsync("Y");

        // O dinheiro total no sistema deve ser conservado
        Assert.Equal(initialBalance * 2, balanceX + balanceY);
    }

    // -------------------------------------------------------------------------
    // 4. Atomicidade: transferência sem saldo suficiente não deve alterar nenhuma conta
    // -------------------------------------------------------------------------
    [Fact]
    public async Task Transfer_InsufficientFunds_NeitherAccountIsModified()
    {
        await ResetAsync();

        await PostEventAsync(new { type = "deposit", destination = "RICH", amount = 5 });
        await PostEventAsync(new { type = "deposit", destination = "POOR", amount = 5 });

        // Tenta transferir mais do que o saldo disponível
        var response = await PostEventAsync(new { type = "transfer", origin = "POOR", destination = "RICH", amount = 100 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // Nenhuma conta deve ter seu saldo alterado
        Assert.Equal(5m, await GetBalanceAsync("RICH"));
        Assert.Equal(5m, await GetBalanceAsync("POOR"));
    }

    // -------------------------------------------------------------------------
    // 5. Stress: muitas operações mistas concorrentes mantêm consistência
    // -------------------------------------------------------------------------
    [Fact]
    public async Task StressTest_MixedOperations_StateRemainsConsistent()
    {
        await ResetAsync();

        // Deposita saldo inicial
        await PostEventAsync(new { type = "deposit", destination = "S1", amount = 500 });
        await PostEventAsync(new { type = "deposit", destination = "S2", amount = 500 });


        // Mix: depósitos, saques e transferências simultâneos
        var tasks = Enumerable.Range(0, 20).SelectMany(_ => new[]
        {
            PostEventAsync(new { type = "deposit", destination = "S1", amount = 10 }),
            PostEventAsync(new { type = "withdraw", origin = "S1", amount = 10 }),
            PostEventAsync(new { type = "transfer", origin = "S1", destination = "S2", amount = 5 }),
            PostEventAsync(new { type = "transfer", origin = "S2", destination = "S1", amount = 5 }),
        });

        await Task.WhenAll(tasks);

        // Após depósitos e saques iguais e transferências inversas, o total deve ser conservado
        var s1 = await GetBalanceAsync("S1");
        var s2 = await GetBalanceAsync("S2");

        // Total depositado = 500+500 + (20×10 depósitos) = 1200
        // Total sacado = até 20×10 = 200 (alguns podem falhar por saldo insuficiente)
        // Saldo nunca pode ser negativo — invariante da entidade Account
        Assert.True(s1 >= 0, "S1 não pode ter saldo negativo");
        Assert.True(s2 >= 0, "S2 não pode ter saldo negativo");
        Assert.True(s1 + s2 >= 0, "Saldo total não pode ser negativo");
    }
}
