// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved

using Newtonsoft.Json.Linq;

namespace BrewHub.Controller.Models;

public interface IComponentModel
{
    string dtmi { get; }

    bool HasTelemetry { get; }

    object SetProperty(string key, object value);

    IDictionary<string,object> GetTelemetry();
}

public class SensorModel: IComponentModel
{
    public string dtmi => "dtmi:brewhub:logical:xy_02;1";
    public string? Target { get; set; }
    public int LogicalAddress { get; set; }
    public int PhysicalAddress { get; private set; }
    public double TemperatureCorrection { get; set; }
    public double HumidityCorrection { get; set; }
    public bool IsActive { get; set; }
    public bool IsConnected { get; private set; }

    public bool HasTelemetry => LogicalAddress > 0 && IsActive;

    public object SetProperty(string key, object value)
    {
        if (key == "LogicalAddress")
        {
            int desired = (int)(JValue)value;
            LogicalAddress = desired;
            return LogicalAddress;
        }
        else if (key == "temperatureCorrection")
        {
            double desired = (double)(JValue)value;
            TemperatureCorrection = desired;
            return TemperatureCorrection;
        }
        else if (key == "humidityCorrection")
        {
            double desired = (double)(JValue)value;
            HumidityCorrection = desired;
            return HumidityCorrection;
        }
        else if (key == "IsActive")
        {
            bool desired = (bool)(JValue)value;
            IsActive = desired;
            return IsActive;
        }
        else if (key == "Target")
        {
            var newval = (string?)(JValue)value;
            if (newval is null)
                throw new FormatException($"Failed to extract string from {value}");

            Target = newval;
            return Target;
        }
        else
        {
            throw new ApplicationException($"{this} has no property '{value}'");
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
    public string dtmi => "dtmi:brewhub:logical:valve;1";
    public string? Target { get; set; }
    public bool IsActive { get; set; }
    public bool IsOpen { get; set; }

    public int Relay { get; set; }

    public bool HasTelemetry { get; } = false;

    public object SetProperty(string key, object value)
    {
        if (key == "Relay")
        {
            int desired = (int)(JValue)value;
            Relay = desired;
            return Relay;
        }
        else if (key == "isOpen")
        {
            bool desired = (bool)(JValue)value;
            IsOpen = desired;
            return IsOpen;
        }
        else if (key == "IsActive")
        {
            bool desired = (bool)(JValue)value;
            IsActive = desired;
            return IsActive;
        }
        else if (key == "Target")
        {
            var newval = (string?)(JValue)value;
            if (newval is null)
                throw new FormatException($"Failed to extract string from {value}");

            Target = newval;
            return Target;
        }
        else
        {
            throw new ApplicationException($"{this} has no property '{key}'");
        }
    }

    public override string ToString()
    {
        return $"{(Target ?? "Component")}@{Relay}";
    }

    public IDictionary<string,object> GetTelemetry() => throw new NotImplementedException();

}