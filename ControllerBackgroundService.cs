using System.Text;
using System.Text.Json;
using System.Xml;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;
using Microsoft.Azure.Devices.Shared;
using Tomlyn;
using BrewHub.Controller.Models;

namespace BrewHub.Controller;

public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    private DeviceClient? iotClient;
    private SecurityProviderSymmetricKey? security;
    private DeviceRegistrationResult? result;

    private MachineryInfo? MachineryInfo;
    private TimeSpan TelemetryPeriod = TimeSpan.FromSeconds(30);

    private const string dtmi = "dtmi:brewhub:controller:still;1";

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation(LogEvents.ExecuteStartOK,"BrewHub Controller Service: Started OK");
            await LoadConfig();
            await ProvisionDevice();
            await OpenConnection();
            while (!stoppingToken.IsCancellationRequested)
            {
                await SendTelemetry();
                await Task.Delay(TelemetryPeriod, stoppingToken);
            }

            if (iotClient is not null)
                await iotClient.CloseAsync();
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation(LogEvents.ExecuteFinished,"BrewHub Controller Service: Stopped");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(LogEvents.ExecuteFailed,"BrewHub Controller Service: Failed {type} {message}", ex.GetType().Name, ex.Message);
        }
    }

    private async Task LoadConfig()
    {
        // Machinery info can OPTIONALLY be supplied via local machine config.
        // Alternately, it can be sent down from the cloud as a desired property

        try
        {
            if (File.Exists("machineryinfo.json"))
            {
                using var stream = File.OpenRead("machineryinfo.json");
                var options = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
                MachineryInfo = await JsonSerializer.DeserializeAsync<MachineryInfo>(stream,options);

                if (MachineryInfo is null)
                    throw new ApplicationException("Unable to load machinery info file");

                _logger.LogDebug(LogEvents.ConfigLoaded,"Config: Loaded for {maker} {model}", 
                    MachineryInfo?.Manufacturer ?? "(null)",
                    MachineryInfo?.Model ?? "(null)"
                    );
            }
            else
            {
                _logger.LogDebug("Config: No machineryinfo.json found. Starting unconfigured.");
            }

            _logger.LogInformation(LogEvents.ConfigOK,"Config: OK {machine}",MachineryInfo?.Model ?? "Empty");
        }
        catch (Exception ex)
        {
            _logger.LogError(LogEvents.ConfigError,"Config: Error {message}", ex.Message);
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

            // For group enrollments, the second parameter must be the derived device key.
            // See the ComputeDerivedSymmetricKeySample for how to generate the derived key.
            // The secondary key could be included, but was left out for the simplicity of this sample.
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
                _logger.LogCritical(LogEvents.ProvisionFailed,"Provisioning: Failed");
                throw new ApplicationException("Failed");
            }

            _logger.LogInformation(LogEvents.ProvisionOK,"Provisioning: OK. Device {id} on Hub {hub}", result.DeviceId, result.AssignedHub);
        }
        catch (Exception ex)
        {
            _logger.LogError(LogEvents.ProvisionError,"Provisioning: Error {message}", ex.Message);
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
                ModelId = dtmi
            };

            iotClient = DeviceClient.Create(result.AssignedHub, auth, TransportType.Mqtt, options);
            _logger.LogInformation(LogEvents.ConnectOK,"Connection: OK. {info}", iotClient.ProductInfo);

            // Attach a callback for updates to the module twin's desired properties.
            await iotClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertiesUpdate, null);

    #if false        
            // Register callback for health check command
            await _moduleClient.SetMethodHandlerAsync(HealthCheckCommand, HealthCheckAsync, null);
    #endif
        }
        catch (Exception ex)
        {
            _logger.LogError(LogEvents.ConnectError,"Connection: Error {message}", ex.Message);
            throw;
        }
    }

    private async Task SendTelemetry()
    {
        if (MachineryInfo is not null && MachineryInfo?.Configuration.Sensors.Count > 0)
        {
            // Consider each connected sensor in the configuration
            int position = 0;
            foreach(var sensor in MachineryInfo.Configuration.Sensors)
            {
                // Note that official PnP messages can only come from a single component at a time.
                // This is a weakness that drives up the message count. So, will have to decide later
                // if it's worth keeping this, or scrapping PnP compatibility and collecting them all
                // into a single message.

                // Take the reading for that sensor
                Dictionary<string,object> readings = new();
                var fakereading = DateTime.Now.Minute + sensor.Value * 10;
                readings["temperature"] = fakereading;
                readings["humidity"] = 100 - fakereading;

                // Make a message out of it
                var component = $"Sensor_{++position}";
                using var message = CreateMessage(readings,component);

                // Send the message
                await iotClient!.SendEventAsync(message);
                var detailslist = readings.Select(x=>$"{x.Key}={x.Value:F1}");
                var details = string.Join(' ',detailslist);
                _logger.LogDebug(LogEvents.TelemetrySentOne,"Telemetry: {component} ({name}) {details}", component, sensor.Key, details);
            }
            _logger.LogInformation(LogEvents.TelemetryOK,"Telemetry: OK {count} messages",position);            
        }
        else
        {
            _logger.LogWarning(LogEvents.TelemetryNoMachinery,"Telemetry: No machinery info found. Nothing sent");
        }
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
    public static Message CreateMessage(IDictionary<string, object> telemetryPairs, string? componentName = default, Encoding? encoding = default)
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

    private async Task OnDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
    {
        try
        {
            _logger.LogDebug(LogEvents.PropertyRequest, "Property change: {request}",desiredProperties.ToJson());

            if (desiredProperties.Contains("TelemetryPeriod"))
            {
                var token = desiredProperties["TelemetryPeriod"];
                var desired = (string)token;

                if (desired is not null)
                {
                    try
                    {
                        // Actually update the property in memory
                        TelemetryPeriod = XmlConvert.ToTimeSpan(desired);
                        _logger.LogInformation(LogEvents.PropertyTelemetryPeriodOK,"Property change: TelemetryPeriod OK. Set to {period}",TelemetryPeriod);

                        // Acknowledge the request back to hub
                        var response = new Dictionary<string,PropertyChangeAck>();
                        var ack = new PropertyChangeAck() 
                        {
                            PropertyValue = desired,
                            AckCode = 200,
                            AckVersion = desiredProperties.Version,
                            AckDescription = "OK"
                        };
                        response.Add("TelemetryPeriod",ack);
                        var json = JsonSerializer.Serialize(response);
                        var responsetc = new TwinCollection(json);
                        await iotClient!.UpdateReportedPropertiesAsync(responsetc);

                        _logger.LogDebug(LogEvents.PropertyTelemetryPeriodResponse, "Property change: TelemetryPeriod responded to server with {response}",json);
                    }
                    catch (FormatException ex)
                    {
                        _logger.LogError(LogEvents.PropertyTelemetryPeriodBadFormat,"Property change: TelemetryPeriod failed to convert {token} to timespan. {message}", desired, ex.Message);
                    }
                }
                else
                {
                    _logger.LogError(LogEvents.PropertyTelemetryPeriodNotString,"Property change: TelemetryPeriod failed to convert {token} to string", desired);
                }
            }
        }
        catch (AggregateException ex)
        {
            foreach (Exception exception in ex.InnerExceptions)
            {
                _logger.LogError(LogEvents.PropertyMultipleFailure, exception, "Property change: Multiple failures");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(LogEvents.PropertySingleFailure,ex,"Property change: Failed");
        }
    }
}
