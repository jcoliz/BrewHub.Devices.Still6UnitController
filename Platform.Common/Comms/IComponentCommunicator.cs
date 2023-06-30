// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved
// Use of this source code is governed by the MIT license (see LICENSE file)

namespace BrewHub.Devices.Platform.Common.Comms;

/// <summary>
/// A pathway for components to communicate with one another
/// </summary>
public interface IComponentCommunicator
{
    /// <summary>
    /// Get the current value of a single metric (telemetry or prop) from
    /// another component
    /// </summary>
    /// <param name="path">
    /// Descriptor of item. Format: [{componentid}.][telem.]{metricid}.
    /// Componentid is optional, if missing, we're asking for the root (device)
    /// "telem." indicates this is a telemetry metric, else it's a property
    /// </param>
    /// <returns>Metric value converted to string</returns>
    Task<string> GetMetricValueAsync(string path);
}