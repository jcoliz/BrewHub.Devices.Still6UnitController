using BrewHub.Devices.Platform.Common.Comms;

/// <summary>
/// Implements component comms pathway with simple mock overrides
/// </summary>
/// <remarks>
/// TODO: Reimplement with Moqs
/// </remarks>
public class ComponentCommunicatorTestHelper : IComponentCommunicator
{
    public Dictionary<string, string> Metrics { get; } = new();

    public Task<string> GetMetricValueAsync(string path)
    {
        return Task.FromResult(Metrics[path]);
    }

    public void Clear()
    {
        Metrics.Clear();
    }
}
