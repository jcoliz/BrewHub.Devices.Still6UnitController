// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved

using AzDevice.Models;
using Newtonsoft.Json.Linq;

namespace BrewHub.Controller.Models;

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

    Task<object> IComponentModel.DoCommandAsync(string name)
    {
        throw new NotImplementedException();
    }
}
