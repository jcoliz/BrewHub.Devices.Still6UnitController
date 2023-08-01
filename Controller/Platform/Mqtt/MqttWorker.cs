// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved
// Use of this source code is governed by the MIT license (see LICENSE file)

using BrewHub.Devices.Platform.Common.Workers;
using BrewHub.Devices.Platform.Common.Models;
using BrewHub.Devices.Platform.Common.Logging;
using BrewHub.Protocol.Mqtt;
using MQTTnet;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using MQTTnet.Client.Receiving;
using MQTTnet.Extensions.ManagedClient;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace BrewHub.Devices.Platform.Mqtt;

public class MqttWorker : DeviceWorker
{
#region Injected Fields

    private readonly IRootModel _model;
    private readonly ILogger _logger;
    // Note that we need the entire config, because we have to pass unstructured
    // InitialState properties to the model
    private readonly IConfiguration _config;
    private readonly IHostEnvironment _hostenv;

    private readonly IOptions<MqttOptions> _options;

    #endregion

    #region Fields
    private IManagedMqttClient? mqttClient;
    private MessageGenerator messageGenerator;
    string deviceid = string.Empty;
    string basetopic = string.Empty;
    private readonly JsonSerializerOptions _jsonoptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private readonly Regex _dtmiStrippingRegex = new Regex("^dtmi:(.+)");
    private DateTimeOffset LastTelemetryUpdateTime = DateTimeOffset.MinValue;
    #endregion

    #region Constructor
    public MqttWorker(
        ILoggerFactory logfact, 
        IRootModel model, 
        IConfiguration config,
        IOptions<MqttOptions> options, 
        IHostEnvironment hostenv, 
        IHostApplicationLifetime lifetime
    ): 
        base(logfact,model,config,hostenv,lifetime)
    {
        // For more compact logs, only use the class name itself, NOT fully-qualified class name
        _logger = logfact.CreateLogger(nameof(MqttWorker));
        _model = model;
        _config = config;
        _hostenv = hostenv;
        _options = options;

        messageGenerator = new MessageGenerator(options.Value);
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
            _options.Value.ClientId = _config["Provisioning:deviceid"] ?? System.Net.Dns.GetHostName();

            // If we're running in docker...
            if ( Boolean.Parse(_config["Provisioning:docker"] ?? "false") )
            {
                // We need the synthetic data to have some skew.

                // We will tell the root level component to generate the skew,
                // using the deviceid as the seed.
                _model.SetInitialState(new Dictionary<string,string>() {{ "Skew", _options.Value.ClientId }});
            }

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
    protected async override Task OpenConnection()
    {
        try
        {
            if (_options.Value is null)
                throw new ApplicationException($"Unable to find MQTT connection details in app configuration. Create a config.toml with connection details in the content root ({_hostenv.ContentRootPath}).");

            if ( _options.Value.Server == "none" )
            {
                _logger.LogWarning(LogEvents.MqttServerNone,"Connection: Using 'none' for MQTT server. This is only useful for testing/debugging.");
                return;
            }

            MqttClientOptionsBuilder builder = new MqttClientOptionsBuilder()
                                        .WithClientId(deviceid)
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

            // Listen for all node command messages
            var topic = messageGenerator.GetTopic(MessageGenerator.MessageKind.NodeCommandAll);
            await mqttClient.SubscribeAsync(topic);
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

    private async void OnMessageReceived(MqttApplicationMessageReceivedEventArgs obj)
    {
        try
        {
            var topic = obj.ApplicationMessage.Topic;
            var json = System.Text.Encoding.UTF8.GetString(obj.ApplicationMessage.Payload);
            _logger.LogDebug(LogEvents.PropertyRequest,"Message recieved: {topic} {payload}", topic, json);

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

                    if (string.IsNullOrEmpty(component))
                    {
                        _model.SetProperty(kvp.Key, jsonvalue);
                    }
                    else
                    {
                        _model.Components[component].SetProperty(kvp.Key, jsonvalue);
                    }
                    _logger.LogInformation(LogEvents.PropertyUpdateOK, "Property: OK. Updated {property} to {updated}", fullpropname, kvp.Value);
                }
                catch (Exception ex)
                {
                    _logger.LogError(LogEvents.PropertyUpdateError, ex, "Property: Update error for {property}", fullpropname);
                }
            }

            // Send it to the component

            // Send back the ACK
            // The easiest way to do this right now is just trigger an update with ALL
            // the properties. Someday we will improve this to only send the changed
            // properties, which will make this more efficient
            await UpdateReportedProperties();
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
                if (mqttClient is not null && !mqttClient.IsConnected)
                {
                    // MqttNotConnectedNotSent
                    _logger.LogWarning(LogEvents.MqttNotConnectedTelemetryNotSent,"MQTT: Client not connected. Telemetry not sent.");
                    return;
                }

                // Task 1657: Log a warning when telemetry is taking longer than the telemetry period

                if (LastTelemetryUpdateTime > DateTimeOffset.MinValue)
                {
                    var elapsed = DateTimeOffset.UtcNow - LastTelemetryUpdateTime;
                    if (elapsed > _model.TelemetryPeriod * 3)
                    {
                        _logger.LogWarning(LogEvents.TelemetryDelayed, "Telemetry: Delayed {elapsed} since last send", elapsed);
                    }
                }

                // User Story 1670: Failed telemetry should not stop other telemetry from sending
                try
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
                }
                catch (AggregateException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(LogEvents.TelemetrySingleError,ex,"Telemetry: Error");
                }

                // Send telemetry from components

                foreach(var kvp in _model.Components)
                {
                    try
                    {
                        // Obtain readings from this component
                        var readings = kvp.Value.GetTelemetry();
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
                    catch (AggregateException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(LogEvents.TelemetrySingleError,ex,"Telemetry: Error");
                    }                    
                }

                if (numsent == 0)
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
            _logger.LogError(LogEvents.TelemetrySystemError,ex,"Telemetry: System Error");
        }
        finally
        {
            LastTelemetryUpdateTime = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Send a single node or device data message
    /// </summary>
    /// <remarks>
    /// Can be telemetry or properties or both
    /// </remarks>
    private async Task SendDataMessageAsync(object telemetry, KeyValuePair<string, IComponentModel> component)
    {
        // Create a dictionary of telemetry payload

        var telemetry_json = JsonSerializer.Serialize(telemetry,_jsonoptions);
        var telemetry_dict = JsonSerializer.Deserialize<Dictionary<string, object>>(telemetry_json);

        // Create message

        var (topic, payload) = messageGenerator.Generate(MessageGenerator.MessageKind.Data, null, component.Key, component.Value.dtmi, telemetry_dict!);

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
#endregion

#region Properties
    /// <summary> 
    ///  Send a separate message to update each component's properties
    /// </summary>
    protected override async Task UpdateReportedProperties()
    {
        if (mqttClient is not null && !mqttClient.IsConnected)
        {
            // MqttNotConnectedNotSent
            _logger.LogWarning(LogEvents.MqttNotConnectedPropertyNotSent,"MQTT: Client not connected. Properties not sent.");

            // TODO: Need a better way to communicate upward that we did not send props, but it's not an error
            // condition. In fact it's not even really worthy of a warning.
            throw new ApplicationException("Not connected");
        }

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
