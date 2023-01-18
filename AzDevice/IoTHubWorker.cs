// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved

using AzDevice.Models;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using Tomlyn;

namespace AzDevice;

public sealed class IoTHubWorker : BackgroundService
{
#region Injected Fields

    private readonly IRootModel model;
    private readonly ILogger<IoTHubWorker> _logger;

#endregion

#region Fields
    private DeviceClient? iotClient;
    private SecurityProviderSymmetricKey? security;
    private DeviceRegistrationResult? result;
#endregion

#region Constructor
    public IoTHubWorker(ILogger<IoTHubWorker> logger, IRootModel inmodel)
    {
        _logger = logger;
        model = inmodel;
    }
#endregion

#region Execute
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation(LogEvents.ExecuteStartOK,"IoTHub Device Worker: Started OK");
            var device = model.DeviceInfo;
            _logger.LogInformation(LogEvents.ExecuteDeviceInfo,"Device: {mfr} {model} {version}", device.Manufacturer, device.DeviceModel, device.SoftwareVersion);

            if (!string.IsNullOrEmpty(model.dtmi))
                _logger.LogInformation(LogEvents.ExecuteDeviceModel,"Model: {model}", model.dtmi);

            await LoadConfig();
            await ProvisionDevice();
            await OpenConnection();
            while (!stoppingToken.IsCancellationRequested)
            {
                await SendTelemetry();
                await Task.Delay(model.TelemetryPeriod, stoppingToken);
            }

            if (iotClient is not null)
                await iotClient.CloseAsync();
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation(LogEvents.ExecuteFinished,"IoTHub Device Worker: Stopped");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(LogEvents.ExecuteFailed,"IoTHub Device Worker: Failed {type} {message}", ex.GetType().Name, ex.Message);
        }
    }
#endregion

#region Startup
    private async Task LoadConfig()
    {
        try
        {
            var status = await model.LoadConfigAsync();

            _logger.LogInformation(LogEvents.ConfigOK,"Config: OK {status}",status);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(LogEvents.ConfigError,"Config: Error {message}", ex.Message);
            throw;
        }
    }

    private async Task ProvisionDevice()
    {
        try
        {
            using var reader = File.OpenText("config.toml");
            var toml = reader.ReadToEnd();
            var config = Toml.ToModel<ConfigModel>(toml);

#if false
            var options = new System.Text.Json.JsonSerializerOptions() { WriteIndented = true };
            var json = System.Text.Json.JsonSerializer.Serialize(config,options);
            Console.WriteLine("Config:");
            Console.WriteLine(json);
#endif

            _logger.LogDebug(LogEvents.ProvisionConfig,"Provisioning: Loaded config");

            security = new SecurityProviderSymmetricKey(
                config!.Provisioning!.Attestation!.RegistrationId,
                config!.Provisioning!.Attestation!.SymmetricKey!.Value,
                null);

            using ProvisioningTransportHandler transportHandler = new ProvisioningTransportHandlerHttp();

            var endpoint = new Uri(config!.Provisioning!.GlobalEndpoint!);
            var provClient = ProvisioningDeviceClient.Create(
                endpoint.Host,
                config.Provisioning.IdScope,
                security,
                transportHandler);

            _logger.LogDebug(LogEvents.ProvisionInit,"Provisioning: Initialized {id}", security.GetRegistrationID());

            result = await provClient.RegisterAsync();

            _logger.LogDebug(LogEvents.ProvisionStatus,"Provisioning: Status {status}", result.Status);
            if (result.Status != ProvisioningRegistrationStatusType.Assigned)
            {
                throw new ApplicationException($"Failed. Status: {result.Status} {result.Substatus}");
            }

            _logger.LogInformation(LogEvents.ProvisionOK,"Provisioning: OK. Device {id} on Hub {hub}", result.DeviceId, result.AssignedHub);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(LogEvents.ProvisionError,"Provisioning: Error {message}", ex.Message);
            throw;
        }
    }

    private async Task OpenConnection()
    {
        try
        {
            // NOTE we can now store the device registration result to storage, and use it next time
            // to not have to run the above registration flow again

            IAuthenticationMethod auth = new DeviceAuthenticationWithRegistrySymmetricKey(
                result!.DeviceId,
                security!.GetPrimaryKey());
            _logger.LogDebug(LogEvents.ConnectAuth,"Connection: Created SK Auth");

            var options = new ClientOptions
            {
                ModelId = model.dtmi
            };

            iotClient = DeviceClient.Create(result.AssignedHub, auth, TransportType.Mqtt, options);
            _logger.LogInformation(LogEvents.ConnectOK,"Connection: OK. {info}", iotClient.ProductInfo);

            // Update the current state of actual properties
            await UpdateReportedProperties();

            // Read the current state of desired properties and set the local values as desired
            var twin = await iotClient.GetTwinAsync().ConfigureAwait(false);
            await OnDesiredPropertiesUpdate(twin.Properties.Desired, this);

            // Attach a callback for updates to the module twin's desired properties.
            await iotClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertiesUpdate, null);

    #if false        
            // Register callback for health check command
            await _moduleClient.SetMethodHandlerAsync(HealthCheckCommand, HealthCheckAsync, null);
    #endif
        }
        catch (Exception ex)
        {
            _logger.LogCritical(LogEvents.ConnectError,"Connection: Error {message}", ex.Message);
            throw;
        }
    }
#endregion

#region Telemetry
    private async Task SendTelemetry()
    {
        // In the current model, the root component doesn't send any telemetry. Would need
        // some updating here if that changes

        int numsent = 0;

        foreach(var kvp in model.Components)
        {
            if (kvp.Value.HasTelemetry)
            {
                // Note that official PnP messages can only come from a single component at a time.
                // This is a weakness that drives up the message count. So, will have to decide later
                // if it's worth keeping this, or scrapping PnP compatibility and collecting them all
                // into a single message.

                // Obtain readings from this component
                var readings = kvp.Value.GetTelemetry();

                // Make a message out of it
                using var message = CreateTelemetryMessage(readings,kvp.Key);

                // Send the message
                await iotClient!.SendEventAsync(message);
                var detailslist = readings.Select(x=>$"{x.Key}={x.Value:F1}");
                var details = string.Join(' ',detailslist);
                _logger.LogDebug(LogEvents.TelemetrySentOne,"Telemetry: {component} {id} {details}", kvp.Key, kvp.Value, details);
                ++numsent;
            }
        }

        if (numsent > 0)
            _logger.LogInformation(LogEvents.TelemetryOK,"Telemetry: OK {count} messages",numsent);            
        else
            _logger.LogWarning(LogEvents.TelemetryNotSent,"Telemetry: No components had available readings. Nothing sent");
    }

    // Below is from https://github.com/Azure/azure-iot-sdk-csharp/blob/1e97d800061aca1ab812ea32d47bac2442c1ed26/iothub/device/samples/solutions/PnpDeviceSamples/PnpConvention/PnpConvention.cs#L40

    /// <summary>
    /// Create a plug and play compatible telemetry message.
    /// </summary>
    /// <param name="componentName">The name of the component in which the telemetry is defined. Can be null for telemetry defined under the root interface.</param>
    /// <param name="telemetryPairs">The unserialized name and value telemetry pairs, as defined in the DTDL interface. Names must be 64 characters or less. For more details see
    /// <see href="https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v2/dtdlv2.md#telemetry"/>.</param>
    /// <param name="encoding">The character encoding to be used when encoding the message body to bytes. This defaults to utf-8.</param>
    /// <returns>A plug and play compatible telemetry message, which can be sent to IoT Hub. The caller must dispose this object when finished.</returns>
    public static Message CreateTelemetryMessage(IDictionary<string, object> telemetryPairs, string? componentName = default, Encoding? encoding = default)
    {
        if (telemetryPairs == null)
        {
            throw new ArgumentNullException(nameof(telemetryPairs));
        }

        Encoding messageEncoding = encoding ?? Encoding.UTF8;
        string payload = JsonSerializer.Serialize(telemetryPairs);
        var message = new Message(messageEncoding.GetBytes(payload))
        {
            ContentEncoding = messageEncoding.WebName,
            ContentType = ContentApplicationJson,
        };

        if (!string.IsNullOrWhiteSpace(componentName))
        {
            message.ComponentName = componentName;
        }

        return message;
    }
    private const string ContentApplicationJson = "application/json";
#endregion

#region Properties
    private async Task OnDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
    {
        try
        {
            _logger.LogDebug(LogEvents.PropertyRequest, "Property: Desired {request}",desiredProperties.ToJson());

            // Consider each kvp in the request
            foreach(KeyValuePair<string, object> prop in desiredProperties)
            {
                var fullpropname = "(unknown)";
                try
                {
                    fullpropname = prop.Key;

                    // Is this 'property' actually one of our components?
                    if (model.Components.ContainsKey(prop.Key))
                    {
                        // In which case, we need to iterate again over the desired property's children
                        var component = model.Components[prop.Key];
                        var jo = prop.Value as JObject;
                        foreach(JProperty child in jo!.Children())
                        {
                            if (child.Name != "__t")
                            {
                                fullpropname = $"{prop.Key}.{child.Name}";

                                // Update the property
                                var updated = component.SetProperty(child.Name,child.Value);
                                _logger.LogInformation(LogEvents.PropertyComponentOK,"Property: Component OK. Updated {property} to {updated}",fullpropname,updated);

                                // Acknowledge the request back to hub
                                await RespondPropertyUpdate(fullpropname,updated,desiredProperties.Version);
                            }
                        }
                    }
                    // Otherwise, treat it as a property of the rool model
                    else
                    {
                        // Update the property
                        var updated = model.SetProperty(prop.Key,prop.Value);
                        _logger.LogInformation(LogEvents.PropertyOK,"Property: OK. Updated {property} to {updated}",fullpropname,updated);

                        // Acknowledge the request back to hub
                        await RespondPropertyUpdate(fullpropname,updated,desiredProperties.Version);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(LogEvents.PropertyUpdateFailure,ex,"Property: Update failed for {property}",fullpropname);
                }
            }
        }
        catch (AggregateException ex)
        {
            foreach (Exception exception in ex.InnerExceptions)
            {
                _logger.LogError(LogEvents.PropertyMultipleFailure, exception, "Property: Multiple update failures");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(LogEvents.PropertySingleFailure,ex,"Property: Update failed");
        }
    }

    private async Task RespondPropertyUpdate(string key, object value, long version)
    {
        // Key is either 'Property' or 'Component.Property'
        var split = key.Split('.');
        string property = split.Last();
        string? component = split.SkipLast(1).FirstOrDefault();

        var patch = new Dictionary<string,object>();
        var ack = new PropertyChangeAck() 
        {
            PropertyValue = value,
            AckCode = HttpStatusCode.OK,
            AckVersion = version,
            AckDescription = "OK"
        };
        patch.Add(property,ack);
        var response = patch;
        
        if (component is not null)
        {
            patch.Add("__t","c");
            
            response = new Dictionary<string,object>()
            {
                { component, patch }
            };
        }

        var json = JsonSerializer.Serialize(response);
        var resulttc = new TwinCollection(json);
        await iotClient!.UpdateReportedPropertiesAsync(resulttc);

        _logger.LogDebug(LogEvents.PropertyResponse, "Property: Responded to server with {response}",json);
    }

    // Single update of all reported properties at once
    private async Task UpdateReportedProperties()
    {
        // For NOW, I am just going to update the "Info" component
        var info = new DeviceInformationModel();
        var update = new Dictionary<string,object>()
        {
            { "Info", info }
        };

        var json = JsonSerializer.Serialize(update);
        var resulttc = new TwinCollection(json);
        await iotClient!.UpdateReportedPropertiesAsync(resulttc);

        _logger.LogDebug(LogEvents.PropertySendActuals,"Property: Updated reported properties as {update}",json);
    }
#endregion

#region Commands
#endregion

}