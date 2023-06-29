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

    [Test]
    public void HotAcceleration()
    {
        // Given: Initial values of StartPoint:{startpoint}, HotAccel:{hotaccel} (C/s^2)
        var startpoint = 30.0;
        var hotaccel = 0.2;
        var state = new Dictionary<string,string>() 
        { 
            { "Temperature", $"{startpoint:F0}" },
            { "HotAccel", $"{hotaccel:F1}" }
        };
        component.SetInitialState(state);

        // When: {time} has passed since system start
        var time = TimeSpan.FromSeconds(10);
        clock.UtcNow += time;

        // And: Getting Telemetry
        var actual = component.GetTelemetry() as ThermostatModelBH.Telemetry;

        // Then: Temperature is {startpoint} + {hotaccel}/2 * {time}^2
        var expected = startpoint + hotaccel/2.0 * Math.Pow( time.TotalSeconds, 2.0); 
        Assert.That(actual!.Temperature,Is.EqualTo(expected));
    }

    [Test]
    public void RefluxValveOpens()
    {
        // Given: Initial values of StartPoint:{startpoint}, HotAccel:{hotaccel} (C/s^2), Tolerance:{tolerance}, Target: {target}
        var startpoint = 30.0;
        var hotaccel = 0.2;
        var tolerance = 5.0;
        var target = 80.0;
        var state = new Dictionary<string,string>() 
        { 
            { "Temperature", $"{startpoint:F0}" },
            { "HotAccel", $"{hotaccel:F1}" },
            { "Tolerance", $"{tolerance:F1}" },
            { "targetTemp", $"{target:F1}" }
        };
        component.SetInitialState(state);

        // When: Sufficient time has passed, such that Temperature exceeds Target property by {tolerance}
        var time = TimeSpan.FromSeconds(24);
        clock.UtcNow += time;

        // And: Getting telemetry (which is needed to give the model a slide of CPU to work in)
        component.GetTelemetry();

        // Then: Thermostat opens reflux valve
        Assert.That(model.IsOpen,Is.True);
    }

    // Temperature starts at {startpoint} when system starts
    // Temperature increases/decreases by {velocity} degrees every {timeframe}
    // When reflux valve is closes, {velocity} increases by {hotaccel} degrees every {timeframe}
    // Climbs {range} degrees every {timeframe}
    // When temperature exceeds Target property by {tolerance}, opens the reflux valve
    // When reflux valve is open, {velocity} decreases {coldaccel} degrees every {timeframe}
    //When temperature falls below Target property by {tolerance}, closes the reflux valve
}