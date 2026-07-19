using Ebanx.DTOs;
using Ebanx.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ebanx.Controllers;

[ApiController]
public class EventController(TransactionService transactionService) : ControllerBase
{
    [HttpPost("reset")]
    public IActionResult Reset()
    {
        transactionService.Reset();
        return Ok();
    }

    [HttpGet("balance")]
    public IActionResult GetBalance([FromQuery(Name = "account_id")] string accountId)
    {
        var account = transactionService.GetAccount(accountId);
        return account is null 
            ? NotFound(0) 
            : Ok(account.Balance);
    }

    [HttpPost("event")]
    public IActionResult HandleEvent([FromBody] EventRequest request)
    {
        return request.Type switch
        {
            "deposit" => HandleDeposit(request),
            "withdraw" => HandleWithdraw(request),
            "transfer" => HandleTransfer(request),
            _ => BadRequest("Unknown event type.")
        };
    }

    private IActionResult HandleDeposit(EventRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Destination))
            return BadRequest("Destination is required for deposit.");

        var account = transactionService.Deposit(request.Destination, request.Amount);
        
        return Created(string.Empty, new
        {
            destination = new AccountDto { Id = account.Id, Balance = account.Balance }
        });
    }

    private IActionResult HandleWithdraw(EventRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Origin))
            return BadRequest("Origin is required for withdraw.");

        var account = transactionService.Withdraw(request.Origin, request.Amount);
        
        return account is null
            ? NotFound(0)
            : Created(string.Empty, new
            {
                origin = new AccountDto { Id = account.Id, Balance = account.Balance }
            });
    }

    private IActionResult HandleTransfer(EventRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Origin) || string.IsNullOrWhiteSpace(request.Destination))
            return BadRequest("Origin and Destination are required for transfer.");

        var (origin, destination) = transactionService.Transfer(request.Origin, request.Destination, request.Amount);

        return origin is null
            ? NotFound(0)
            : Created(string.Empty, new
            {
                origin = new AccountDto { Id = origin.Id, Balance = origin.Balance },
                destination = new AccountDto { Id = destination!.Id, Balance = destination.Balance }
            });
    }
}