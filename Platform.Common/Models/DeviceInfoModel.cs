// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved
// Use of this source code is governed by the MIT license (see LICENSE file)

using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Hardware.Info;

namespace BrewHub.Devices.Platform.Common.Models;

/// <summary>
/// Standardized implementation of "dtmi:azure:DeviceManagement:DeviceInformation;1"
/// </summary>
/// <remarks>
/// Describes the device THIS CODE is currently running on
/// </remarks>

public class DeviceInformationModel: IComponentModel
{
    [JsonPropertyName("__t")]
    public string ComponentID => "c";

    /// <summary>
    /// Company name of the device manufacturer. This could be the same as the name of the original equipment manufacturer (OEM). Ex. Contoso.
    /// </summary>
    [JsonPropertyName("manufacturer")]
    public string? Manufacturer { get; set; }

    /// <summary>
    /// Device model name or ID. Ex. Surface Book 2.
    /// </summary>
    [JsonPropertyName("model")]
    public string? DeviceModel { get; set; }

    /// <summary>
    /// Version of the software on your device. This could be the version of your firmware. Ex. 1.3.45
    /// </summary>
    [JsonPropertyName("swVersion")]
    public string? SoftwareVersion { get; set; }

    /// <summary>
    /// Name of the operating system on the device. Ex. Windows 10 IoT Core.
    /// </summary>
    [JsonPropertyName("osName")]
    public string? OperatingSystemName { get; private set; }

    /// <summary>
    /// Architecture of the processor on the device. Ex. x64 or ARM.
    /// </summary>
    [JsonPropertyName("processorArchitecture")]
    public string? ProcessorArchitecture { get; private set; }

    /// <summary>
    /// Name of the manufacturer of the processor on the device. Ex. Intel.
    /// </summary>
    [JsonPropertyName("processorManufacturer")]
    public string? ProcessorManufacturer { get; private set; }

    /// <summary>
    /// Total available storage on the device in kilobytes. Ex. 2048000 kilobytes.
    /// </summary>
    [JsonPropertyName("totalStorage")]
    public double? AvailableStorageKB { get; private set; }

    /// <summary>
    /// Total available memory on the device in kilobytes. Ex. 256000 kilobytes.
    /// </summary>
    [JsonPropertyName("totalMemory")]
    public double? AvailableMemoryKB { get; private set; }

    // Inhereted classes may find these helpful
    #region Protected Properties
    /// <summary>
    /// Average CPU load across all logical processors
    /// </summary>
    /// <remarks>
    /// Not sent with this interface, but an inhereted interface may want send it
    /// </remarks>
    [JsonIgnore]
    protected double? AveragePercentProcessorTime { get; private set; }

    #endregion

    #region Fields
    static readonly IHardwareInfo hardwareInfo = new HardwareInfo();

    static DateTimeOffset nextHardwareRead = DateTimeOffset.MinValue;
    #endregion

    #region IComponentModel
    string IComponentModel.dtmi => "dtmi:azure:DeviceManagement:DeviceInformation;1";

    Task<object> IComponentModel.DoCommandAsync(string name, string jsonparams)
    {
        throw new NotImplementedException();
    }

    object IComponentModel.GetProperties()
    {
        return this as DeviceInformationModel;
    }

    public object? GetTelemetry()
    {
        var now = DateTimeOffset.UtcNow;
        if (now >= nextHardwareRead)
        {
            // Takes a while to read hardware. So we will only get new values every 30 seconds
            nextHardwareRead = now + TimeSpan.FromSeconds(30);

            // And we do this in the background.
            Task.Run(() => 
            { 
                // TODO: For the first read EVER on a run, we should go ahead and run in the foregorund and wait
                // for it, so we have data up there, rather than having to wait a minute. 
                hardwareInfo.RefreshOperatingSystem();
                hardwareInfo.RefreshMemoryStatus();
                hardwareInfo.RefreshCPUList();
                hardwareInfo.RefreshDriveList();

                OperatingSystemName = hardwareInfo.OperatingSystem.Name + " " + hardwareInfo.OperatingSystem.VersionString;
                AvailableMemoryKB = hardwareInfo.MemoryStatus.AvailablePhysical / 1000;        
                ProcessorManufacturer = string.Join(", ",hardwareInfo.CpuList.Select(x => $"{x.Name.Trim()} [{x.NumberOfCores}c/{x.NumberOfLogicalProcessors}p]"));
                ProcessorArchitecture = string.Join(", ",hardwareInfo.CpuList.Select(x => x.Caption));

                var freespace = hardwareInfo.DriveList.SelectMany(x => x.PartitionList).SelectMany(x => x.VolumeList).Select(x => x.FreeSpace).Aggregate((s, x) => s + x);
                AvailableStorageKB = freespace / 1000Lu;

                AveragePercentProcessorTime = hardwareInfo.CpuList.SelectMany(x => x.CpuCoreList).Select(x => x.PercentProcessorTime).Average(x=>(double)x);
            });
        }

        // We don't actually have any telemetry. Just using this for a CPU slice!
        return null;
    }

    object IComponentModel.SetProperty(string key, string jsonvalue)
    {
        throw new NotImplementedException();
    }
    
    public void SetInitialState(IDictionary<string, string> values)
    {
        if (values.ContainsKey("manufacturer"))
            Manufacturer = values["manufacturer"];
        if (values.ContainsKey("model"))
            DeviceModel = values["model"];
        if (values.ContainsKey("swVersion"))
            SoftwareVersion = values["swVersion"];
    }

    #endregion    
}