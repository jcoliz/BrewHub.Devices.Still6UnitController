namespace BrewHub.Controller;

public class LogEvents
{
    // 1. Execute
    public const int ExecuteStartOK     = 100;
    public const int ExecuteFinished    = 101;

    // 2. Config
    public const int ConfigLoaded       = 201;
    public const int ConfigOK           = 200;
    public const int ConfigError        = 280;

    // 3. Provision
    public const int ProvisionConfig    = 301;
    public const int ProvisionInit      = 302;
    public const int ProvisionStatus    = 303;
    public const int ProvisionOK        = 300;
    public const int ProvisionError     = 380;
    public const int ProvisionFailed    = 390;

    // 4. Connect
    public const int ConnectAuth        = 401;
    public const int ConnectOK          = 400;
    public const int ConnectError       = 480;

    // 5. Telemetry
    public const int TelemetryOK        = 500;

    // 6. Command

    // 7. Property
}