# BrewHub.Net: Multi-stack IoT Architecture

BrewHub.Net is an IoT reference architecture using .NET-based device software, InfluxDB and Grafana on a multi-node edge cluster, connected to Azure Services on the backend, with a Vue.JS dashboard for user monitoring and control.

More details: [BrewHub.Edge](https://github.com/jcoliz/BrewHub.Edge)

![Distillery](docs/images/feature.png)

## What's Here: Six-Unit Distillery Prototype

The Six-Unit Distillery is a prototype implementation to demonstrate the BrewHub.Net IoT Reference Architecture across the device, edge, cloud, and web stacks. This repository contains the device control software for the prototype.

Originally created for a spirits distiller, the prototype presents a simplified version of the industrial continuous distillation
equipment illustrated above. While there are **nineteen** components on the full version, this scaled-down prototype 
works the same way, it's just easier to explain.

We use the term "six-unit" here to refer to the components of the device model which is surfaced on the dashboard. Those units here are:

* **Device**: The device itself. For the prototype, we ran on a [Waveshare RPi Zero Relay](https://www.waveshare.com/rpi-zero-relay.htm).
* **Ambient Conditions**: A temperature and humidity sensor providing details about the conditions in the room where the equipment is installed.
* **Reflux Thermostat**: Controls the temperature at the top of the rectifier column by opening or closing the Reflux Valve
* **Reflux Valve**: Controls the flow of reflux (fully distilled product) introduced at the top of the rectifier column. By opening the reflux valve, the temperature is reduced through the inflow of cooled product.
* **Condenser Thermostat**: Controls the temperature at the bottom of the condenser tower by opening or closing the Consender Valve.
* **Condenser Valve**: Controls the flow of cold water circulated through the condenser column. This water removes the heat from the product which was added earlier in the process.

## Getting Started

The fastest way to get started is bring up an instance of the [BrewHub.Net Edge Stack](https://github.com/jcoliz/BrewHub.Edge),
while including the optional controllers to generate synthetic data. Those controllers are instances of the Six-Unit Distillery device controller in a container. Of course, device software doesn't run in a container in a real system, however this will get a demo
up and running quickly.

```
$ git clone https://github.com/jcoliz/BrewHub.Edge
$ cd BrewHub.Edge
$ docker compose -f docker-compose.yml -f docker-compose-controllers.yml up -d
```

## Building Locally

The controller is build on .NET 7.0. To build it, you'll need the [.NET SDK](https://dotnet.microsoft.com/en-us/download) installed on your machine.

It needs to know where to look for an MQTT broker, and (optionally) what to use as a deviceid (client id) when connecting. You can use any supported .NET configuration method, or you can create a `config.toml` file in the project root:

```toml
[mqtt]
server = "localhost"

[provisioning]
deviceid = "your-device-name"
```

Once you're configured, simply `dotnet run` to get started.

```
$ dotnet run
<6> [ 17/06/2023 19:18:08 ] MqttWorker[100] Started OK
<6> [ 17/06/2023 19:18:08 ] MqttWorker[200] Initial State: OK Applied 9 keys
<6> [ 17/06/2023 19:18:08 ] MqttWorker[101] Device: BrewHub 6-Unit Distillery Prototype v1 S/N:1234567890-ABCDEF ver:0.0.0
<6> [ 17/06/2023 19:18:08 ] MqttWorker[102] Model: dtmi:brewhub:prototypes:still_6_unit;1
<6> [ 17/06/2023 19:18:08 ] MqttWorker[300] Provisioning: OK. Device your-device-name
<6> [ 17/06/2023 19:18:08 ] MqttWorker[400] Connection: OK.
<6> [ 17/06/2023 19:18:09 ] MqttWorker[500] Telemetry: OK 4 messages
<6> [ 17/06/2023 19:18:09 ] MqttWorker[720] Property: Reported OK. Next update after 00:01:00
```

Open up the dashboard on `http://localhost:80`, and you'll soon see `your-device-name` show up with its new data.

## Running on Rasperry Pi

This controller ultimately will connect to physical sensors to send true data. It will run on any .NET-supported platform, including Rasperry Pi. For the prototype, only the Ambient Conditions component was connected to a physical sensor.

## Running on Microcontrollers

The BrewHub.Net stack requires very few resources. As a future project, I'll demonstrate running this on an Espressif MCU-based device. Stay tuned!
