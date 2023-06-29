using BrewHub.Devices.Platform.Common;
using BrewHub.Devices.Platform.Common.Clock;
using BrewHub.Controllers.Models.Synthetic;

namespace Models.Synthetic.Tests.Unit;

public class RefluxThermostatTests
{
    private readonly TestClock clock = new() { Locked = true };
    private ThermostatModelBH model = new();

    private IComponentModel component => model as IComponentModel;

    [SetUp]
    public void Setup()
    {
        model = new(clock);
    }

    [Test]
    public void GetDTMI()
    {
        var actual = model.dtmi;

        Assert.That(actual,Is.EqualTo("dtmi:brewhub:controls:Thermostat;1"));
    }

    [Test]
    public void StartPoint()
    {
        // Given: Initial StartPoint value of {startpoint}
        var startpoint = 30.0;
        var state = new Dictionary<string,string>() { {"Temperature", $"{startpoint:F0}"} };
        component.SetInitialState(state);

        // When: Getting Telemetry immediately after system start
        var actual = component.GetTelemetry() as ThermostatModelBH.Telemetry;

        // Then: Temperature is {startpoint}
        Assert.That(actual!.Temperature,Is.EqualTo(startpoint));
    }

    public void HotAcceleration()
    {
        // Given: Initial values of StartPoint:{startpoint}, HotAccel:{hotaccel} (C/s^2)
        // When: {time} has passed since system start
        // And: Getting Telemetry
        // Then: Temperature is {startpoint} + {hotaccel}/2 * {time}^2
        // D = v*t + 1/2*a*t^2
    }

    public void RefluxValveCloses()
    {
        // Given: Initial values of StartPoint:{startpoint}, HotAccel:{hotaccel} (C/s^2), Tolerance:{tolerance}
        // When: Sufficient time has passed, such that Temperature exceeds Target property by {tolerance}
        // Then: Thermostat opens reflux valve
    }

    // Temperature starts at {startpoint} when system starts
    // Temperature increases/decreases by {velocity} degrees every {timeframe}
    // When reflux valve is closes, {velocity} increases by {hotaccel} degrees every {timeframe}
    // Climbs {range} degrees every {timeframe}
    // When temperature exceeds Target property by {tolerance}, opens the reflux valve
    // When reflux valve is open, {velocity} decreases {coldaccel} degrees every {timeframe}
    //When temperature falls below Target property by {tolerance}, closes the reflux valve
}