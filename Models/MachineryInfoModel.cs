// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved

namespace BrewHub.Controller.Models;

// Matches dtmi:brewhub:controller:still-1 "MachineryInfo" property
public class MachineryInfo
{
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? Variant { get; set; }
    public string? SerialNumber { get; set; }
    public MachineryConfiguration Configuration { get; set; } = new();

    public override string ToString()
    {
        return $"[Machinery Info] {Manufacturer??"null"} {Model??"null"}";
    }
}

public class MachineryConfiguration
{
    public Dictionary<string,int> Sensors { get; set; } = new();
    public Dictionary<string,int> Valves { get; set; } = new();
}