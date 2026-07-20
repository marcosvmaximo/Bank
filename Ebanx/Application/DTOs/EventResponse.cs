using System.Text.Json.Serialization;

namespace Ebanx.Application.DTOs;

public class EventResponse
{
    [JsonPropertyName("origin")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AccountDto? Origin { get; set; }

    [JsonPropertyName("destination")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AccountDto? Destination { get; set; }
}
