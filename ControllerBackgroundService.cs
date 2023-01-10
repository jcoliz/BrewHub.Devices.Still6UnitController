using System.Text;
using System.Text.Json;
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

    private MachineryInfo? MachineryInfo;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(LogEvents.ExecuteStartOK,"BrewHub Controller Service: Started OK");
        await LoadConfig();
        await ProvisionDevice();
        await OpenConnection();
        while (!stoppingToken.IsCancellationRequested)
        {
            await SendTelemetry();
            await Task.Delay(30_000, stoppingToken);
        }

        if (iotClient is not null)
            await iotClient.CloseAsync();

        _logger.LogInformation(LogEvents.ExecuteFinished,"BrewHub Controller Service: Stopped");
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

                _logger.LogInformation(LogEvents.ConfigLoaded,"Config: Loaded for {maker} {model}", 
                    MachineryInfo?.Manufacturer ?? "(null)",
                    MachineryInfo?.Model ?? "(null)"
                    );
            }
            else
            {
                _logger.LogInformation("Config: No machineryinfo.json found. Starting unconfigured.");
            }

            _logger.LogInformation(LogEvents.ConfigOK,"Config: OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(LogEvents.ConfigError,"Config: Error {message}", ex.Message);
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

            _logger.LogInformation(LogEvents.ProvisionConfig,"Provisioning: Loaded config");

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

            _logger.LogInformation(LogEvents.ProvisionInit,"Provisioning: Initialized {id}", security.GetRegistrationID());

            result = await provClient.RegisterAsync();

            _logger.LogInformation(LogEvents.ProvisionStatus,"Provisioning: Status {status}", result.Status);
            if (result.Status != ProvisioningRegistrationStatusType.Assigned)
            {
                _logger.LogCritical(LogEvents.ProvisionFailed,"Provisioning: Failed");
                return;
            }

            _logger.LogInformation(LogEvents.ProvisionOK,"Provisioning: OK. Device {id} on Hub {hub}", result.DeviceId, result.AssignedHub);
        }
        catch (Exception ex)
        {
            _logger.LogError(LogEvents.ProvisionError,"Provisioning: Error {message}", ex.Message);
        }
    }

    private Task OpenConnection()
    {
        try
        {
            // NOTE we can now store the device registration result to storage, and use it next time
            // to not have to run the above registration flow again

            IAuthenticationMethod auth = new DeviceAuthenticationWithRegistrySymmetricKey(
                result!.DeviceId,
                security!.GetPrimaryKey());
            _logger.LogInformation(LogEvents.ConnectAuth,"Connection: Created SK Auth");

            iotClient = DeviceClient.Create(result.AssignedHub, auth, TransportType.Mqtt);
            _logger.LogInformation(LogEvents.ConnectOK,"Connection: OK. {info}", iotClient.ProductInfo);

    #if false        
            // Attach a callback for updates to the module twin's desired properties.
            await _moduleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertiesUpdate, null);

            // Register callback for health check command
            await _moduleClient.SetMethodHandlerAsync(HealthCheckCommand, HealthCheckAsync, null);
    #endif
        }
        catch (Exception ex)
        {
            _logger.LogError(LogEvents.ConnectError,"Connection: Error {message}", ex.Message);
        }

        return Task.CompletedTask;
    }

    private async Task SendTelemetry()
    {
        var text = "TestMessage";
        using var message = new Message(Encoding.UTF8.GetBytes(text));
        await iotClient!.SendEventAsync(message);
        _logger.LogInformation(LogEvents.TelemetryOK,"Telemetry: OK. Sent {text}", text);
    }
}
