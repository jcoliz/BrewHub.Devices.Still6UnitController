// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved

using System.Text.Json;
using System.Xml;
using Newtonsoft.Json.Linq;

namespace BrewHub.Controller.Models;

public interface IRootModel
{
    string dtmi { get; }

    public TimeSpan TelemetryPeriod { get; }

    IDictionary<string,IComponentModel> Components { get; }

    object SetProperty(string key, object value);

    Task<string> LoadConfigAsync();
}

public class StillControllerModel: IRootModel
{
    protected MachineryInfo? MachineryInfo;
    public TimeSpan TelemetryPeriod { get; protected set; } = TimeSpan.FromSeconds(30);

    private const string _dtmi = "dtmi:brewhub:controller:still;1";
    public string dtmi => _dtmi;

    public IDictionary<string,IComponentModel> Components { get; } = new Dictionary<string,IComponentModel>()
    {
        { "Sensor_1", new SensorModel() },
        { "Sensor_2", new SensorModel() },
        { "Sensor_3", new SensorModel() },
        { "Valve_1", new ValveModel() },
        { "Valve_2", new ValveModel() },
        { "Valve_3", new ValveModel() },
    };

    public async Task<string> LoadConfigAsync()
    {
        // Machinery info can OPTIONALLY be supplied via local machine config.
        // Alternately, it can be sent down from the cloud as a desired property

        var status = "No config found";
        if (File.Exists("machineryinfo.json"))
        {
            using var stream = File.OpenRead("machineryinfo.json");
            var options = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            MachineryInfo = await JsonSerializer.DeserializeAsync<MachineryInfo>(stream,options);

            if (MachineryInfo is null)
                throw new ApplicationException("Unable to deserialize machinery info file");

            status = MachineryInfo.ToString();
        }

        return status;
    }

    public object SetProperty(string key, object value)
    {
        object? result = null;
        if (key == "machineryInfo")
        {
            // Sending NULL is a valid action here. It will show up as a JValue of Type Null
            if (value is JValue jv && jv.Type == JTokenType.Null)
            {
                MachineryInfo = new MachineryInfo();
                result = MachineryInfo;

                return result;
            }

            var desired = value as Newtonsoft.Json.Linq.JObject;
            if (desired is null)
                throw new ApplicationException($"Failed to extract value from {value}");
    
            var newval = desired.ToObject<MachineryInfo>();
            if (newval is null)
                throw new FormatException($"Failed to extract MachineryInfo from {value}");

            MachineryInfo = newval;
            result = MachineryInfo;
        }
        else if (key == "TelemetryPeriod")
        {
            var desired = value as Newtonsoft.Json.Linq.JValue;
            if (desired is null)
                throw new ApplicationException($"Failed to extract value from {value}");

            var newval = (string?)desired;
            if (newval is null)
                throw new FormatException($"Failed to extract string from {value}");

            TelemetryPeriod = XmlConvert.ToTimeSpan(newval);
            result = newval;
        }
        else
            throw new ApplicationException($"Does not contain property '{key}'");

        return result!;
    }
 
}