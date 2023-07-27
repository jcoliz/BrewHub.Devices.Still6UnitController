// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved
// Use of this source code is governed by the MIT license (see LICENSE file)

using BrewHub.Devices.Platform.Common.Clock;
using BrewHub.Devices.Platform.Common.Comms;
using BrewHub.Devices.Platform.Common.Models;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

[assembly: InternalsVisibleTo("Models.Synthetic.Tests.Unit")]

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

    public ThermostatModelBH(IClock? clock = null, IComponentCommunicator? comms = null)
    {
        _clock = clock ?? new SystemClock();
        lastread = _clock.UtcNow;

        _comms = comms;
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
    public double TemperatureCorrection { get; private set; }

    /// <summary>
    /// Specify a metric on another component whose temperature we target. Overrides targetTemp if set.
    /// </summary>
    [JsonPropertyName("targetMetric")]
    public string? TargetMetric { get; private set; }

    /// <summary>
    /// Temperature reading is over target temp.
    /// </summary>
    [JsonPropertyName("overTemp")]
    public bool IsOverTemp { get; private set; }

    #endregion

    #region Telemetry

    public record Telemetry
    {
        /// <summary>
        /// Temperature in degrees Celsius
        /// </summary>
        [JsonPropertyName("t")]
        public double Temperature { get; init; }

        /// <summary>
        /// Component status. Zero is OK, >0 increasing severity to 999
        /// </summary>
        public int Status { get; set; }
    }

    #endregion

    #region Commands
    #endregion

    #region Fields

    private int skew = 0;
    private readonly IClock _clock;
    private readonly IComponentCommunicator? _comms;

    /// <summary>
    /// Current synthetic temperature
    /// </summary>
    internal double temperature = 0.0;

    /// <summary>
    /// Current synthetic temperature velocity in C/s
    /// </summary>
    internal double velocity = 0.0;

    /// <summary>
    /// Current synthetic temperature accelleration when hot in C/s^2
    /// </summary>
    private double hotaccel = 0.0;

    /// <summary>
    /// Current synthetic temperature accelleration when cold in C/s^2
    /// </summary>
    private double coldaccel = 0.0;

    /// <summary>
    /// How much above or below the target temp can we get
    /// </summary>
    private double tolerance = 5.0;

    private DateTimeOffset lastread = DateTimeOffset.MinValue;

    #endregion

    #region Internals

    /// <summary>
    /// Considering the rules for determining target temperature, what temp
    /// should we target right NOW?
    /// </summary>
    /// <remarks>
    /// Capable of throwing different exceptions. Be prepared!
    /// </remarks>
    /// <returns></returns>
    private double CalculateTargetTemperature()
    {
        if (TargetMetric is not null && _comms is not null)
        {
            var strresult = _comms.GetMetricValueAsync(TargetMetric).Result;
            return Convert.ToDouble(strresult);
        }
        else
        {
            return TargetTemperature;
        }
    }

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

        // Bug 1635: Synthetic model corrupted on restart. 
        // Task 1658: Synthetic telemetry should reset after long delays
        const double maxelapsed = 9.0;
        if (elapsed > maxelapsed)
        {
            lastread = now;
            velocity = 0;
            return null;

            // If we had a logger here, we would log this.
        }

        // Determine the current acceleration
        var accel = IsOverTemp ? coldaccel : hotaccel;

        // Determine the current temp
        temperature = temperature + elapsed * velocity + accel / 2.0 * elapsed * elapsed;

        // Take the reading
        var reading = new Telemetry() { Temperature = temperature };

        // Update the velocity
        velocity += accel * elapsed;

        // Terminal velocity
        if (accel < 0)
        {
            velocity = Math.Max(velocity, -2.0 * hotaccel);
        }
        else
        {
            velocity = Math.Min(velocity, 2.0 * hotaccel);
        }

        // Update last read time
        lastread = now;

        // Open valve if too hot
        var target = CalculateTargetTemperature();
        if (temperature >= target + tolerance)
        {
            IsOverTemp = true;
        }
        // Close valve if too cold
        if (temperature <= target - tolerance)
        {
            IsOverTemp = false;
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
            return TemperatureCorrection = Convert.ToDouble(jsonvalue);
        if (key == "targetTemp")
            return TargetTemperature = Convert.ToDouble(jsonvalue);
        if (key == "targetMetric")
            return TargetMetric = JsonSerializer.Deserialize<string>(jsonvalue)!;

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
        if (values.ContainsKey("targetMetric"))
            TargetMetric = values["targetMetric"];

        // Synthetic controls
        if (values.ContainsKey("Temperature"))
            temperature = Convert.ToDouble(values["Temperature"]);
        if (values.ContainsKey("HotAccel"))
            hotaccel = Convert.ToDouble(values["HotAccel"]);
        if (values.ContainsKey("ColdAccel"))
            coldaccel = Convert.ToDouble(values["ColdAccel"]);
        if (values.ContainsKey("Tolerance"))
            tolerance = Convert.ToDouble(values["Tolerance"]);
        if (values.ContainsKey("Skew"))
            skew = values["Skew"].ToString().First() - '0';
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