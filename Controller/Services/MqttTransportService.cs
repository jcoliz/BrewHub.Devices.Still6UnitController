// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved
// Use of this source code is governed by the MIT license (see LICENSE file)

using BrewHub.Devices.Platform.Common.Workers;
using BrewHub.Devices.Platform.Common.Models;
using BrewHub.Devices.Platform.Common.Logging;
using BrewHub.Devices.Platform.Common.Providers;
using BrewHub.Protocol.Mqtt;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using MQTTnet.Client.Receiving;
using MQTTnet.Extensions.ManagedClient;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace BrewHub.Devices.Services;

/// <summary>
/// Provides MQTT-based transport service using BrewHub protocol.
/// </summary>
/// <remarks>
/// Note that this is now mis-named. It's not a worker anymore, it's a 'service'.
/// </remarks>

public class MqttTransportService: ITransportProvider
{
#region Injected Fields

    private readonly ILogger _logger;
    // Note that we need the entire config, because we have to pass unstructured
    // InitialState properties to the model
    private readonly IConfiguration _config;
    private readonly IOptions<MqttOptions> _options;

    #endregion

    #region Fields
    private IManagedMqttClient? mqttClient;
    private MessageGenerator messageGenerator;
    private readonly JsonSerializerOptions _jsonoptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    #endregion

    #region Constructor
    public MqttTransportService(
        ILoggerFactory logfact, 
        IConfiguration config,
        IOptions<MqttOptions> options
    ) 
    {
        // For more compact logs, only use the class name itself, NOT fully-qualified class name
        _logger = logfact.CreateLogger(nameof(MqttTransportService));
        _config = config;
        _options = options;

        messageGenerator = new MessageGenerator(options.Value);
    }
#endregion

#region Connection
    public async Task ConnectAsync()
    {
        await ProvisionDevice();
        await OpenConnection();
    }

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
    protected Task ProvisionDevice()
    {
        try
        {
            // TODO: Move "provisioning" config to  IOptions
            _options.Value.ClientId = _config["Provisioning:deviceid"] ?? System.Net.Dns.GetHostName();
            _logger.LogInformation(LogEvents.ProvisionOK,"Provisioning: OK. Device {id}", _options.Value.ClientId);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(LogEvents.ProvisionError,"Provisioning: Error {message}", ex.Message);
            throw;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Open a connection to MQTT broker
    /// </summary>
    /// <exception cref="ApplicationException">Thrown if connection fails (critical error)</exception>
    protected async Task OpenConnection()
    {
        try
        {
            if (_options.Value is null)
                throw new ApplicationException($"Unable to find MQTT connection details in app configuration. Create a config.toml with connection details in the content root.");

            if ( _options.Value.Server == "none" )
            {
                _logger.LogWarning(LogEvents.MqttServerNone,"Connection: Using 'none' for MQTT server. This is only useful for testing/debugging.");
                return;
            }

            MqttClientOptionsBuilder builder = new MqttClientOptionsBuilder()
                                        .WithClientId(_options.Value.ClientId)
                                        .WithTcpServer(_options.Value.Server, _options.Value.Port);

            ManagedMqttClientOptions options = new ManagedMqttClientOptionsBuilder()
                                    .WithAutoReconnectDelay(TimeSpan.FromSeconds(30))
                                    .WithClientOptions(builder.Build())
                                    .Build();

            mqttClient = new MqttFactory().CreateManagedMqttClient();

            mqttClient.ConnectedHandler = new MqttClientConnectedHandlerDelegate(OnConnected);
            mqttClient.DisconnectedHandler = new MqttClientDisconnectedHandlerDelegate(OnDisconnected);            
            mqttClient.ConnectingFailedHandler = new ConnectingFailedHandlerDelegate(OnConnectingFailed);
            mqttClient.ApplicationMessageReceivedHandler = new MqttApplicationMessageReceivedHandlerDelegate(OnMessageReceived);

            await mqttClient.StartAsync(options);

            _logger.LogDebug(LogEvents.Connecting,"Connection: Connecting on {server}:{port}",_options.Value.Server, _options.Value.Port);

            // Wait here until connected.
            // Bug 1609: MQTT on controller should not show warnings when metrics not sent because not connected yet

            var now = DateTimeOffset.Now;
            var timeout = now + TimeSpan.FromMinutes(3);
            while (!mqttClient.IsConnected && DateTimeOffset.Now < timeout)
            {
                _logger.LogDebug(LogEvents.MqttConnectingWaiting,"Connection: Waiting for connection");
                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
            if (!mqttClient.IsConnected)
                throw new ApplicationException("Timeout attempting to connect");

            // Listen for all command messages
            var topic = messageGenerator.GetTopic(MessageGenerator.MessageKind.Command);
            await mqttClient.SubscribeAsync(topic + "/#");
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

    private static readonly Regex componentfromtopic = new Regex("^((.+?)/){4}(?<component>.+)$");

    public bool IsConnected => mqttClient?.IsConnected ?? false;
    #endregion

    #region Messaging
    /// <summary>
    /// Send telemetry for one component
    /// </summary>
    /// <param name="metrics">Telemetry metrics to send</param>
    /// <param name="component">Name of component, or null for device</param>
    /// <param name="dtmi">Model identifier for the component/device</param>
    /// <returns></returns>
    public Task SendTelemetryAsync(object metrics, string? component, string dtmi)
    {
        return SendDataMessageAsync(metrics, MessageGenerator.MessageKind.Telemetry, component, dtmi);
    }

    /// <summary>
    /// Send properties for one component
    /// </summary>
    /// <param name="metrics">Property metrics to send</param>
    /// <param name="component">Name of component, or null for device</param>
    /// <param name="dtmi">Model identifier for the component/device</param>
    /// <returns></returns>
    public Task SendPropertiesAsync(object metrics, string? component, string dtmi)
    {
        return SendDataMessageAsync(metrics, MessageGenerator.MessageKind.ReportedProperties, component, dtmi);
    }

    /// <summary>
    /// Send a single node or device data message
    /// </summary>
    /// <remarks>
    /// Can be telemetry or properties or both
    /// </remarks>
    private async Task SendDataMessageAsync(object metrics, MessageGenerator.MessageKind kind, string? component, string dtmi)
    {
        // Create a dictionary of telemetry payload

        var metrics_json = JsonSerializer.Serialize(metrics,_jsonoptions);
        var metrics_dict = JsonSerializer.Deserialize<Dictionary<string, object>>(metrics_json);

        // Create message

        var (topic, payload) = messageGenerator.Generate(kind, null, component, dtmi, metrics_dict!);

        // Send it

        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        
        if (mqttClient is not null)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(json)
                .WithExactlyOnceQoS()
                .WithRetainFlag()
                .Build();

            await mqttClient!.PublishAsync(message, CancellationToken.None); // Since 3.0.5 with CancellationToken

            // Log about it
            _logger.LogDebug(LogEvents.MqttDataMessageSent,"MQTT: Sent {topic} {message}", topic, json);
        }
        else
        {
            _logger.LogInformation(LogEvents.MqttDataMessageReady,"MQTT: Ready (no server) {topic} {message}", topic, json);
        }
    }

    public event EventHandler<PropertyReceivedEventArgs>? PropertyReceived = null;

    private void OnMessageReceived(MqttApplicationMessageReceivedEventArgs obj)
    {
        try
        {
            var topic = obj.ApplicationMessage.Topic;
            var json = System.Text.Encoding.UTF8.GetString(obj.ApplicationMessage.Payload);
            _logger.LogDebug(LogEvents.PropertyRequest,"Message received: {topic} {payload}", topic, json);

            // Was this sent to a component or to the device?
            var match = componentfromtopic.Match(topic);
            var component = match.Success ? match.Groups["component"].Value : null;

            // Break out the metric and value

            // NOTE: For near-term backward compat, the payload could EITHER be a Mqtt.MessagePayload,
            // OR just a dictionary of string/objects
            Dictionary<string, object> message;
            try
            {
                var payload = JsonSerializer.Deserialize<MessagePayload>(json);
                if (payload is not null)
                    message = payload.Metrics!;
                else
                    message = JsonSerializer.Deserialize<Dictionary<string, object>>(json)!;
            }
            catch
            {
                message = JsonSerializer.Deserialize<Dictionary<string, object>>(json)!;
            }

            foreach (var kvp in message)
            {
                var fullpropname = (string.IsNullOrEmpty(component)) ? kvp.Key : $"{component}/{kvp.Key}";
                try
                {
                    // Models expect a string THAT they will deserialize on their side :P
                    var jsonvalue = JsonSerializer.Serialize(kvp.Value);
                    var jv1 = kvp.Value.ToString();

                    var eventargs = new PropertyReceivedEventArgs()
                    {
                        Component = component,
                        PropertyName = kvp.Key,
                        JsonValue = jsonvalue
                    };

                    PropertyReceived?.Invoke(this, eventargs);
                }
                catch (Exception ex)
                {
                    _logger.LogError(LogEvents.PropertyUpdateError, ex, "Property: Update error for {property}", fullpropname);
                }
            }

            // Send it to the component

            // TODO: Send back the ACK
            // The ability to do this easily went away in the latest refactor. However,
            // that's probably good because the old way was **LAZY**. Instead, we should
            // craft a message here with only the updated property.
            // await UpdateReportedProperties();
        }
        catch (AggregateException ex)
        {
            foreach (Exception exception in ex.InnerExceptions)
            {
                _logger.LogError(LogEvents.PropertyUpdateMultipleErrors, exception, "Property: Multiple update errors");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(LogEvents.PropertyUpdateSingleError,ex,"Property: Update error");
        }
    }


#endregion

}
