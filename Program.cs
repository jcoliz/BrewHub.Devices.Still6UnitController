// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved

using BrewHub;
using BrewHub.Controller.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        services.AddHostedService<IoTHubWorker>();
        services.AddSingleton<IRootModel>(new StillControllerModel());
    })
    .Build();

await host.RunAsync();

