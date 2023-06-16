namespace BrewHub.Devices.Platform.Mqtt;
public record MessagePayload
{
    public long Timestamp { get; init; }
    public int Seq { get; init; }
    public string? Model { get; init; }
    public Dictionary<string, object>? Metrics { get; init; }
}
