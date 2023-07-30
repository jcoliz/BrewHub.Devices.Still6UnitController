// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved
// Use of this source code is governed by the MIT license (see LICENSE file)

using BrewHub.Devices.Platform.Common.Comms;
using System.Device.Gpio;
using System.Text.Json.Serialization;

namespace BrewHub.Controllers.Models.Gpio;

/// <summary>
/// Binary Valve: Controls a single two-state (on/off) valve, using Gpio pins
/// </summary>
/// <remarks>
/// Implements "dtmi:brewhub:controls:BinaryValve;1";
/// 
/// Cannot run this on a device that doesn't have a GPIO controller.
/// Use Synthetic.BinaryValveModel instead.
/// </remarks>
public class BinaryValveGpioModel :  BrewHub.Controllers.Models.Synthetic.BinaryValveModel
{
    #region Constructor

    public BinaryValveGpioModel(IComponentCommunicator? comms = null): base(comms)
    {
        _controller = new();

        // For testing natively
        //_controller = null;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Whether the valve is open. Only writable if `Source Metric` is null
    /// </summary>
    [JsonPropertyName("open")]
    public override bool IsOpen 
    { 
        get
        {
            return _IsOpen;
        }
        set
        {
            _IsOpen = value;

            if (Pin.HasValue)
                _controller?.Write(Pin.Value, _IsOpen);
        } 
    }
    private bool _IsOpen = false;

    #endregion

    #region Telemetry
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
        return $"Binary Valve on GPIO#{Pin?.ToString()??"(Not Configured)"}";
    }
    #endregion

    #region Fields

    private readonly GpioController? _controller;

    /// <summary>
    /// Which GPIO pin we operate, or null for do not touch the GPIO
    /// </summary>
    private int? Pin { get; set; }
    #endregion

    #region IComponentModel

    /// <summary>
    /// Set the application intitial state from the supplied configuration values
    /// </summary>
    /// <param name="values">Dictionary of all known configuration values which could apply to this component</param>
    public override void SetInitialState(IDictionary<string, string> values)
    {
        if (values.ContainsKey("Pin"))
        {
            Pin = Convert.ToInt32(values["Pin"]);

            if (Pin.HasValue)
                _controller?.OpenPin(Pin.Value, PinMode.Output);

            IsOpen = false;
        }

        base.SetInitialState(values);
    }
 
    #endregion
}