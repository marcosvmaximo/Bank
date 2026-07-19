using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ebanx.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Ebanx.Tests.IntegrationTests;

public class EventApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public EventApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    private async Task ResetAsync() =>
        await _client.PostAsync("/reset", null);

    private async Task<HttpResponseMessage> PostEventAsync(object payload) =>
        await _client.PostAsJsonAsync("/event", payload);

    private async Task<string> GetBodyAsync(HttpResponseMessage response) =>
        await response.Content.ReadAsStringAsync();

    [Fact]
    public async Task Reset_Returns200WithOkBody()
    {
        var response = await _client.PostAsync("/reset", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetBalance_NonExistentAccount_Returns404WithBodyZero()
    {
        await ResetAsync();

        var response = await _client.GetAsync("/balance?account_id=999");
        var body = await GetBodyAsync(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("0", body);
    }

    [Fact]
    public async Task Deposit_NewAccount_Returns201WithCorrectDestinationBalance()
    {
        await ResetAsync();

        var response = await PostEventAsync(new { type = "deposit", destination = "100", amount = 10 });
        var body = await GetBodyAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var json = JsonDocument.Parse(body).RootElement;
        Assert.Equal("100", json.GetProperty("destination").GetProperty("id").GetString());
        Assert.Equal(10, json.GetProperty("destination").GetProperty("balance").GetDecimal());
    }

    [Fact]
    public async Task Deposit_ExistingAccount_AccumulatesBalance()
    {
        await ResetAsync();
        await PostEventAsync(new { type = "deposit", destination = "100", amount = 10 });

        var response = await PostEventAsync(new { type = "deposit", destination = "100", amount = 20 });
        var body = await GetBodyAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var json = JsonDocument.Parse(body).RootElement;
        Assert.Equal(30, json.GetProperty("destination").GetProperty("balance").GetDecimal());
    }

    [Fact]
    public async Task GetBalance_AfterDeposit_ReturnsUpdatedBalance()
    {
        await ResetAsync();
        await PostEventAsync(new { type = "deposit", destination = "100", amount = 20 });

        var response = await _client.GetAsync("/balance?account_id=100");
        var body = await GetBodyAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("20", body);
    }

    [Fact]
    public async Task Withdraw_ExistingAccount_Returns201WithReducedBalance()
    {
        await ResetAsync();
        await PostEventAsync(new { type = "deposit", destination = "100", amount = 30 });

        var response = await PostEventAsync(new { type = "withdraw", origin = "100", amount = 10 });
        var body = await GetBodyAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var json = JsonDocument.Parse(body).RootElement;
        Assert.Equal("100", json.GetProperty("origin").GetProperty("id").GetString());
        Assert.Equal(20, json.GetProperty("origin").GetProperty("balance").GetDecimal());
    }

    [Fact]
    public async Task Withdraw_NonExistentAccount_Returns404WithBodyZero()
    {
        await ResetAsync();

        var response = await PostEventAsync(new { type = "withdraw", origin = "999", amount = 10 });
        var body = await GetBodyAsync(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("0", body);
    }

    [Fact]
    public async Task Withdraw_InsufficientFunds_Returns404WithBodyZero()
    {
        await ResetAsync();
        await PostEventAsync(new { type = "deposit", destination = "100", amount = 5 });

        var response = await PostEventAsync(new { type = "withdraw", origin = "100", amount = 20 });
        var body = await GetBodyAsync(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("0", body);
    }

    [Fact]
    public async Task Transfer_ValidAccounts_Returns201WithBothUpdatedBalances()
    {
        await ResetAsync();
        await PostEventAsync(new { type = "deposit", destination = "100", amount = 50 });
        await PostEventAsync(new { type = "deposit", destination = "300", amount = 10 });

        var response = await PostEventAsync(new { type = "transfer", origin = "100", destination = "300", amount = 15 });
        var body = await GetBodyAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var json = JsonDocument.Parse(body).RootElement;
        Assert.Equal(35, json.GetProperty("origin").GetProperty("balance").GetDecimal());
        Assert.Equal(25, json.GetProperty("destination").GetProperty("balance").GetDecimal());
    }

    [Fact]
    public async Task Transfer_NonExistentOrigin_Returns404WithBodyZero()
    {
        await ResetAsync();

        var response = await PostEventAsync(new { type = "transfer", origin = "999", destination = "300", amount = 10 });
        var body = await GetBodyAsync(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("0", body);
    }

    [Fact]
    public async Task Transfer_NonExistentDestination_CreatesItAndPersistsState()
    {
        await ResetAsync();
        await PostEventAsync(new { type = "deposit", destination = "100", amount = 50 });

        var response = await PostEventAsync(new { type = "transfer", origin = "100", destination = "300", amount = 15 });
        var body = await GetBodyAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var json = JsonDocument.Parse(body).RootElement;
        Assert.Equal(35, json.GetProperty("origin").GetProperty("balance").GetDecimal());
        Assert.Equal(15, json.GetProperty("destination").GetProperty("balance").GetDecimal());

        var balanceResponse = await _client.GetAsync("/balance?account_id=300");
        var balanceBody = await GetBodyAsync(balanceResponse);
        Assert.Equal(HttpStatusCode.OK, balanceResponse.StatusCode);
        Assert.Equal("15", balanceBody);
    }

    [Fact]
    public async Task FullFlow_DepositWithdrawTransfer_MaintainsConsistentState()
    {
        await ResetAsync();

        await PostEventAsync(new { type = "deposit", destination = "100", amount = 100 });
        await PostEventAsync(new { type = "deposit", destination = "200", amount = 50 });

        await PostEventAsync(new { type = "withdraw", origin = "100", amount = 30 });

        await PostEventAsync(new { type = "transfer", origin = "100", destination = "200", amount = 20 });

        var balance100 = await _client.GetAsync("/balance?account_id=100");
        var balance200 = await _client.GetAsync("/balance?account_id=200");

        Assert.Equal("50", await GetBodyAsync(balance100));
        Assert.Equal("70", await GetBodyAsync(balance200));
    }
}
