// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved
// Use of this source code is governed by the MIT license (see LICENSE file)

using BrewHub.Devices.Platform.Common;
using MQTTnet;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using MQTTnet.Client.Receiving;
using MQTTnet.Extensions.ManagedClient;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace BrewHub.Devices.Platform.Mqtt;

public class MqttWorker : DeviceWorker
{
#region Injected Fields

    private readonly IRootModel _model;
    private readonly ILogger<MqttWorker> _logger;
    // Note that we need the entire config, because we have to pass unstructured
    // InitialState properties to the model
    private readonly IConfiguration _config;
    private readonly IHostEnvironment _hostenv;

    #endregion

    #region Fields
    private IManagedMqttClient? mqttClient;
    string deviceid = string.Empty;
    string basetopic = string.Empty;
    private readonly JsonSerializerOptions _jsonoptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private readonly Regex _dtmiStrippingRegex = new Regex("^dtmi:(.+)");
    #endregion

    #region Constructor
    public MqttWorker(ILogger<MqttWorker> logger, IRootModel model, IConfiguration config, IHostEnvironment hostenv, IHostApplicationLifetime lifetime): 
        base(logger,model,config,hostenv,lifetime)
    {
        _logger = logger;
        _model = model;
        _config = config;
        _hostenv = hostenv;
    }
#endregion

#region Startup
    /// <summary>
    /// Provision this device according to the config supplied in "Provisioning" section
    /// </summary>
    /// <remarks>
    /// Note that the Provisioning section is designed to follow the format of the config.toml
    /// used by Azure IoT Edge. So if you generate a config.toml for an edge device, you can
    /// use it here. Just be sure to add the config.toml to the HostConfiguration during setup.
    /// (See examples for how this is done.)
    /// </remarks>
    /// <exception cref="ApplicationException">Thrown if provisioning fails (critical error)</exception>
    protected override Task ProvisionDevice()
    {
        try
        {
            deviceid = _config["Provisioning:deviceid"] ?? System.Net.Dns.GetHostName();

            // If we're running in docker...
            if ( Boolean.Parse(_config["Provisioning:docker"] ?? "false") )
            {
                // We need the synthetic data to have some skew.

                // We will tell the root level component to generate the skew,
                // using the deviceid as the seed.
                _model.SetInitialState(new Dictionary<string,string>() {{ "Skew", deviceid }});
            }

            _logger.LogInformation(LogEvents.ProvisionOK,"Provisioning: OK. Device {id}", deviceid);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(LogEvents.ProvisionError,"Provisioning: Error {message}", ex.Message);
            throw;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Open a connection to InfluxDB
    /// </summary>
    /// <exception cref="ApplicationException">Thrown if connection fails (critical error)</exception>
    protected async override Task OpenConnection()
    {
        string GetConfig(string key)
        {
            return _config[key] ?? throw new ApplicationException($"Failed. Please supply {key} in configuration");
        }

        try
        {
            if (!_config.GetSection("MQTT").Exists())
                throw new ApplicationException($"Unable to find MQTT connection details in app configuration. Create a config.toml with connection details in the content root ({_hostenv.ContentRootPath}).");

            var server = GetConfig("MQTT:server");
            var port = _config["MQTT:port"] ?? "1883";
            var topic1 = GetConfig("MQTT:topic");
            var site = _config["MQTT:site"] ?? "none";
            basetopic = $"{topic1}/{site}";

            MqttClientOptionsBuilder builder = new MqttClientOptionsBuilder()
                                        .WithClientId(deviceid)
                                        .WithTcpServer(server, Convert.ToInt32(port));

            ManagedMqttClientOptions options = new ManagedMqttClientOptionsBuilder()
                                    .WithAutoReconnectDelay(TimeSpan.FromSeconds(30))
                                    .WithClientOptions(builder.Build())
                                    .Build();

            mqttClient = new MqttFactory().CreateManagedMqttClient();

            mqttClient.ConnectedHandler = new MqttClientConnectedHandlerDelegate(OnConnected);
            mqttClient.DisconnectedHandler = new MqttClientDisconnectedHandlerDelegate(OnDisconnected);            
            mqttClient.ConnectingFailedHandler = new ConnectingFailedHandlerDelegate(OnConnectingFailed);

            mqttClient.ApplicationMessageReceivedHandler = new MqttApplicationMessageReceivedHandlerDelegate(a => {
                _logger.LogInformation("Message recieved: {payload}", System.Text.Encoding.UTF8.GetString(a.ApplicationMessage.Payload));
            });

            await mqttClient.StartAsync(options);

            _logger.LogDebug(LogEvents.Connecting,"Connection: Connecting on {server}:{port}",server,port);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(LogEvents.ConnectFailed,"Connection: Failed {message}", ex.Message);
            throw new ApplicationException("Connection to MQTT Broker failed", ex);
        }
    }

    private void OnConnected(MqttClientConnectedEventArgs obj)
    {
        _logger.LogInformation(LogEvents.ConnectOK,"Connection: OK.");
    }

    private void OnConnectingFailed(ManagedProcessFailedEventArgs obj)
    {
        _logger.LogError(LogEvents.ConnectFailed, obj.Exception, "Connection: Failed.");
    }

    private void OnDisconnected(MqttClientDisconnectedEventArgs obj)
    {
        _logger
            .LogError(
                LogEvents.ConnectDisconnectedError,
                "Connection: Error, Disconnected. {Reason} {was} {type} {message}", 
                obj.Reason, 
                obj.ClientWasConnected,
                obj.Exception?.GetType().Name ?? "(null)", obj.Exception?.Message ?? "(null)"
            );
    }

    #endregion

    #region Commands
    #endregion

    #region Telemetry
    /// <summary>
    /// Send latest telemetry from root and all components
    /// </summary>
    protected async override Task SendTelemetry()
    {
        try
        {
            int numsent = 0;

            // Send telementry from root

            if (_model.TelemetryPeriod > TimeSpan.Zero)
            {
                // Obtain readings from the root
                var readings = _model.GetTelemetry();

                // If telemetry exists
                if (readings is not null)
                {
                    // Send them
                    await SendDataMessageAsync(readings, new(string.Empty, _model));
                    ++numsent;
                }

                // Send telemetry from components

                foreach(var kvp in _model.Components)
                {
                    // Obtain readings from this component
                    readings = kvp.Value.GetTelemetry();
                    if (readings is not null)
                    {
                        // Note that official PnP messages can only come from a single component at a time.
                        // This is a weakness that drives up the message count. So, will have to decide later
                        // if it's worth keeping this, or scrapping PnP compatibility and collecting them all
                        // into a single message.

                        // Send them
                        await SendDataMessageAsync(readings, kvp);
                        ++numsent;
                    }
                }

                if (numsent > 0)
                    _logger.LogInformation(LogEvents.TelemetryOK,"Telemetry: OK {count} messages",numsent);            
                else
                    _logger.LogWarning(LogEvents.TelemetryNotSent,"Telemetry: No components had available readings. Nothing sent");
            }
            else
                _logger.LogWarning(LogEvents.TelemetryNoPeriod,"Telemetry: Telemetry period not configured. Nothing sent. Will try again in {period}",TelemetryRetryPeriod);
        }
        catch (AggregateException ex)
        {
            foreach (Exception exception in ex.InnerExceptions)
            {
                _logger.LogError(LogEvents.TelemetryMultipleError, exception, "Telemetry: Multiple Errors");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(LogEvents.TelemetrySingleError,ex,"Telemetry: Error");
        }
    }

    private int sequencenumber = 1;

    /// <summary>
    /// Send a single node or device data message
    /// </summary>
    /// <remarks>
    /// Can be telemetry or properties or both
    /// </remarks>
    private async Task SendDataMessageAsync(object telemetry, KeyValuePair<string, IComponentModel> component)
    {
        // Send Node data message

        var topic = string.IsNullOrEmpty(component.Key) ? $"{basetopic}/NDATA/{deviceid}" : $"{basetopic}/DDATA/{deviceid}/{component.Key}";
        
        // Create a dictionary of telemetry payload

        var telemetry_json = JsonSerializer.Serialize(telemetry,_jsonoptions);
        var telemetry_dict = JsonSerializer.Deserialize<Dictionary<string, object>>(telemetry_json);
        
        // Create a dictionary of message envelope

        var payload = new MessagePayload()
        { 
            Model = component.Value.dtmi,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Seq = sequencenumber++,
            Metrics = telemetry_dict
        };

        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(json)
            .WithExactlyOnceQoS()
            .WithRetainFlag()
            .Build();

        await mqttClient!.PublishAsync(message, CancellationToken.None); // Since 3.0.5 with CancellationToken

        // Log about it
        _logger.LogDebug(LogEvents.DataMessageSent,"Message: Sent {topic} {message}", topic, json);
    }
#endregion

#region Properties
    /// <summary>
    ///  Send a separate message to update each component's properties
    /// </summary>
    protected override async Task UpdateReportedProperties()
    {
        // Get device properties
        var props = _model.GetProperties();

        // If properties exist
        if (props is not null)
        {
            // We can just send them as a telemetry messages
            // Right now telemetry and props messages are no different
            await SendDataMessageAsync(props, new(string.Empty, _model));
        }

        // Send properties from components

        foreach(var kvp in _model.Components)
        {
            // Obtain readings from this component
            props = kvp.Value.GetProperties();
            if (props is not null)
            {
                // Note that official PnP messages can only come from a single component at a time.
                // This is a weakness that drives up the message count. So, will have to decide later
                // if it's worth keeping this, or scrapping PnP compatibility and collecting them all
                // into a single message.

                // Send them
                await SendDataMessageAsync(props, kvp);
            }
        }
    }
    #endregion
}
