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

        Assert.That(actual,Is.EqualTo("Simulated TH"));
    }

    [TestCase("2023-07-02T12:00:00Z",35.0)]
    [TestCase("2023-07-02T00:00:00Z",20.0)]
    [TestCase("2023-01-01T12:00:00Z",15.0)]
    [TestCase("2023-01-01T00:00:00Z",0.0)]
    public void TemperatureOnDate(string time, double expected)
    {
        var dt = DateTimeOffset.Parse(time);
        clock.UtcNow = dt;
        var actual = component.GetTelemetry() as TempHumidityModel.SimulatedTelemetry;

        Assert.That(actual!.Temperature,Is.EqualTo(expected).Within(1.0));
    }
}