using System.Text;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;
using Microsoft.Azure.Devices.Shared;
using Tomlyn;

namespace BrewHub.Controller;

public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    private DeviceClient? iotClient;
    private SecurityProviderSymmetricKey? security;
    private DeviceRegistrationResult? result;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ProvisionDevice();
        await OpenConnection();
        while (!stoppingToken.IsCancellationRequested)
        {
            await SendTelemetry();
            await Task.Delay(30_000, stoppingToken);
        }

        if (iotClient is not null)
            await iotClient.CloseAsync();

        _logger.LogInformation("Finished");
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

            _logger.LogInformation($"Initializing the device provisioning client");

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

            _logger.LogInformation("Initialized for registration Id {id}", security.GetRegistrationID());

            _logger.LogInformation("Registering with the device provisioning service");
            result = await provClient.RegisterAsync();

            _logger.LogInformation("Registration status {status}", result.Status);
            if (result.Status != ProvisioningRegistrationStatusType.Assigned)
            {
                _logger.LogCritical($"Registration status did not assign a hub");
                return;
            }

            _logger.LogInformation("Device {id} registered to {hub}", result.DeviceId, result.AssignedHub);
        }
        catch (Exception ex)
        {
            _logger.LogError("ERROR {message}", ex.Message);
        }
    }

    private Task OpenConnection()
    {
        try
        {
            // NOTE we can now store the device registration result to storage, and use it next time
            // to not have to run the above registration flow again

            _logger.LogInformation("Creating symmetric key authentication for IoT Hub");
            IAuthenticationMethod auth = new DeviceAuthenticationWithRegistrySymmetricKey(
                result!.DeviceId,
                security!.GetPrimaryKey());

            _logger.LogInformation("Connecting to IoT Hub");
            iotClient = DeviceClient.Create(result.AssignedHub, auth, TransportType.Mqtt);

    #if false        
            // Attach a callback for updates to the module twin's desired properties.
            await _moduleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertiesUpdate, null);

            // Register callback for health check command
            await _moduleClient.SetMethodHandlerAsync(HealthCheckCommand, HealthCheckAsync, null);
    #endif
        }
        catch (Exception ex)
        {
            _logger.LogError("ERROR {message}", ex.Message);
        }

        return Task.CompletedTask;
    }

    private async Task SendTelemetry()
    {
        var text = "TestMessage";
        using var message = new Message(Encoding.UTF8.GetBytes(text));
        await iotClient!.SendEventAsync(message);
        _logger.LogInformation("Sent message {text}", text);
    }
}
