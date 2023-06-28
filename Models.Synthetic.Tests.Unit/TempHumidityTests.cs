using BrewHub.Devices.Platform.Common;
using BrewHub.Devices.Platform.Common.Clock;

namespace Models.Synthetic.Tests.Unit;

public class TempHumidityTests
{
    private readonly TestClock clock = new() { Locked = true };
    private TempHumidityModel model = new();

    private IComponentModel component => model as IComponentModel;

    [SetUp]
    public void Setup()
    {
        model = new(clock);
    }

    [Test]
    public void GetLogIdentity()
    {
        var actual = model.ToString();

        Assert.That(actual,Is.EqualTo("Simulated T&H"));
    }

    [TestCase("2023-07-02T12:00:00Z",35.0)]
    [TestCase("2023-07-02T00:00:00Z",20.0)]
    [TestCase("2023-01-01T12:00:00Z",15.0)]
    [TestCase("2023-01-01T12:50:00Z",14.8222)]
    [TestCase("2023-01-01T13:00:00Z",14.7444)]
    [TestCase("2023-01-01T13:00:15Z",15.2444)]
    [TestCase("2023-01-01T13:00:30Z",15.7444)]
    [TestCase("2023-01-01T00:00:00Z",0.0)]
    public void TemperatureOnDate(string time, double expected)
    {
        // Given: The time is now {time}
        var dt = DateTimeOffset.Parse(time);
        clock.UtcNow = dt;

        // When: Getting telemetry
        var actual = component.GetTelemetry() as TempHumidityModel.SimulatedTelemetry;

        // Then: The temperature is {expected}
        Assert.That(actual!.Temperature,Is.EqualTo(expected).Within(0.001));
    }

    [TestCase("2023-01-01T00:00:00Z","0",0.0)]
    [TestCase("2023-01-01T00:00:00Z","-1",-1.0)]
    [TestCase("2023-01-01T00:00:00Z","-1.5",-1.5)]
    [TestCase("2023-01-01T00:00:00Z","25.71",25.71)]
    public void TemperatureCorrection(string time, string correction, double expected)
    {
        // Given: The time is now {time}
        var dt = DateTimeOffset.Parse(time);
        clock.UtcNow = dt;

        // When: Setting temperature correction to {correction}
        var state = new Dictionary<string,string>() { { "tcorr",correction } }; 
        component.SetInitialState(state);

        // And: Getting telemetry
        var actual = component.GetTelemetry() as TempHumidityModel.SimulatedTelemetry;

        // Then: The temperature is {expected}
        Assert.That(actual!.Temperature,Is.EqualTo(expected).Within(0.001));
    }
}