// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved
// Use of this source code is governed by the MIT license (see LICENSE file)

using BrewHub.Devices.Platform.Common;
using BrewHub.Devices.Platform.Common.Clock;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Basic Temperature & Humidity Sensor
/// </summary>
public class TempHumidityModel :  IComponentModel
{
    #region Constructor

    public TempHumidityModel(IClock? clock = null)
    {
        _clock = clock ?? new SystemClock();
    }

    #endregion

    #region Properties

    [JsonPropertyName("__t")]
    public string ComponentID => "c";

    [JsonPropertyName("tcorr")]
    public double TemperatureCorrection { get; set; }

    [JsonPropertyName("hcorr")]
    public double HumidityCorrection { get; set; }

    #endregion

    #region Telemetry

    /// <summary>
    /// Generates simulated telemetry in case we don't have an actual sensor
    /// attached.
    /// </summary>
    /// <remarks>
    /// Whether or not we are running with a real sensor is a runtime decision
    /// based on "Initial State" configuration.
    /// </remarks>
    public class SimulatedTelemetry
    {
        public SimulatedTelemetry(IClock clock)
        {
            var dt = clock.UtcNow;

            // Temperature varies by:
            //      - 20 degrees over the course of a year
            //      - 15 degrees over the course of a day
            //      - +/- 1 degrees randomly at any moment

            var dailytemp = 10.0 - 10.0 * Math.Cos( dt.DayOfYear/366.0 * 2 * Math.PI );
            var hourlyangle = dt.Hour / 24.0 * 2.0 * Math.PI;
            var hourlytemp = 7.5 - 7.5 * Math.Cos( hourlyangle );

            Temperature = dailytemp + hourlytemp;

            Humidity = (dt.Hour * 100.0 + dt.Minute + dt.Second / 100.0) / 2400.0;
        }

        [JsonPropertyName("t")]
        public double Temperature { get; set; }

        [JsonPropertyName("h")]
        public double Humidity { get; set; }
    }

    #endregion

    #region Commands
    #endregion

    #region Log Identity
    /// <summary>
    /// How should this component appear in the logs?
    /// </summary>
    /// <returns>String to identify the current device</returns>
    public override string ToString()
    {
        #if false
        if (PhysicalSensor is null)
        #endif
        return "Simulated TH";
    }
    #endregion

    #region Fields
    /// <summary>
    /// Connection to physical sensors
    /// </summary>
    /// <remarks>
    /// Or (null), indicating we are sending simulated sensor data
    /// </remarks>
    #if false
    private Shtc3Physical? PhysicalSensor = null;
    #endif

    private readonly IClock _clock;

    #endregion

    #region IComponentModel

    /// <summary>
    /// Identifier for the model implemented here
    /// </summary>
    [JsonIgnore]
    public string dtmi => "dtmi:brewhub:sensors:TH;1";

    /// <summary>
    /// Get an object containing all current telemetry
    /// </summary>
    /// <returns>All telemetry we wish to send at this time, or null for don't send any</returns>
    object? IComponentModel.GetTelemetry()
    {
        #if false
        // If we have a physical sensor connected, use that
        if (PhysicalSensor is not null)
        {
            if (PhysicalSensor.TryUpdate())
            {
                // Update the properties which track the current values
                CurrentHumidity = PhysicalSensor.Humidity;
                CurrentTemperature = PhysicalSensor.Temperature;

                // Return it
                return PhysicalSensor;
            }
            else
                return null;
        }
        // Otherwise, use simulated telemetry
        else
        #endif
        {
            // Take the reading
            var reading = new SimulatedTelemetry(_clock);

            // Adjust for corrections
            reading.Temperature += TemperatureCorrection;
            reading.Temperature += TemperatureCorrection;

            // Return it
            return reading;
        }
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

        if (key == "hcorr")
            return HumidityCorrection = Convert.ToDouble(jsonvalue);

        throw new NotImplementedException($"Property {key} is not implemented on {dtmi}");
    }

    /// <summary>
    /// Get an object containing all properties known to this model
    /// </summary>
    /// <returns>All known properties, and their current state</returns>
    object IComponentModel.GetProperties()
    {
        return this as TempHumidityModel;
    }

    /// <summary>
    /// Set the application intitial state from the supplied configuration values
    /// </summary>
    /// <param name="values">Dictionary of all known configuration values which could apply to this component</param>
    void IComponentModel.SetInitialState(IDictionary<string, string> values)
    {
        if (values.ContainsKey("tcorr"))
            TemperatureCorrection = Convert.ToDouble(values["tcorr"]);

        if (values.ContainsKey("hcorr"))
            HumidityCorrection = Convert.ToDouble(values["hcorr"]);

        #if false
        if (values.ContainsKey("Physical"))
        {
            var usephysical = Convert.ToBoolean(values["Physical"]);
            if (usephysical)
            {
                PhysicalSensor = new Shtc3Physical();
            }
        }
        #endif
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