// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved
// Use of this source code is governed by the MIT license (see LICENSE file)

using System.IO.Ports;
using System.Xml;

namespace BrewHub.Controllers.Models.Modbus.Client;

/// <summary>
/// Options for configuring the modbus client
/// </summary>
public class ModbusClientOptions
{
    public const string Section = "Modbus";

    public string? Port { get; set; } = null;
    public int BaudRate { get; set; } = 9600;
    public Parity Parity { get; set; } = Parity.Even;
    public StopBits StopBits { get; set; } = StopBits.One;
    public string ReadTimeout { get; set; } = "PT1S";
    public string WriteTimeout { get; set; } = "PT1S";
     
    public TimeSpan ReadTimeoutTimeSpan => XmlConvert.ToTimeSpan(ReadTimeout);
    public TimeSpan WriteTimeoutTimeSpan => XmlConvert.ToTimeSpan(WriteTimeout);

    public override string ToString()
    {
        return $"Port={Port ?? "null"};BaudRate={BaudRate};Parity={Parity};StopBits={StopBits};ReadTimeout={ReadTimeout};WriteTimeout={WriteTimeout}";
    }
}
