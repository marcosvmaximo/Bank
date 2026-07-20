using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Ebanx.Api.Filters;

/// <summary>
/// Action filter que garante idempotência via header <c>Idempotency-Key</c>.
/// Registrado como singleton para que o cache persista entre requisições.
/// </summary>
public class IdempotencyFilter : IAsyncActionFilter
{
    private readonly ConcurrentDictionary<string, (int StatusCode, object? Body)> _cache = new();

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var key = context.HttpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();

        // Chave já processada → retorna resultado cacheado sem executar o action
        if (key is not null && _cache.TryGetValue(key, out var cached))
        {
            context.Result = new ObjectResult(cached.Body) { StatusCode = cached.StatusCode };
            return;
        }

        var executed = await next();

        // Armazena o resultado tipado (IActionResult) — sem precisar tocar no stream HTTP
        if (key is not null && executed.Result is ObjectResult result)
            _cache[key] = (result.StatusCode ?? StatusCodes.Status200OK, result.Value);
    }

    /// <summary>Limpa o cache — chamado pelo Reset da aplicação.</summary>
    public void Reset() => _cache.Clear();
}
