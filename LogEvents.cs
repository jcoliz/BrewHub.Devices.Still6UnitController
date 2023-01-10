namespace BrewHub.Controller;

public class LogEvents
{
    // 1. Execute
    public const int ExecuteStartOK     = 100;
    public const int ExecuteFinished    = 101;

    // 2. Provision
    public const int ProvisionConfig    = 201;
    public const int ProvisionInit      = 202;
    public const int ProvisionStatus    = 203;
    public const int ProvisionOK        = 200;
    public const int ProvisionError     = 280;
    public const int ProvisionFailed    = 290;

    // 3. Connect
    public const int ConnectAuth        = 301;
    public const int ConnectOK          = 300;
    public const int ConnectError       = 380;

    // 4. Telemetry
    public const int TelemetryOK        = 400;

    // 5. Command

    // 6. Property
}