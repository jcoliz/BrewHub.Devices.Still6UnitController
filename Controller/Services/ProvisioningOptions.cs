// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved
// Use of this source code is governed by the MIT license (see LICENSE file)

namespace BrewHub.Devices.Services;

/// <summary>
/// Options for provisioning the device
/// </summary>
public class ProvisioningOptions
{
    public const string Section = "Provisioning";

    public string? DeviceId { get; set; } = null;
}
