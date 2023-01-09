using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BrewHub.Controller;

using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();

