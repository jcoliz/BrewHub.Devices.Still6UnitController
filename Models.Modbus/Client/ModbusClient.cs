// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved
// Use of this source code is governed by the MIT license (see LICENSE file)

using FluentModbus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO.Ports;

namespace BrewHub.Controllers.Models.Modbus.Client;

/// <summary>
/// Modbus client wrapper for use in dependency injection
/// </summary>
/// <remarks>
/// Also provides thread safety against conflicting bus access
/// </remarks>
public class ModbusClient : IModbusClient
{
    private readonly IOptions<ModbusClientOptions> _options;
    private readonly ILogger<ModbusClient> _logger;
    private readonly ModbusRtuClient _client;
    private readonly Mutex _mutex = new Mutex();

    public ModbusClient(IOptions<ModbusClientOptions> options, ILogger<ModbusClient> logger)
    {
        _options = options;
        _logger = logger;
        _client = new ModbusRtuClient();
    }

    public bool IsConnected { get; private set; } = false;

    public void Connect()
    {
        if (IsConnected)
            return;

        try
        {
            _logger.LogInformation(ModbusLogEvents.ModbusCreating, "Creating with options {options}", _options.Value);
    
            _client.BaudRate = _options.Value.BaudRate;
            _client.Parity = _options.Value.Parity;
            _client.StopBits = _options.Value.StopBits;
            _client.ReadTimeout = (int)_options.Value.ReadTimeoutTimeSpan.TotalMilliseconds;
            _client.WriteTimeout = (int)_options.Value.WriteTimeoutTimeSpan.TotalMilliseconds;

            // TODO: Allow config of write timeout

            _client.Connect(_options.Value.Port!,ModbusEndianness.BigEndian);
            IsConnected = true;

            _logger.LogInformation(ModbusLogEvents.ModbusCreateOK, "Created OK on {port}",_options.Value.Port);
        }
        catch(Exception ex)
        {
            var names = string.Join(',', SerialPort.GetPortNames());

            _logger.LogCritical(ModbusLogEvents.ModbusCreateFailed, ex,"Failed to create on {port}. Available ports: {names}",_options.Value.Port ?? "null",names);
            throw;
        }
    }
    public Span<T> ReadInputRegisters<T>(int unitIdentifier, int startingAddress, int count) where T : unmanaged
    {
        _mutex.WaitOne();

        try
        {
            var result = _client.ReadInputRegisters<T>(unitIdentifier, startingAddress, count);
            return result;
        }
        finally
        {
            _mutex.ReleaseMutex();
        }
    }

    public Span<T> ReadHoldingRegisters<T>(int unitIdentifier, int startingAddress, int count) where T : unmanaged
    {
        _mutex.WaitOne();

        try
        {
            _logger.LogDebug(ModbusLogEvents.ModbusReadingHolding, "Reading holding {address}:{register}", unitIdentifier, startingAddress);
            var result = _client.ReadHoldingRegisters<T>(unitIdentifier, startingAddress, count);

            // Recovery time between sequential reads ("Poll delay")
            Thread.Sleep(TimeSpan.FromMilliseconds(100));
            return result;
        }
        finally
        {
            _mutex.ReleaseMutex();
        }
    }

    public void WriteSingleRegister(int unitIdentifier, int registerAddress, short value)
    {
        _mutex.WaitOne();

        try
        {
            _client.WriteSingleRegister(unitIdentifier, registerAddress, value);
        }
        finally
        {
            _mutex.ReleaseMutex();
        }
    }
}
