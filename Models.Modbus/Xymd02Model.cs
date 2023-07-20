// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved
// Use of this source code is governed by the MIT license (see LICENSE file)

using BrewHub.Devices.Platform.Common.Logging;
using BrewHub.Devices.Platform.Common.Models;
using BrewHub.Controllers.Models.Modbus.Client;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace BrewHub.Controllers.Models.Modbus.Client;

/// <summary>
/// IoT Plug and Play implementation for XY-MD02 RS485 Temp/Humidity sensor
/// </summary>
/// <remarks>
/// http://www.sah.rs/media/sah/techdocs/xy-md02-manual.pdf
/// </remarks>
public class Xymd02Model :  IComponentModel
{
    #region Properties

    [JsonPropertyName("__t")]
    public string ComponentID => "c";

    [JsonPropertyName("tcorr")]
    public double TemperatureCorrection
    { 
        get
        {
            return UartOK switch
            {
                true => (double)(_client!.ReadHoldingRegisters<Int16>(Address, TemperatureCorrectionRegister, 1).ToArray()[0]) / 10.0,
                false => 0
            };
        }
        private set
        {
            if (UartOK)
            {
                short newval = (short)(value * 10.0);
                _client.WriteSingleRegister(Address, TemperatureCorrectionRegister, newval);
            }
        }
    }

    [JsonPropertyName("hcorr")]
    public double HumidityCorrection 
    { 
        get
        {
            return UartOK switch
            {
                true => (double)(_client!.ReadHoldingRegisters<Int16>(Address, HumidityCorrectionRegister, 1).ToArray()[0]) / 10.0,
                false => 0
            };
        }
        private set
        {
            if (UartOK)
            {
                short newval = (short)(value * 10.0);
                _client.WriteSingleRegister(Address, HumidityCorrectionRegister, newval);
            }
        }
    }

    #endregion

    #region Telemetry

    public class Telemetry
    {
        [JsonPropertyName("t")]
        public double Temperature { get; set; }

        [JsonPropertyName("h")]
        public double Humidity { get; set; }
    }

    #endregion

    #region Commands
    #endregion

    #region Constructor
    public Xymd02Model(IModbusClient client, ILoggerFactory logfact)
    {
        _client = client;

        // For more compact logs, only use the class name itself, NOT fully-qualified class name
        _logger = logfact.CreateLogger(nameof(Xymd02Model));
    }
    #endregion

    #region Log Identity
    /// <summary>
    /// How should this component appear in the logs?
    /// </summary>
    /// <returns>String to identify the current device</returns>
    public override string ToString()
    {
        return $"XY-MD02@{Address}";
    }
    #endregion

    #region Fields

    /// <summary>
    /// Which modbus client to use for communication
    /// </summary>
    private readonly IModbusClient _client;

    /// <summary>
    /// Where to log events
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Whether we should expect modbus operations to succeed
    /// </summary>
    private bool UartOK => _client.IsConnected && Address > 0;
    #endregion

    #region Internals

    /// <summary>
    /// Location on Modbus where this sensor is to be found
    /// </summary>
    internal int Address { get; private set; }

    /// <summary>
    /// Bus speed currently expected by this device
    /// </summary>
    /// <remarks>
    /// We do not allow changing it, because that seems dangerous
    /// </remarks>
    internal int BaudRate => 
        UartOK switch
        {
            true => _client!.ReadHoldingRegisters<Int16>(Address, BaudRateRegister, 1).ToArray()[0],
            false => -1
        };

    #endregion

    #region ModBus Registers
    const int FirstDataRegister = 1;
    const int TemperatureRegister = 1;
    const int HumidityRegister = 2;
    const int AfterLastDataRegister = 3;
    const int AddressRegister = 0x101;
    const int BaudRateRegister = 0x102;
    const int TemperatureCorrectionRegister = 0x103;
    const int HumidityCorrectionRegister = 0x104;
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
        if (!UartOK)
            return null;

        // Grab telemetry from sensor
        var inputs = _client!.ReadInputRegisters<Int16>(Address,FirstDataRegister,AfterLastDataRegister-FirstDataRegister).ToArray();

        // Save those as telemetry
        var reading = new Telemetry()
        {
            Temperature = (double)inputs[TemperatureRegister - FirstDataRegister] / 10.0,
            Humidity = (double)inputs[HumidityRegister - FirstDataRegister] / 10.0
        };

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
        return this as Xymd02Model;
    }

    /// <summary>
    /// Set the application intitial state from the supplied configuration values
    /// </summary>
    /// <param name="values">Dictionary of all known configuration values which could apply to this component</param>
    void IComponentModel.SetInitialState(IDictionary<string, string> values)
    {
        if (values.ContainsKey("Address"))
            Address = Convert.ToInt16(values["Address"]);

        _logger.LogDebug(ModbusLogEvents.ModbusSensorReady, "Sensor {sensor}: Ready? {isready}",this.ToString(),UartOK);
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