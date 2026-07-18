using System.Text.Json.Serialization;

namespace Ebanx.DTOs;

public class EventRequest
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("destination")]
    public string? Destination { get; set; }

    [JsonPropertyName("origin")]
    public string? Origin { get; set; }
}
