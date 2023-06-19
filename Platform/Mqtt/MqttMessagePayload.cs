// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved
// Use of this source code is governed by the MIT license (see LICENSE file)

namespace BrewHub.Devices.Platform.Mqtt;
public record MessagePayload
{
    public long Timestamp { get; init; }
    public int Seq { get; init; }
    public string? Model { get; init; }
    public Dictionary<string, object>? Metrics { get; init; }
}
