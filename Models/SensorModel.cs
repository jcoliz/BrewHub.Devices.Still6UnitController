// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved

using Newtonsoft.Json.Linq;

namespace BrewHub.Controller.Models;

public interface IComponent
{
    string? Name { get; }

    object SetProperty(JProperty property);
}

public class SensorModel: IComponent
{
    public string? Name { get; set; }
    public int ModbusAddress { get; set; }
    public double TemperatureCorrection { get; set; }
    public double HumidityCorrection { get; set; }

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
}