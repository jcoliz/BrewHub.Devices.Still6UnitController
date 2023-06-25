// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved

using BrewHub.Devices.Platform.Common;
using BrewHub.Devices.Platform.Mqtt;
using BrewHub.Controllers;

using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        services.AddHostedService<MqttWorker>();
        services.AddSingleton<IRootModel,Still6UnitModel>();
    })
    .ConfigureAppConfiguration(config =>
    {
        config.AddTomlFile("config.toml", optional: true, reloadOnChange: true);
    })
    .Build();

host.Run();
