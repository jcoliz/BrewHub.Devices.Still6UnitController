// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved
// Use of this source code is governed by the MIT license (see LICENSE file)

namespace BrewHub.Protocol.Mqtt;

public class MessageGenerator
{
    public enum MessageKind { Invalid = 0, Data, Telemetry, ReportedProperties, DesiredProperties, Command };
    private readonly MqttOptions? _options;
    public MessageGenerator(MqttOptions? options)
    {
        _options = options;
    }

    public (string topic, MessagePayload payload) Generate(MessageKind kind, string deviceid, string? componentid, string model, Dictionary<string, object> metrics)
    {
        // Make sure we were configured with options
        if (_options is null)
            throw new ApplicationException("No MQTT options configured");

        // Set the topic
        var iscomponent = !string.IsNullOrEmpty(componentid);
        var mtype = (kind, iscomponent) switch
        {
            (MessageKind.Data, true) => "DDATA",
            (MessageKind.Data, false) => "NDATA",
            (MessageKind.Command, true) => "DCMD",
            (MessageKind.Command, false) => "NCMD",
            _ => throw new NotImplementedException($"Message Kind {kind} is not implemented.")
        };
        var topic = $"{_options.Topic}/{_options.Site}/{mtype}/{deviceid}" + (iscomponent ? $"/{componentid}" : string.Empty);

        // Assemble the message
        var payload = new MessagePayload()
        { 
            Model = model,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Metrics = metrics
        };

        return (topic, payload);
    }
}