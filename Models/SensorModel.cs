// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved

using Newtonsoft.Json.Linq;

namespace BrewHub.Controller.Models;

public interface IComponentModel
{
    bool HasTelemetry { get; }

    object SetProperty(JProperty property);

    IDictionary<string,object> GetTelemetry();
}

public class SensorModel: IComponentModel
{
    public string? Target { get; set; }
    public int LogicalAddress { get; set; }
    public int PhysicalAddress { get; private set; }
    public double TemperatureCorrection { get; set; }
    public double HumidityCorrection { get; set; }
    public bool IsActive { get; set; }
    public bool IsConnected { get; private set; }

    public bool HasTelemetry => LogicalAddress > 0 && IsActive;

    public object SetProperty(JProperty property)
    {
        if (property.Name == "LogicalAddress")
        {
            int desired = (int)property.Value;
            LogicalAddress = desired;
            return LogicalAddress;
        }
        else if (property.Name == "temperatureCorrection")
        {
            double desired = (double)property.Value;
            TemperatureCorrection = desired;
            return TemperatureCorrection;
        }
        else if (property.Name == "humidityCorrection")
        {
            double desired = (double)property.Value;
            HumidityCorrection = desired;
            return HumidityCorrection;
        }
        else if (property.Name == "IsActive")
        {
            bool desired = (bool)property.Value;
            IsActive = desired;
            return IsActive;
        }
        else if (property.Name == "Target")
        {
            var desired = property.Value as Newtonsoft.Json.Linq.JValue;
            if (desired is null)
                throw new ApplicationException($"Failed to extract value from {property.Value}");

            var newval = (string?)desired;
            if (newval is null)
                throw new FormatException($"Failed to extract string from {property.Value}");

            Target = newval;
            return Target;
        }
        else
        {
            throw new ApplicationException($"{Target ?? "Component"} has no property '{property.Name}'");
        }
    }

    public IDictionary<string,object> GetTelemetry()
    {
        var readings = new Dictionary<string,object>();

        var fakereading = DateTime.Now.Minute + LogicalAddress * 10;
        readings["temperature"] = fakereading + TemperatureCorrection;
        readings["humidity"] = 100 - fakereading + HumidityCorrection;

        return readings;
    }

    public override string ToString()
    {
        return $"{(Target ?? "Component")}@{LogicalAddress}";
    }
}

public class ValveModel: IComponentModel
{
    public string? Name { get; set; }
    public bool IsOpen { get; set; }

    public int Relay { get; set; }

    public bool HasTelemetry { get; } = false;
    public bool IsActive { get; set; }

    public object SetProperty(JProperty property)
    {
        if (property.Name == "isOpen")
        {
            bool desired = (bool)property.Value;
            IsOpen = desired;
            return IsOpen;
        }
        else
        {
            throw new ApplicationException($"{Name} has no property '{property.Name}'");
        }
    }

    public IDictionary<string,object> GetTelemetry() => throw new NotImplementedException();

}