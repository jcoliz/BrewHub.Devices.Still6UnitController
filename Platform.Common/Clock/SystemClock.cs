namespace BrewHub.Devices.Platform.Common.Clock;

public class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}