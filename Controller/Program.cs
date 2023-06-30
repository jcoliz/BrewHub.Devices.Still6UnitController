// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved

using BrewHub.Devices.Platform.Common.Models;
using BrewHub.Devices.Platform.Mqtt;
using BrewHub.Protocol.Mqtt;
using BrewHub.Controllers;

using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        services.AddHostedService<MqttWorker>();
        services.AddSingleton<IRootModel,Still6UnitModel>();

        var section = hostContext.Configuration.GetSection(MqttOptions.Section);
        if (section.Exists())
            services.Configure<MqttOptions>(section);
    })
    .ConfigureAppConfiguration(config =>
    {
        config.AddTomlFile("config.toml", optional: true, reloadOnChange: true);
    })
    .Build();

host.Run();
