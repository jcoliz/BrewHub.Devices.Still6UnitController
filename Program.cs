using BrewHub.Controller;
using Tomlyn;

// See https://aka.ms/new-console-template for more information
Console.WriteLine("BrewHub Controller");

using var reader = File.OpenText("config.toml");
var toml = reader.ReadToEnd();

var config = Toml.ToModel<ConfigModel>(toml);

var options = new System.Text.Json.JsonSerializerOptions() { WriteIndented = true };
var json = System.Text.Json.JsonSerializer.Serialize(config,options);

Console.WriteLine("Config:");
Console.WriteLine(json);