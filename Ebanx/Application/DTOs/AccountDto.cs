using System.Text.Json.Serialization;

namespace Ebanx.Application.DTOs;

public class AccountDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("balance")]
    public decimal Balance { get; set; }
}
