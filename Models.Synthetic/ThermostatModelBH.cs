// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved
// Use of this source code is governed by the MIT license (see LICENSE file)

using BrewHub.Devices.Platform.Common;
using BrewHub.Devices.Platform.Common.Clock;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BrewHub.Controllers.Models.Synthetic;

/// <summary>
/// Thermostat: Reports current temperature and provides desired temperature control
/// </summary>
/// <remarks>
/// The design of this component is described in:
/// Task 1627: Fake reflux loop
/// Implements "dtmi:brewhub:controls:Thermostat;1";
/// </remarks>
public class ThermostatModelBH : IComponentModel
{    
    #region Constructor

    public ThermostatModelBH(IClock? clock = null)
    {
        _clock = clock ?? new SystemClock();
        lastread = _clock.UtcNow;
    }

    #endregion

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

    /// <summary>
    /// Specify a component whose temperature we target. Overrides targetTemp if set.
    /// </summary>
    [JsonPropertyName("isOpen")]
    public bool IsOpen { get; private set; }

    #endregion

    #region Telemetry

    public record Telemetry
    {
        [JsonPropertyName("t")]
        public double Temperature { get; init; }

        /// <summary>
        /// Device status. Zero is OK, >0 increasing severity to 999
        /// </summary>
        public int Status { get; set; }
    }

    #endregion

    #region Commands
    #endregion

    #region Fields

    private int skew = 0;
    private readonly IClock _clock;

    /// <summary>
    /// Current synthetic temperature
    /// </summary>
    private double temperature = 0.0;

    /// <summary>
    /// Current synthetic temperature velocity in C/s
    /// </summary>
    private double velocity = 0.0;

    /// <summary>
    /// Current synthetic temperature accelleration when hot in C/s^2
    /// </summary>
    private double hotaccel = 0.0;

    /// <summary>
    /// How much above or below the target temp can we get
    /// </summary>
    private double tolerance = 5.0;

    private DateTimeOffset lastread = DateTimeOffset.MinValue;

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
        // Fix the time
        var now = _clock.UtcNow;

        // Measure the time since last reading
        double elapsed = (now - lastread).TotalSeconds;

        // Determine the current temp
        temperature = temperature + elapsed * velocity + hotaccel / 2.0 * elapsed * elapsed;

        // Take the reading
        var reading = new Telemetry() { Temperature = temperature };

        // Update the velocity
        velocity += hotaccel * elapsed;

        // Update last read time
        lastread = now;

        // Open valve if too hot
        if (temperature >= TargetTemperature + tolerance)
        {
            IsOpen = true;
        }

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

        // Synthetic controls
        if (values.ContainsKey("Temperature"))
            temperature = Convert.ToDouble(values["Temperature"]);
        if (values.ContainsKey("HotAccel"))
            hotaccel = Convert.ToDouble(values["HotAccel"]);
        if (values.ContainsKey("Tolerance"))
            tolerance = Convert.ToDouble(values["Tolerance"]);

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
        throw new NotImplementedException($"Command {name} is not implemented on {dtmi}");
    }
 
    #endregion
}