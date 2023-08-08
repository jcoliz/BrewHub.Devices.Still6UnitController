// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved
// Use of this source code is governed by the MIT license (see LICENSE file)

namespace BrewHub.Devices.Platform.Common.Providers;

/// <summary>
/// Defines the service which the worker will use to transport metrics
/// upstream toward their final destination
/// </summary>
public interface ITransportProvider
{
    /// <summary>
    /// Provision this device and open a connection to the transport service
    /// </summary>
    Task ConnectAsync();

    /// <summary>
    /// Send telemetry for one component
    /// </summary>
    /// <param name="metrics">Telemetry metrics to send</param>
    /// <param name="component">Name of component, or null for device</param>
    /// <param name="dtmi">Model identifier for the component/device</param>
    /// <returns></returns>
    Task SendTelemetryAsync(object metrics, string? component, string dtmi);

    /// <summary>
    /// Send properties for one component
    /// </summary>
    /// <param name="metrics">Property metrics to send</param>
    /// <param name="component">Name of component, or null for device</param>
    /// <param name="dtmi">Model identifier for the component/device</param>
    /// <returns></returns>
    Task SendPropertiesAsync(object metrics, string? component, string dtmi);

    event EventHandler<PropertyReceivedEventArgs> PropertyReceived;

    /// <summary>
    /// Whether the transport service is connected and accepting metrics
    /// </summary>
    bool IsConnected { get; }
}

public class PropertyReceivedEventArgs : EventArgs
{
    public string? Component { get; init; }
    public string PropertyName { get; init; } = string.Empty;
    public string JsonValue { get; init; } = string.Empty;
}