// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved

using Newtonsoft.Json.Linq;

namespace BrewHub.Controller.Models;

public interface IComponentModel
{
    string? Name { get; }

    bool HasTelemetry { get; }

    object SetProperty(JProperty property);

    IDictionary<string,object> GetTelemetry();
}

public class SensorModel: IComponentModel
{
    public string? Name { get; set; }
    public int ModbusAddress { get; set; }
    public double TemperatureCorrection { get; set; }
    public double HumidityCorrection { get; set; }

    public bool HasTelemetry => ModbusAddress > 0;

    public object SetProperty(JProperty property)
    {
        if (property.Name == "modbusAddress")
        {
            int desired = (int)property.Value;
            ModbusAddress = desired;
            return ModbusAddress;
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
        else
        {
            throw new ApplicationException($"{Name} has no property '{property.Name}'");
        }
    }

    public IDictionary<string,object> GetTelemetry()
    {
        var readings = new Dictionary<string,object>();

        var fakereading = DateTime.Now.Minute + ModbusAddress * 10;
        readings["temperature"] = fakereading;
        readings["humidity"] = 100 - fakereading;

        return readings;
    }    
}

public class ValveModel: IComponentModel
{
    public string? Name { get; set; }
    public bool IsOpen { get; set; }

    public bool HasTelemetry { get; } = false;

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