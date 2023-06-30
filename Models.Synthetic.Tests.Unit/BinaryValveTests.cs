// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved
// Use of this source code is governed by the MIT license (see LICENSE file)

using BrewHub.Devices.Platform.Common.Comms;
using BrewHub.Devices.Platform.Common.Models;
using BrewHub.Controllers.Models.Synthetic;
using Moq;

namespace Models.Synthetic.Tests.Unit;

public class BinaryValveTests
{
    private BinaryValveModel model = new();

    private IComponentModel component => model as IComponentModel;

    [SetUp]
    public void Setup()
    {
        model = new();
    }

    [Test]
    public void GetDTMI()
    {
        var actual = model.dtmi;

        Assert.That(actual, Is.EqualTo("dtmi:brewhub:controls:BinaryValve;1"));
    }

    /// <summary>
    /// Scenario: Source metric overrides initial state 
    /// </summary>
    /// <param name="expected"></param>
    [TestCase(true)]
    [TestCase(false)]
    public void OpensOnSource(bool expected)
    {
        // Given: Another component which will return {value} as the valve state
        var mock = new Mock<IComponentCommunicator>();
        var targetprop = "rt.open";
        mock
            .Setup(x => x.GetMetricValueAsync(targetprop))
            .Returns(Task.FromResult(expected.ToString()));
        model = new(mock.Object);

        // And: Explicitly setting the component to the OPPOSITE isOpen value initially
        var state = new Dictionary<string,string>() 
        { 
            { "open", $"{!expected}" }
        };
        component.SetInitialState(state);

        // When: Setting the source Metric to {targetprop} 
        state = new Dictionary<string,string>() 
        { 
            { "sourceMetric", $"{targetprop}" }
        };
        component.SetInitialState(state);

        // And: Getting telemetry
        component.GetTelemetry();

        // Then: IsOpen state on valve is {expected}
        var actual = model.IsOpen;
        Assert.That(actual, Is.EqualTo(expected));
    }

    /// <summary>
    /// Scenario: Source metric overrides explicitly set property
    /// </summary>
    /// <param name="expected"></param>
    [TestCase(true)]
    [TestCase(false)]
    public void SourceMetricOverridesPropery(bool expected)
    {
        // Given: Another component which will return {expected} as the valve state
        var mock = new Mock<IComponentCommunicator>();
        var targetprop = "rt.open";
        mock
            .Setup(x => x.GetMetricValueAsync(targetprop))
            .Returns(Task.FromResult(expected.ToString()));
        model = new(mock.Object);

        // And: Source Metric is set to {targetprop} 
        var state = new Dictionary<string,string>() 
        { 
            { "sourceMetric", $"{targetprop}" }
        };
        component.SetInitialState(state);

        // When: Setting IsOpen Property to OPPOSITE of {expected} value
        component.SetProperty("open", (!expected).ToString());

        // And: Getting telemetry
        component.GetTelemetry();

        // Then: IsOpen state on valve is {expected}
        var actual = model.IsOpen;
        Assert.That(actual, Is.EqualTo(expected));
    }

    /// <summary>
    /// Scenario: Set open initial state
    /// </summary>
    /// <param name="expected"></param>
    [TestCase(true)]
    [TestCase(false)]
    public void InitialStateOpen(bool expected)
    {
        // When: Setting the initial state to {expected} open value
        var state = new Dictionary<string,string>() 
        { 
            { "open", $"{expected}" }
        };
        component.SetInitialState(state);

        // And: Getting telemetry
        component.GetTelemetry();

        // Then: IsOpen state on valve is {expected}
        var actual = model.IsOpen;
        Assert.That(actual, Is.EqualTo(expected));
    }

    /// <summary>
    /// Scenario: Set open property
    /// </summary>
    /// <param name="expected"></param>
    [TestCase(true)]
    [TestCase(false)]
    public void PropertyOpen(bool expected)
    {
        // Given: Initial state to opposite of {expected} open value
        var state = new Dictionary<string,string>() 
        { 
            { "open", $"{!expected}" }
        };
        component.SetInitialState(state);

        // When: Setting open property to {expected}
        component.SetProperty("open", expected.ToString());

        // And: Getting telemetry
        component.GetTelemetry();

        // Then: IsOpen state on valve is {expected}
        var actual = model.IsOpen;
        Assert.That(actual, Is.EqualTo(expected));
    }
}
