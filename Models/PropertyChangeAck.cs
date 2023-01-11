namespace BrewHub.Controller;

using System.Text.Json.Serialization;

public class PropertyChangeAck
{
    /// <summary>
    /// The unserialized property value.
    /// </summary>
    [JsonPropertyName("value")]
    public object? PropertyValue { get; set; } = null;

    /// <summary>
    /// The acknowledgement code, usually an HTTP Status Code e.g. 200, 400.
    /// </summary>
    [JsonPropertyName("ac")]
    public int AckCode { get; set; }

    /// <summary>
    /// The acknowledgement version, as supplied in the property update request.
    /// </summary>
    [JsonPropertyName("av")]
    public long AckVersion { get; set; }

    /// <summary>
    /// The acknowledgement description, an optional, human-readable message about the result of the property update.
    /// </summary>
    [JsonPropertyName("ad")]
    public string? AckDescription { get; set; }
}