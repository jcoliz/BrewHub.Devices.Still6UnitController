namespace BrewHub.Devices.Platform.Common.Clock;

/// <summary>
/// Determine the current system date/time
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}