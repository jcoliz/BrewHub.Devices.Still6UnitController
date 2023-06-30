// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved
// Use of this source code is governed by the MIT license (see LICENSE file)

using BrewHub.Devices.Platform.Common.Models;
using BrewHub.Devices.Platform.Common.Clock;
using System.Text.Json.Serialization;

/// <summary>
/// Basic Temperature &amp; Humidity Sensor
/// With synthetic (simulated) data
/// </summary>
public class TempHumidityTestModel :  IComponentModel
{
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
    /// Generates synthetic telemetry in case we don't have an actual sensor
    /// attached.
    /// </summary>
    /// <remarks>
    /// If we have a physical sensor attached, we should not be using this
    /// model at all.
    /// </remarks>
    public class SimulatedTelemetry
    {
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
        return "Test T&H";
    }
    #endregion

    #region Fields

    internal double SetTemperature { get; set; }
    internal double SetHumidity { get; set; }

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
        // Take the reading
        var reading = new SimulatedTelemetry() { Temperature = SetTemperature, Humidity = SetHumidity };

        // Adjust for corrections
        reading.Temperature += TemperatureCorrection;
        reading.Humidity += HumidityCorrection;

        // Return it
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
        return this as TempHumidityTestModel;
    }

    /// <summary>
    /// Set the application intitial state from the supplied configuration values
    /// </summary>
    /// <param name="values">Dictionary of all known configuration values which could apply to this component</param>
    void IComponentModel.SetInitialState(IDictionary<string, string> values)
    {
        if (values.ContainsKey("temp"))
            SetTemperature = Convert.ToDouble(values["temp"]);

        if (values.ContainsKey("tcorr"))
            TemperatureCorrection = Convert.ToDouble(values["tcorr"]);

        if (values.ContainsKey("hcorr"))
            HumidityCorrection = Convert.ToDouble(values["hcorr"]);
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