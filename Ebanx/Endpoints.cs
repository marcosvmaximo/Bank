using Ebanx.DTOs;
using Ebanx.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ebanx;

public static class Endpoints
{
    public static void MapEndpoints(this IEndpointRouteBuilder app)
    {
        // 1. Reset
        app.MapPost("/reset", (TransactionService transactionService) =>
        {
            transactionService.Reset();
            return Results.Ok();
        });

        // 2. Balance
        app.MapGet("/balance", ([FromQuery(Name = "account_id")] string accountId, TransactionService transactionService) =>
        {
            var account = transactionService.GetAccount(accountId);
            return account is null 
                ? Results.NotFound(0) 
                : Results.Ok(account.Balance);
        });

        // 3. Handle Event
        app.MapPost("/event", (EventRequest request, TransactionService transactionService) =>
        {
            return request.Type switch
            {
                "deposit" => HandleDeposit(request, transactionService),
                "withdraw" => HandleWithdraw(request, transactionService),
                "transfer" => HandleTransfer(request, transactionService),
                _ => Results.BadRequest("Unknown event type.")
            };
        });
    }

    // Métodos auxiliares estáticos mantendo a mesma lógica da sua Controller
    private static IResult HandleDeposit(EventRequest request, TransactionService transactionService)
    {
        if (string.IsNullOrWhiteSpace(request.Destination))
            return Results.BadRequest("Destination is required for deposit.");

        var account = transactionService.Deposit(request.Destination, request.Amount);
        
        return Results.Created(string.Empty, new
        {
            destination = new AccountDto { Id = account.Id, Balance = account.Balance }
        });
    }

    private static IResult HandleWithdraw(EventRequest request, TransactionService transactionService)
    {
        if (string.IsNullOrWhiteSpace(request.Origin))
            return Results.BadRequest("Origin is required for withdraw.");

        var account = transactionService.Withdraw(request.Origin, request.Amount);
        
        return account is null
            ? Results.NotFound(0)
            : Results.Created(string.Empty, new
            {
                origin = new AccountDto { Id = account.Id, Balance = account.Balance }
            });
    }

    private static IResult HandleTransfer(EventRequest request, TransactionService transactionService)
    {
        if (string.IsNullOrWhiteSpace(request.Origin) || string.IsNullOrWhiteSpace(request.Destination))
            return Results.BadRequest("Origin and Destination are required for transfer.");

        var (origin, destination) = transactionService.Transfer(request.Origin, request.Destination, request.Amount);

        return origin is null
            ? Results.NotFound(0)
            : Results.Created(string.Empty, new
            {
                origin = new AccountDto { Id = origin.Id, Balance = origin.Balance },
                destination = new AccountDto { Id = destination!.Id, Balance = destination.Balance }
            });
    }
}