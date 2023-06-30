// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved
// Use of this source code is governed by the MIT license (see LICENSE file)

using BrewHub.Devices.Platform.Common.Clock;
using BrewHub.Devices.Platform.Common.Comms;
using BrewHub.Devices.Platform.Common.Models;
using BrewHub.Controllers.Models.Synthetic;
using Moq;

namespace Models.Synthetic.Tests.Unit;

public class RefluxThermostatTests
{
    private ThermostatModelBH model = new();
    private readonly TestClock clock = new() { Locked = true };

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

    /// <summary>
    /// Scenario: Temperature starts at {startpoint} when system starts
    /// </summary>
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

    /// <summary>
    /// Scenario: When reflux valve is closed, {velocity} increases by {hotaccel} degrees every {timeframe}
    /// </summary>
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

    /// <summary>
    /// Scenario: When temperature exceeds Target property by {tolerance}, opens the reflux valve
    /// </summary>
    [Test]
    public void ValveOpens()
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

    /// <summary>
    /// When reflux valve is open, {velocity} decreases {coldaccel} degrees every {timeframe}
    /// </summary>
    [Test]
    public void ColdAcceleration()
    {
        // Given: Initial values of ColdAccel
        var coldaccel = -0.5;
        var state = new Dictionary<string,string>() 
        { 
            { "ColdAccel", $"{coldaccel:F1}" },
        };
        component.SetInitialState(state);

        // And: Reflux Valve has gotten open
        ValveOpens();

        // And: Taken note of the Current temp and velocity
        var hightemp = model.temperature;
        var velocity = model.velocity;

        // When: More {time} has passed
        var time = TimeSpan.FromSeconds(20);
        clock.UtcNow += time;

        // And: Getting Telemetry
        var actual = component.GetTelemetry() as ThermostatModelBH.Telemetry;

        // Then: Temperature is {hightemp} + {velocity} * {time} + {coldaccel}/2 * {time}^2
        var expected = hightemp + velocity * time.TotalSeconds + coldaccel/2.0 * Math.Pow( time.TotalSeconds, 2.0); 
        Assert.That(actual!.Temperature,Is.EqualTo(expected));
    }
    
    /// <summary>
    /// Scenario: When temperature falls below Target property by {tolerance}, closes the reflux valve
    /// </summary>
    [Test]
    public void ValveCloses()
    {
        // Given: Initial values of ColdAccel
        var coldaccel = -0.5;
        var state = new Dictionary<string,string>() 
        { 
            { "ColdAccel", $"{coldaccel:F1}" },
        };
        component.SetInitialState(state);

        // And: Reflux Valve has gotten open
        ValveOpens();

        // And: Taken note of the Current temp and velocity
        var hightemp = model.temperature;
        var velocity = model.velocity;

        // When: Sufficient time has passed, such that Temperature falls below Target property by {tolerance}
        var time = TimeSpan.FromSeconds(22);
        clock.UtcNow += time;

        // And: Getting telemetry (which is needed to give the model a slide of CPU to work in)
        component.GetTelemetry();

        // And: Temperature has gone below the threshold
        Assert.That(model.temperature,Is.LessThan(75.0));

        // Then: Thermostat opens reflux valve
        Assert.That(model.IsOpen,Is.False);
    }

    /// <summary>
    /// Scenario: User Can calibrate the temperature readings
    /// </summary>
    public void TemperatureCorrection()
    {
    }

    /// <summary>
    /// Scenario: User can designate another component as the target temperature
    /// </summary>
    [Test]
    public void TargetFromComponent()
    {
        // Actually, user is going to designate a whole component PATH for the target,
        // e.g. "amb.t" which means the `t` metric value from the `amb` component

        // Given: Another component which will return a much lower target temperature on {targetprop}
        var mock = new Mock<IComponentCommunicator>();
        var targetprop = "amb.t";
        var targetpropvalue = 40.0;
        mock
            .Setup(x => x.GetMetricValueAsync(targetprop))
            .Returns(Task.FromResult(targetpropvalue.ToString()));
        model = new(clock, mock.Object);

        // And: Initial values of StartPoint:{startpoint}, HotAccel:{hotaccel} (C/s^2), Tolerance:{tolerance}, Target: {target}
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

        // When: Setting a target component property, pointing at {targetprop}
        state = new Dictionary<string,string>() 
        { 
            { "targetComp", $"{targetprop}" }
        };
        component.SetInitialState(state);

        // When: Sufficient time has passed, such that Temperature exceeds {targetprop} by {tolerance}
        var time = TimeSpan.FromSeconds(13);
        clock.UtcNow += time;

        // And: Getting telemetry 
        // (which is needed to give the model a slice of CPU to work in)
        // (Also we want to print out details in case of failure)
        var t = component.GetTelemetry() as ThermostatModelBH.Telemetry;

        // Then: Thermostat opens reflux valve
        Assert.That(model.IsOpen,Is.True,$"Current temp: {t.Temperature}");
    }

    /// <summary>
    /// Scenario: Valve component comes on when our IsOpen is  on
    /// </summary>
    public void ValveComponentOn()
    {
        // TODO: This test actually belongs in the binary valve component
    }
}