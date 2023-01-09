using System.Text;
using BrewHub.Controller;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;
using Microsoft.Azure.Devices.Shared;
using Tomlyn;

try
{
    // See https://aka.ms/new-console-template for more information
    Console.WriteLine("BrewHub Controller");

    using var reader = File.OpenText("config.toml");
    var toml = reader.ReadToEnd();

    var config = Toml.ToModel<ConfigModel>(toml);

    var options = new System.Text.Json.JsonSerializerOptions() { WriteIndented = true };
    var json = System.Text.Json.JsonSerializer.Serialize(config,options);

    Console.WriteLine("Config:");
    Console.WriteLine(json);

    Console.WriteLine($"Initializing the device provisioning client...");

    // For group enrollments, the second parameter must be the derived device key.
    // See the ComputeDerivedSymmetricKeySample for how to generate the derived key.
    // The secondary key could be included, but was left out for the simplicity of this sample.
    using var security = new SecurityProviderSymmetricKey(
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

    Console.WriteLine($"Initialized for registration Id {security.GetRegistrationID()}.");

    Console.WriteLine("Registering with the device provisioning service...");
    DeviceRegistrationResult result = await provClient.RegisterAsync();

    Console.WriteLine($"Registration status: {result.Status}.");
    if (result.Status != ProvisioningRegistrationStatusType.Assigned)
    {
        Console.WriteLine($"Registration status did not assign a hub, so exiting this sample.");
        return;
    }

    Console.WriteLine($"Device {result.DeviceId} registered to {result.AssignedHub}.");

    // NOTE we can now store the device registration result to storage, and use it next time
    // to not have to run the above registration flow again

    Console.WriteLine("Creating symmetric key authentication for IoT Hub...");
    IAuthenticationMethod auth = new DeviceAuthenticationWithRegistrySymmetricKey(
        result.DeviceId,
        security.GetPrimaryKey());

    Console.WriteLine($"Testing the provisioned device with IoT Hub...");
    using var iotClient = DeviceClient.Create(result.AssignedHub, auth, TransportType.Mqtt);

    Console.WriteLine("Sending a telemetry message...");
    using var message = new Message(Encoding.UTF8.GetBytes("TestMessage"));
    await iotClient.SendEventAsync(message);

    await iotClient.CloseAsync();
    Console.WriteLine("Finished.");
}
catch (Exception ex)
{
    Console.WriteLine("ERROR: {0}", ex.Message);
}
