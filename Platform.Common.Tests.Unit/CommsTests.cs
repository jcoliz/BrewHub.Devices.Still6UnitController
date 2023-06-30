using BrewHub.Devices.Platform.Common.Models;
using BrewHub.Devices.Platform.Common.Comms;

namespace Platform.Common.Tests.Unit;

/// <summary>
/// Feature: Inter-Component Communication 
/// </summary>
public class CommsTests
{
    IComponentCommunicator? comms = null;
    SimpleRootModel? root = null;

    [SetUp]
    public void Setup()
    {
        root = new SimpleRootModel();
        comms = new ComponentCommunicator(root);
    }

    /// <summary>
    /// Scenario: Request property from root
    /// </summary>
    [Test]
    public async Task RootProperty()
    {
        // Given: A communicator set up on a single root model
        // (done in Setup)

        // When: Asking for the metric value of one of its properties
        var result = await comms!.GetMetricValueAsync("serialNumber");

        // Then: The value of the property is returned
        Assert.That(result, Is.EqualTo("Unassigned"));
    }

    /// <summary>
    /// Scenario: Request telemetry from root
    /// </summary>
    [Test]
    public async Task RootTelemetry()
    {
        // Given: A communicator set up on a single root model
        // (done in Setup)

        // And: An initial telemetry value of {expected}
        double expected = 1234.56;
        root!.workingSet = expected;

        // When: Asking for the metric value of the expected telemetry
        var result = await comms!.GetMetricValueAsync("workingSet");

        // Then: The value of the property is returned as expected
        Assert.That(result, Is.EqualTo($"{expected}"));
    }

    /// <summary>
    /// Scenario: Request property from child component
    /// </summary>
    public Task ChildProperty()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Scenario: Request property from child component
    /// </summary>
    [Test]
    public async Task ChildTelemetry()
    {
        // Given: A communicator set up on a root model with child components
        // (done in Setup)

        // And: An initial telemetry value of {expected} on component {id}
        var id = "th2";
        var expected = "567.89";
        root!.Components[id].SetInitialState(new Dictionary<string, string>() { { "temp", expected } });

        // When: Asking for the metric value of the expected telemetry on the component {id}
        var result = await comms!.GetMetricValueAsync($"{id}.t");

        // Then: The value of the property is returned as expected
        Assert.That(result, Is.EqualTo($"{expected}"));    
    }
}
