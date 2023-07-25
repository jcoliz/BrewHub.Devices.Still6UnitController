// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved
// Use of this source code is governed by the MIT license (see LICENSE file)

using BrewHub.Devices.Platform.Common.Comms;
using BrewHub.Devices.Platform.Common.Models;
using BrewHub.Controllers.Models.Synthetic;
using BrewHub.Controllers.Models.Modbus;
using BrewHub.Controllers.Models.Modbus.Client;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BrewHub.Controllers;

/// <summary>
/// Implementation for BrewHub 6-Unit Distillery Prototype v1
/// </summary>
/// <remarks>
/// "dtmi:brewhub:prototypes:still_6_unit;1";
/// </remarks>
public class Still6UnitModel : DeviceInformationModel, IRootModel
{
    #region Constructor

    public Still6UnitModel(IModbusClient client, ILoggerFactory logfact)
    {
        _modbusclient = client;
        _logfact = logfact;

        // "For now" just going to create one here. Could instead dependency-inject this,
        // but that seems like overkill.
        _comms = new ComponentCommunicator(this);

        // TODO: Get working also on Linux
        //https://github.com/dotnet/orleans/blob/3.x/src/TelemetryConsumers/Orleans.TelemetryConsumers.Linux/LinuxEnvironmentStatistics.cs

        if ( RuntimeInformation.IsOSPlatform(OSPlatform.Windows) )
        {
            _cpuCounter = new PerformanceCounter();
            _cpuCounter.CategoryName = "Processor";
            _cpuCounter.CounterName = "% Processor Time";
            _cpuCounter.InstanceName = "_Total";
        }
    }

    #endregion

    #region Base Device Properties

    public string? SerialNumber { get; private set; } = "Unassigned";

    // Note that telemetry period is not strictly part of the DTMI. Still,
    // it's nice to be able to set it in config, and send down changes to it

    /// <summary>
    /// How frequently to send telemetry
    /// </summary>
    public string TelemetryInterval
    {
        get
        {
            return XmlConvert.ToString(_TelemetryPeriod);
        }
        private set
        {
            _TelemetryPeriod = XmlConvert.ToTimeSpan(value);
        }
    }
    private TimeSpan _TelemetryPeriod = TimeSpan.Zero;

    /// <summary>
    /// Time this device was last started (UTC)
    /// </summary>
    public DateTimeOffset StartTimeUTC { get; } = DateTimeOffset.UtcNow;

    #endregion

    #region Telemetry

    public class Telemetry
    {
        /// <summary>
        /// Current working set of the device memory in KiB
        /// </summary>
        [JsonPropertyName("WorkingSet")]
        public double WorkingSetKiB
        {
            get
            {
                var ws = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64;

                // Convert to Kibibits
                return (double)ws / (1024.0 / 8.0);
            }
        }

        /// <summary>
        /// Device status. Zero is OK, >0 increasing severity to 999
        /// </summary>
        public int Status { get; set; }

        /// <summary>
        /// Current CPU load (percent)
        /// </summary>
        public double? CpuLoad { get; set; }
    }

    #endregion

    #region Commands

    /// <summary>
    /// Reboots the device after waiting the specified time
    /// </summary>
    /// <param name="jsonparams">Delay to wait, in seconds</param>
    /// <returns></returns>
    protected Task<object> Reboot(string jsonparams)
    {
        var delay = (jsonparams.Length > 0) ? JsonSerializer.Deserialize<int>(jsonparams) : 0;

        // TODO: Do something with this command

        return Task.FromResult<object>(new());
    }

    #endregion

    #region Log Identity
    /// <summary>
    /// How should this model appear in the logs?
    /// </summary>
    /// <returns>String to identify the current model</returns>
    public override string ToString()
    {
        return $"{base.Manufacturer} {base.DeviceModel} S/N:{SerialNumber} ver:{base.SoftwareVersion}";
    }
    #endregion

    #region Fields
    private readonly IComponentCommunicator _comms;
    private readonly IModbusClient _modbusclient;
    private readonly ILoggerFactory _logfact;
    #endregion

    #region IRootModel

    /// <summary>
    /// How often to send telemetry, or zero to avoid sending any telemetry right now
    /// </summary>
    TimeSpan IRootModel.TelemetryPeriod => _TelemetryPeriod;

    /// <summary>
    /// The components which are contained within this one
    /// </summary>
    [JsonIgnore]
    public IDictionary<string, IComponentModel> Components { get; } = new Dictionary<string, IComponentModel>();
    #endregion

    #region Internals

    protected readonly PerformanceCounter? _cpuCounter;

    #endregion

    #region IComponentModel

    /// <summary>
    /// Identifier for this model
    /// </summary>
    [JsonIgnore]
    public string dtmi => "dtmi:brewhub:prototypes:still_6_unit;1";

    /// <summary>
    /// Get an object containing all current telemetry
    /// </summary>
    /// <returns>All telemetry we wish to send at this time, or null for don't send any</returns>
    object? IComponentModel.GetTelemetry()
    {
        var reading = new Telemetry();

        //https://github.com/dotnet/orleans/blob/3.x/src/TelemetryConsumers/Orleans.TelemetryConsumers.Linux/LinuxEnvironmentStatistics.cs
        #pragma warning disable CA1416
        if (_cpuCounter is not null)
            reading.CpuLoad = _cpuCounter.NextValue();
        #pragma warning restore CA1416

        // Take the reading, return it
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
        if (key != "TelemetryInterval")
            throw new NotImplementedException($"Property {key} is not implemented on {dtmi}");

        return TelemetryInterval = JsonSerializer.Deserialize<string>(jsonvalue)!;
    }

    /// <summary>
    /// Get an object containing all properties known to this model
    /// </summary>
    /// <returns>All known properties, and their current state</returns>
    object IComponentModel.GetProperties()
    {
        return this as Still6UnitModel;
    }

    private void SetConfiguration(string config)
    {
        object From(string value)
        {
            if (Boolean.TryParse(value, out var bval))
                return bval;
            else
                return value;            
        }

        var needmodbus = false;

        foreach( var kvp in config.Split(',').Select(x=>x.Split('=')).ToDictionary(x=>x[0],x=>From(x[1])) )
        {
            switch (kvp)
            {
                case { Key: "amb", Value: true }:
                    Components["amb"] = new TempHumidityModel(null);
                    break;

                case { Key: "amb", Value: nameof(SonbestSm7820Model) }:
                    Components["amb"] = new SonbestSm7820Model(_modbusclient,_logfact);
                    needmodbus = true;
                    break;

                case { Key: "amb", Value: nameof(Xymd02Model) }:
                    Components["amb"] = new Xymd02Model(_modbusclient,_logfact);
                    needmodbus = true;
                    break;

                case { Key: "ct", Value: true }:
                    Components["ct"] = new ThermostatModelBH(null,_comms);
                    break;

                case { Key: "rt", Value: true }:
                    Components["rt"] = new ThermostatModelBH(null,_comms);
                    break;

                case { Key: "cv", Value: true }:
                    Components["cv"] = new BinaryValveModel(_comms);
                    break;

                case { Key: "rv", Value: true }:
                    Components["rv"] = new BinaryValveModel(_comms);
                    break;

                case { Value: false }:
                    break;

                default:
                    throw new ApplicationException($"Component not recognized {kvp.Key}={kvp.Value}");
            }
        }

        if (needmodbus)
            _modbusclient.Connect();
    }

    /// <summary>
    /// Set the application intitial state from the supplied configuration values
    /// </summary>
    /// <param name="values">Dictionary of all known configuration values which could apply to this component</param>
    void IComponentModel.SetInitialState(IDictionary<string, string> values)
    {
        if (values.ContainsKey("Version"))
            base.SoftwareVersion = values["Version"];

        if (values.ContainsKey("SerialNumber"))
            SerialNumber = values["SerialNumber"];

        if (values.ContainsKey("TelemetryPeriod"))
            TelemetryInterval = values["TelemetryPeriod"];

        if (values.TryGetValue("Components",out var value))
            SetConfiguration(value);

        // Pass along initial state to the base class
        base.SetInitialState(values);
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
            "reboot" => Reboot(jsonparams),
            _ => throw new NotImplementedException($"Command {name} is not implemented on {dtmi}")
        };
    }

    #endregion    
}