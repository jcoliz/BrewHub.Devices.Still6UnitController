// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved
// Use of this source code is governed by the MIT license (see LICENSE file)

using BrewHub.Devices.Platform.Common;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BrewHub.Controllers;

/// <summary>
/// Thermostat: Reports current temperature and provides desired temperature control
/// </summary>
/// <remarks>
/// "dtmi:brewhub:controls:Thermostat;1";
/// </remarks>
public class ThermostatModelBH : IComponentModel
{    
    #region Properties

    [JsonPropertyName("__t")]
    public string ComponentID => "c";

    /// <summary>
    /// Specify the exact temperature to target
    /// </summary>
    [JsonPropertyName("targetTemp")]
    public double TargetTemperature { get; set; }

    /// <summary>
    /// Correction value to apply for temperature readings
    /// </summary>
    [JsonPropertyName("tcorr")]
    public double TemperatureCurrection { get; private set; }

    /// <summary>
    /// Specify a component whose temperature we target. Overrides targetTemp if set.
    /// </summary>
    [JsonPropertyName("targetComp")]
    public string? TargetComponent { get; private set; }

    /// <summary>
    /// Specify a component to adjust in trying to bring temperature to target.
    /// </summary>
    [JsonPropertyName("cComp")]
    public string? ControlComponent { get; private set; }

    #endregion

    #region Telemetry

    public class Telemetry
    {
        public Telemetry(double target)
        {
            var dt = DateTimeOffset.UtcNow;
            var phase = ((int)target) % 16;
            Temperature = target + 20.0 * Math.Sin((double)(dt.Second + phase * 3) / 30.0 * Math.PI);
        }

        [JsonPropertyName("t")]
        public double Temperature { get; private set; }

        /// <summary>
        /// Device status. Zero is OK, >0 increasing severity to 999
        /// </summary>
        public int Status { get; set; }
    }

    #endregion

    #region Commands

    protected Task<object> GetMinMaxReport(string jsonparams)
    {
        if (jsonparams.Length > 0)
        {
            var since = JsonSerializer.Deserialize<DateTimeOffset>(jsonparams);
            _minMaxReport.StartTime = since;

        }
        return Task.FromResult<object>(_minMaxReport);
    }

    #endregion

    #region Fields

    private readonly MinMaxReportModel _minMaxReport = new MinMaxReportModel();

    private int skew = 0;

    #endregion

    #region IComponentModel

    /// <summary>
    /// Identifier for the model implemented here
    /// </summary>
    [JsonIgnore]
    public string dtmi => "dtmi:brewhub:controls:Thermostat;1";

    /// <summary>
    /// Get an object containing all current telemetry
    /// </summary>
    /// <returns>All telemetry we wish to send at this time, or null for don't send any</returns>
    object? IComponentModel.GetTelemetry()
    {
        // Take the reading
        var reading = new Telemetry(TargetTemperature + (double)skew);

        return reading;
    }

    /// <summary>
    /// Set a particular property to the given value
    /// </summary>
    /// <param name="key">Which property</param>
    /// <param name="jsonvalue">Value to set (will be deserialized from JSON)</param>
    /// <returns>The unserialized new value of the property</returns>
    object IComponentModel.SetProperty(string key, string jsonvalue)
    {
        if (key == "tcorr")
            return TemperatureCurrection = Convert.ToDouble(jsonvalue);
        if (key == "targetTemp")
            return TargetTemperature = Convert.ToDouble(jsonvalue);
        if (key == "targetComp")
            return TargetComponent = JsonSerializer.Deserialize<string>(jsonvalue)!;
        if (key == "cComp")
            return ControlComponent =  JsonSerializer.Deserialize<string>(jsonvalue)!;

        throw new NotImplementedException($"Property {key} is not implemented on {dtmi}");
    }

    /// <summary>
    /// Get an object containing all properties known to this model
    /// </summary>
    /// <returns>All known properties, and their current state</returns>
    object IComponentModel.GetProperties()
    {
        return this as ThermostatModelBH;
    }

    /// <summary>
    /// Set the application intitial state from the supplied configuration values
    /// </summary>
    /// <param name="values">Dictionary of all known configuration values which could apply to this component</param>
    void IComponentModel.SetInitialState(IDictionary<string, string> values)
    {
        if (values.ContainsKey("targetTemp"))
            TargetTemperature = Convert.ToDouble(values["targetTemp"]);
        if (values.ContainsKey("targetComp"))
            TargetComponent = values["targetComp"];
        if (values.ContainsKey("cComp"))
            ControlComponent = values["cComp"];

        if (values.ContainsKey("Skew"))
        {
            skew = values["Skew"].ToString().First() - '0';
        }
    }

    /// <summary>
    /// Execute the given command
    /// </summary>
    /// <param name="name">Name of the command</param>
    /// <param name="jsonparams">Parameters for the command (will be deserialized from JSON)</param>
    /// <returns>Unserialized result of the action, or new() for empty result</returns>
    Task<object> IComponentModel.DoCommandAsync(string name, string jsonparams)
    {
        return name switch
        {
            "getMaxMinReport" => GetMinMaxReport(jsonparams),
            _ => throw new NotImplementedException($"Command {name} is not implemented on {dtmi}")
        };
    }
 
    #endregion
}