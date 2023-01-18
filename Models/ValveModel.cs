// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved

using System.Text;
using System.Xml;
using AzDevice.Models;
using Newtonsoft.Json.Linq;

namespace BrewHub.Controller.Models;

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
        return $"{dtmi} {(Target ?? "Unnamed")}@{Relay}";
    }

    public IDictionary<string,object> GetTelemetry() => throw new NotImplementedException();

    Task<object> IComponentModel.DoCommandAsync(string name, byte[] data)
    {
        if (name != "OpenForDuration")
            throw new NotImplementedException($"{this} has no command {name}");

        var datastr = Encoding.UTF8.GetString(data);
        var param = System.Text.Json.JsonSerializer.Deserialize<string>(datastr);
        var span = XmlConvert.ToTimeSpan(param!);

        return Task.FromResult<object> (new());
    }
}