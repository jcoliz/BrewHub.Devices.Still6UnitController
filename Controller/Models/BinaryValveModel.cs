// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved
// Use of this source code is governed by the MIT license (see LICENSE file)

using BrewHub.Devices.Platform.Common;
using System.Text.Json.Serialization;

/// <summary>
/// Binary Valve: Controls a single two-state (on/off) valve.
/// </summary>
public class BinaryValveModel :  IComponentModel
{
    #region Properties

    [JsonPropertyName("__t")]
    public string ComponentID => "c";

    /// <summary>
    /// Override system contol
    /// </summary>
    [JsonPropertyName("manual")]
    public bool ManualControl { get; set; }

    /// <summary>
    /// Whether the valve is open. Only writable if `Manual Control` is turned on
    /// </summary>
    [JsonPropertyName("open")]
    public bool IsOpen { get; set; }

    #endregion

    #region Telemetry
    #endregion

    #region Commands
    #endregion

    #region Log Identity
    #endregion

    #region Fields
    #endregion

    #region IComponentModel

    /// <summary>
    /// Identifier for the model implemented here
    /// </summary>
    [JsonIgnore]
    public string dtmi => "dtmi:brewhub:controls:BinaryValve;1";

    /// <summary>
    /// Get an object containing all current telemetry
    /// </summary>
    /// <returns>All telemetry we wish to send at this time, or null for don't send any</returns>
    object? IComponentModel.GetTelemetry()
    {
        // Component does not generate telemetry
        return null;
    }

    /// <summary>
    /// Set a particular property to the given value
    /// </summary>
    /// <param name="key">Which property</param>
    /// <param name="jsonvalue">Value to set (will be deserialized from JSON)</param>
    /// <returns>The unserialized new value of the property</returns>
    object IComponentModel.SetProperty(string key, string jsonvalue)
    {
        if (key == "manual")
            return ManualControl = Convert.ToBoolean(jsonvalue);

        if (key == "open")
            return IsOpen = Convert.ToBoolean(jsonvalue);

        throw new NotImplementedException($"Property {key} is not implemented on {dtmi}");
    }

    /// <summary>
    /// Get an object containing all properties known to this model
    /// </summary>
    /// <returns>All known properties, and their current state</returns>
    object IComponentModel.GetProperties()
    {
        return this as BinaryValveModel;
    }

    /// <summary>
    /// Set the application intitial state from the supplied configuration values
    /// </summary>
    /// <param name="values">Dictionary of all known configuration values which could apply to this component</param>
    void IComponentModel.SetInitialState(IDictionary<string, string> values)
    {
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