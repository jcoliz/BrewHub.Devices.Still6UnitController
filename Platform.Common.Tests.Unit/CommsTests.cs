using BrewHub.Devices.Platform.Common.Models;
using BrewHub.Devices.Platform.Common.Comms;

namespace Platform.Common.Tests.Unit;

/// <summary>
/// Feature: Inter-Component Communication 
/// </summary>
public class CommsTests
{
    IComponentCommunicator? comms = null;
    IRootModel? root = null;

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
    public Task RootTelemetry()
    {
        return Task.CompletedTask;
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
    public Task ChildTelemetry()
    {
        return Task.CompletedTask;
    }
}
