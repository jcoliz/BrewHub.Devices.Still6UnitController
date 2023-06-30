// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved
// Use of this source code is governed by the MIT license (see LICENSE file)

using System.Text.RegularExpressions;
using BrewHub.Devices.Platform.Common.Models;

namespace BrewHub.Devices.Platform.Common.Comms;

/// <summary>
/// Provides ability for components with the same model tree a pathway
/// to communicate with one another
/// </summary>
public class ComponentCommunicator: IComponentCommunicator
{
    private readonly IRootModel _root;

    public ComponentCommunicator(IRootModel root)
    {
        _root = root;
    }

    /// <summary>
    /// Get the current value of a single metric (telemetry or prop) from
    /// another component
    /// </summary>
    /// <param name="path">
    /// Descriptor of item. Format: [{componentid}.]{metricid}.
    /// Componentid is optional, if missing, we're asking for the root (device)
    /// </param>
    /// <returns>Metric value converted to string</returns>
    public Task<string> GetMetricValueAsync(string path)
    {
        // Break the path out into what we need to know

        var split = path.Split('.');
        if (split.Length > 2 || split.Length < 1)
            throw new ApplicationException($"Unexpected path containing {split.Length} terms (max is 3)");

        var metric = split.Last();
        var componentid = split.Length == 2 ? split.First() : null;

        // Get the component from the root

        IComponentModel component = _root;
        if (componentid is not null)
        {
            if (!_root.Components.ContainsKey(componentid))
                throw new ApplicationException($"Unknown component with id {componentid}");

            component = _root.Components[componentid];
        }

        // Try to get the property value

        var props = component.GetProperties();
        var json = System.Text.Json.JsonSerializer.Serialize(props);
        var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string,object>>(json);
        if (dict!.ContainsKey(metric))
        {
            return Task.FromResult(dict[metric].ToString()!);
        }
        else
        {
            // Not a property, try telemetry
            var telem = component.GetTelemetry();
            json = System.Text.Json.JsonSerializer.Serialize(props);
            dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string,object>>(json);
            if (dict!.ContainsKey(metric))
            {
                return Task.FromResult(dict[metric].ToString()!);
            }
            else
            {
                // Not a property, or telemetry
                throw new ApplicationException($"Metric {metric} not found on component {component}");
            }
        }
    }
}