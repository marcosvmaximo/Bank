using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ebanx.Application.DTOs;
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
    public async Task OfficialChallengeSpec_Walkthrough_ShouldMatchExactExpectedResponses()
    {
        var resetRes = await _client.PostAsync("/reset", null);
        Assert.Equal(HttpStatusCode.OK, resetRes.StatusCode);

        var get1234Res = await _client.GetAsync("/balance?account_id=1234");
        Assert.Equal(HttpStatusCode.NotFound, get1234Res.StatusCode);
        Assert.Equal("0", await GetBodyAsync(get1234Res));

        var dep1Res = await PostEventAsync(new { type = "deposit", destination = "100", amount = 10 });
        Assert.Equal(HttpStatusCode.Created, dep1Res.StatusCode);
        var dep1Json = JsonDocument.Parse(await GetBodyAsync(dep1Res)).RootElement;
        Assert.Equal("100", dep1Json.GetProperty("destination").GetProperty("id").GetString());
        Assert.Equal(10, dep1Json.GetProperty("destination").GetProperty("balance").GetDecimal());

        var dep2Res = await PostEventAsync(new { type = "deposit", destination = "100", amount = 10 });
        Assert.Equal(HttpStatusCode.Created, dep2Res.StatusCode);
        var dep2Json = JsonDocument.Parse(await GetBodyAsync(dep2Res)).RootElement;
        Assert.Equal("100", dep2Json.GetProperty("destination").GetProperty("id").GetString());
        Assert.Equal(20, dep2Json.GetProperty("destination").GetProperty("balance").GetDecimal());

        var get100Res = await _client.GetAsync("/balance?account_id=100");
        Assert.Equal(HttpStatusCode.OK, get100Res.StatusCode);
        Assert.Equal("20", await GetBodyAsync(get100Res));

        var wit200Res = await PostEventAsync(new { type = "withdraw", origin = "200", amount = 10 });
        Assert.Equal(HttpStatusCode.NotFound, wit200Res.StatusCode);
        Assert.Equal("0", await GetBodyAsync(wit200Res));

        var wit100Res = await PostEventAsync(new { type = "withdraw", origin = "100", amount = 5 });
        Assert.Equal(HttpStatusCode.Created, wit100Res.StatusCode);
        var wit100Json = JsonDocument.Parse(await GetBodyAsync(wit100Res)).RootElement;
        Assert.Equal("100", wit100Json.GetProperty("origin").GetProperty("id").GetString());
        Assert.Equal(15, wit100Json.GetProperty("origin").GetProperty("balance").GetDecimal());

        var trans1Res = await PostEventAsync(new { type = "transfer", origin = "100", amount = 15, destination = "300" });
        Assert.Equal(HttpStatusCode.Created, trans1Res.StatusCode);
        var trans1Json = JsonDocument.Parse(await GetBodyAsync(trans1Res)).RootElement;
        Assert.Equal("100", trans1Json.GetProperty("origin").GetProperty("id").GetString());
        Assert.Equal(0, trans1Json.GetProperty("origin").GetProperty("balance").GetDecimal());
        Assert.Equal("300", trans1Json.GetProperty("destination").GetProperty("id").GetString());
        Assert.Equal(15, trans1Json.GetProperty("destination").GetProperty("balance").GetDecimal());

        var trans2Res = await PostEventAsync(new { type = "transfer", origin = "200", amount = 15, destination = "300" });
        Assert.Equal(HttpStatusCode.NotFound, trans2Res.StatusCode);
        Assert.Equal("0", await GetBodyAsync(trans2Res));
    }
}
