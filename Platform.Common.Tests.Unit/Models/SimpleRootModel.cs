using System.Text.Json.Serialization;
using System.Xml;
using BrewHub.Devices.Platform.Common.Models;

public class SimpleRootModel : IRootModel
{
    #region Properties

    [JsonPropertyName("serialNumber")]
    public string? SerialNumber { get; private set; } = "Unassigned";

    // Note that telemetry period is not strictly part of the DTMI. Still,
    // it's nice to be able to set it in config, and send down changes to it

    [JsonPropertyName("telemetryPeriod")]
    public string TelemetryPeriod
    {
        get
        {
            return XmlConvert.ToString(_TelemetryPeriod);
        }
        private set
        {
            _TelemetryPeriod = XmlConvert.ToTimeSpan(value);
        }
    }
    private TimeSpan _TelemetryPeriod = TimeSpan.Zero;

    #endregion

    #region Telemetry

    public class Telemetry
    {
        [JsonPropertyName("workingSet")]
        public double WorkingSetKiB { get; init; }
    }

    #endregion    
    TimeSpan IRootModel.TelemetryPeriod => _TelemetryPeriod;

    #region Fields
    internal double workingSet { get; set; }
    #endregion

    [JsonIgnore]
    public IDictionary<string, IComponentModel> Components => throw new NotImplementedException();

    [JsonIgnore]
    public string dtmi => throw new NotImplementedException();

    public Task<object> DoCommandAsync(string name, string jsonparams)
    {
        throw new NotImplementedException();
    }

    public object GetProperties()
    {
        return this;
    }

    public object? GetTelemetry()
    {
        // Take the reading, return it
        return new Telemetry() { WorkingSetKiB = workingSet };
    }

    public void SetInitialState(IDictionary<string, string> values)
    {
        throw new NotImplementedException();
    }

    public object SetProperty(string key, string jsonvalue)
    {
        throw new NotImplementedException();
    }
}