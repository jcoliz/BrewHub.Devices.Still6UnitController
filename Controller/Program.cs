﻿// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved
// Use of this source code is governed by the MIT license (see LICENSE file)

using BrewHub.Devices.Platform.Common.Models;
using BrewHub.Devices.Platform.Mqtt;
using BrewHub.Protocol.Mqtt;
using BrewHub.Controllers;
using BrewHub.Controllers.Models.Modbus.Client;

using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddHostedService<MqttWorker>();
        services.AddSingleton<IRootModel,Still6UnitModel>();
        services.AddSingleton<IModbusClient, ModbusClient>();

        services.Configure<MqttOptions>(
            context.Configuration.GetSection(MqttOptions.Section)
        );
        services.Configure<ModbusClientOptions>(
            context.Configuration.GetSection(ModbusClientOptions.Section)
        );
    })
    .ConfigureAppConfiguration(config =>
    {
        config.AddTomlFile("config.toml", optional: true, reloadOnChange: true);
    })
    .Build();

host.Run();
