namespace BrewHub.Devices.Platform.Common.Clock;

public class TestClock: IClock
{
    private TimeSpan Offset = TimeSpan.Zero;
    private DateTimeOffset Fixed = DateTimeOffset.UtcNow;

    public DateTimeOffset UtcNow
    {
        get
        {
            return Locked ? Fixed : DateTimeOffset.UtcNow + Offset;
        }
        set
        {
            Offset = value - DateTimeOffset.UtcNow;
            Fixed = value;
        }
    }

    /// <summary>
    /// Whether the time is fixed to a certain time no matter what (true)
    /// or contiues forward in step with current time (false)
    /// </summary>
    public bool Locked { get; set; } = false;
}