using BrewHub.Devices.Platform.Common.Models;
using BrewHub.Devices.Platform.Common.Comms;

namespace Platform.Common.Tests.Unit;

public class Tests
{
    IComponentCommunicator? comms = null;
    IRootModel? root = null;

    [SetUp]
    public void Setup()
    {
        root = new SimpleRootModel();
        comms = new ComponentCommunicator(root);
    }

    [Test]
    public async Task Test1()
    {
        var result = await comms!.GetMetricValueAsync("serialNumber");

        Assert.That(result, Is.EqualTo("Unassigned"));
    }
}