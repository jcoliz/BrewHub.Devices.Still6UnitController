namespace Models.Synthetic.Tests.Unit;

public class TempHumidityTests
{
    private TempHumidityModel model = new();
    [SetUp]
    public void Setup()
    {
        model = new();
    }

    [Test]
    public void GetLogIdentity()
    {
        var actual = model.ToString();

        Assert.That(actual,Is.EqualTo("Simulated TH"));
    }
}