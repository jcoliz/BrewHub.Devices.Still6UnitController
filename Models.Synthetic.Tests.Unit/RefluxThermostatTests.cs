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
}