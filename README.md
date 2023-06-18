# BrewHub.Net: Multi-stack IoT Architecture

BrewHub.Net is an IoT reference architecture using .NET-based device software, InfluxDB and Grafana on a multi-node edge cluster, connected to Azure Services on the backend, with a Vue.JS dashboard for user monitoring and control.

![Reference Architecture](docs/images/IoT%20Reference%20Architecture%20InfluxDB%20Azure.png)

| Layer | Purpose | Projects |
| -------- | ------- |  ------- |
| Devices | Compute devices connected to machinery, equipment or others. Drives sensors and actuators to sense or drive the attached equipment. May be connected directly, or through industrial control systems (e.g. PLCs) | [6-Unit Controller](https://github.com/jcoliz/BrewHub.Devices.Still6UnitController)
| Edge  | Multi-node edge cluster(s), located on-premises, connected to each device. Handles the 'hot path' of data as it immediately comes off devices. Provides key near-real-time insights and alerting. Cools the data slightly before sending a less-frequent and more-focused data representation to the cloud  | [Edge](https://github.com/jcoliz/BrewHub.Edge)
| Cloud   | Back-end services collect and reason over multiple sites, provide a big-picture view and wide-scope control plane. | Coming soon!
| Dashboard | Gives users a single pane of glass to monitor, configure, control, and gain insights about their system. Can run cloud-side to look across an entire operation, or on the edge to give a low-latency view into a single site. | [Dashboard](https://github.com/jcoliz/BrewHub.Dashboard)

The **Six-Unit Distillery** is a reference implementation to demonstrate how this architecture can effectively be brought together across the device, edge, cloud, and web stacks.

## What's Here: Device Control Software

This project contains the device control software for the "Six Unit Distillery" reference implementation.

## Getting Started

The easiest way to get started is to run the controller in a container on your PC to start sending synthetic data into the system. You'll need access to an MQTT broker accepting unauthenticated connections. If you have one, simply replace the `MQTT__SERVER` environment variable with your server address. If not, you can quickly bring up an instance of the [BrewHub.Net Edge Stack](https://github.com/jcoliz/BrewHub.Edge).

```
docker run --name controllerdemo -e MQTT__SERVER=host.docker.internal  jcoliz/brewhub-still6unit-controller:latest

<6> [ 18/06/2023 02:29:18 ] BrewHub.Devices.Platform.Mqtt.MqttWorker[100] Started OK
<6> [ 18/06/2023 02:29:18 ] BrewHub.Devices.Platform.Mqtt.MqttWorker[200] Initial State: OK Applied 9 keys
<6> [ 18/06/2023 02:29:18 ] BrewHub.Devices.Platform.Mqtt.MqttWorker[101] Device: BrewHub 6-Unit Distillery Prototype v1 S/N:1234567890-ABCDEF ver:0.0.0
<6> [ 18/06/2023 02:29:18 ] BrewHub.Devices.Platform.Mqtt.MqttWorker[102] Model: dtmi:brewhub:prototypes:still_6_unit;1
<6> [ 18/06/2023 02:29:18 ] BrewHub.Devices.Platform.Mqtt.MqttWorker[300] Provisioning: OK. Device f8b390f49e6c
<6> [ 18/06/2023 02:29:18 ] BrewHub.Devices.Platform.Mqtt.MqttWorker[400] Connection: OK.
<6> [ 18/06/2023 02:29:18 ] BrewHub.Devices.Platform.Mqtt.MqttWorker[500] Telemetry: OK 4 messages
<6> [ 18/06/2023 02:29:18 ] BrewHub.Devices.Platform.Mqtt.MqttWorker[720] Property: Reported OK. Next update after 00:01:00
<6> [ 18/06/2023 02:29:18 ] Microsoft.Hosting.Lifetime[0] Application started. Press Ctrl+C to shut down.
<6> [ 18/06/2023 02:29:18 ] Microsoft.Hosting.Lifetime[0] Hosting environment: Production
<6> [ 18/06/2023 02:29:18 ] Microsoft.Hosting.Lifetime[0] Content root path: /app

```

## Building Locally

This controller is build on .NET 7.0. To build it, you'll need the SDK installed on your machine.

As above, you will need to tell the controller where to look for an MQTT broker, and (optionally) what to use as a deviceid (client id) when connecting. You can use any supported .NET configuration method, or you can create a `config.toml` file in the project root:

```toml
[mqtt]
server = "localhost"

[provisioning]
deviceid = "Beach-6"
```

Once you're configured, simply `dotnet run` to get started.

```
PS> dotnet run
<6> [ 17/06/2023 19:18:08 ] BrewHub.Devices.Platform.Mqtt.MqttWorker[100] Started OK
<6> [ 17/06/2023 19:18:08 ] BrewHub.Devices.Platform.Mqtt.MqttWorker[200] Initial State: OK Applied 9 keys
<6> [ 17/06/2023 19:18:08 ] BrewHub.Devices.Platform.Mqtt.MqttWorker[101] Device: BrewHub 6-Unit Distillery Prototype v1 S/N:1234567890-ABCDEF ver:0.0.0-JamesColiz-06171918
<6> [ 17/06/2023 19:18:08 ] BrewHub.Devices.Platform.Mqtt.MqttWorker[102] Model: dtmi:brewhub:prototypes:still_6_unit;1
<6> [ 17/06/2023 19:18:08 ] BrewHub.Devices.Platform.Mqtt.MqttWorker[300] Provisioning: OK. Device Beach-6
<6> [ 17/06/2023 19:18:08 ] BrewHub.Devices.Platform.Mqtt.MqttWorker[400] Connection: OK.
<6> [ 17/06/2023 19:18:09 ] BrewHub.Devices.Platform.Mqtt.MqttWorker[500] Telemetry: OK 4 messages
<6> [ 17/06/2023 19:18:09 ] BrewHub.Devices.Platform.Mqtt.MqttWorker[720] Property: Reported OK. Next update after 00:01:00
```

## Running on Rasperry Pi

This controller ultimately will connect to physical sensors to send true data. It will run on any .NET-supported platform, including Rasperry Pi.

## Running on Microcontrollers

The BrewHub.Net stack requires very little resources. As a future project, I'll demonstrate running this on an Espressif MCU-based device. Stay tuned!
