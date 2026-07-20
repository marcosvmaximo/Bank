using Ebanx.Api.Filters;
using Ebanx.Application;
using Ebanx.Domain;
using Ebanx.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddSingleton<IAccountRepository, AccountRepository>();
builder.Services.AddSingleton<IUnitOfWork, UnitOfWork>();
builder.Services.AddSingleton<IdempotencyFilter>();
builder.Services.AddScoped<TransactionService>();

var app = builder.Build();

app.MapControllers();

app.Run();

// Required for WebApplicationFactory<Program> in integration tests.
namespace Ebanx
{
    public partial class Program { }
}