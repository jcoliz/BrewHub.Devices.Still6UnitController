// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved

using System.Xml;
using Newtonsoft.Json.Linq;

namespace BrewHub.Controller.Models;

public interface IRootModel
{
    string dtmi { get; }

    IDictionary<string,IComponentModel> Children { get; }

    object SetProperty(string key, object value);
}

public class StillControllerModel: IRootModel
{
    public MachineryInfo? MachineryInfo;
    public TimeSpan TelemetryPeriod = TimeSpan.FromSeconds(30);

    private const string _dtmi = "dtmi:brewhub:controller:still;1";
    public string dtmi => _dtmi;

    public IDictionary<string,IComponentModel> Children { get; } = new Dictionary<string,IComponentModel>()
    {
        { "Sensor_1", new SensorModel() },
        { "Sensor_2", new SensorModel() },
        { "Sensor_3", new SensorModel() },
        { "Valve_1", new ValveModel() },
        { "Valve_2", new ValveModel() },
        { "Valve_3", new ValveModel() },
    };

    public object SetProperty(string key, object value)
    {
        object? result = null;
        if (key == "machineryInfo")
        {
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