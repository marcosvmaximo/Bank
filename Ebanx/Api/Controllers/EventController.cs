using Ebanx.Api.Filters;
using Ebanx.Application;
using Ebanx.Application.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Ebanx.Api.Controllers;

[ApiController]
public class EventController : ControllerBase
{
    private readonly TransactionService _transactionService;
    private readonly IdempotencyFilter _idempotencyFilter;
    
    public EventController(TransactionService transactionService, IdempotencyFilter idempotencyFilter)
    {
        _transactionService = transactionService;
        _idempotencyFilter = idempotencyFilter;
    }
    
    [HttpPost("reset")]
    public IActionResult Reset()
    {
        _transactionService.Reset();
        _idempotencyFilter.Reset();
        return Ok();
    }

    [HttpGet("balance")]
    public IActionResult GetBalance([FromQuery(Name = "account_id")] string accountId)
    {
        var balance = _transactionService.GetBalance(accountId);
        return balance is null
            ? NotFound(0)
            : Ok(balance);
    }

    [HttpPost("event")]
    [ServiceFilter(typeof(IdempotencyFilter))] // Para garantir a idempotencia, já que puramente, ele não é idempotente
    public IActionResult HandleEvent([FromBody] EventRequest request)
    {
        try
        {
            var response = _transactionService.ProcessEvent(request);
            return response is null
                ? NotFound(0)
                : Created(string.Empty, response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
