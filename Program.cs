// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved

using BrewHub.Controller.Mqtt;

using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        services.AddHostedService<MqttWorker>();
        services.AddSingleton<IRootModel>(new ControllerModel());
    })
    .ConfigureAppConfiguration(config =>
    {
        config.AddTomlFile("config.toml", optional: true, reloadOnChange: true);
    })
    .Build();

host.Run();
