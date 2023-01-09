using BrewHub.Controller;
using Tomlyn;

// See https://aka.ms/new-console-template for more information
Console.WriteLine("BrewHub Controller");

var model = new ConfigModel();
model.Provisioning = new Provisioning() { Source = "Source", GlobalEndpoint = "GlobalEndpoint", IdScope = "IdScope" };
model.Provisioning.Attestation = new() { Method = "Method", RegistrationId = "RegistrationId" };
model.Provisioning.Attestation.SymmetricKey = new() { Value = "1234" };

var tomlOut = Toml.FromModel(model);
Console.WriteLine(tomlOut);