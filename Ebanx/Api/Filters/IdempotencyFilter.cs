using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Ebanx.Api.Filters;

public class IdempotencyFilter : IAsyncActionFilter
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new();
    private readonly ConcurrentDictionary<string, (int StatusCode, object? Body)> _cache = new();

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var key = context.HttpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();

        if (key is null)
        {
            await next();
            return;
        }

        var semaphore = _keyLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync();
        try
        {
            if (_cache.TryGetValue(key, out var cached))
            {
                context.Result = new ObjectResult(cached.Body) { StatusCode = cached.StatusCode };
                return;
            }

            var executed = await next();

            if (executed.Result is ObjectResult result)
                _cache[key] = (result.StatusCode ?? StatusCodes.Status200OK, result.Value);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public void Reset()
    {
        _cache.Clear();
        _keyLocks.Clear();
    }
}
