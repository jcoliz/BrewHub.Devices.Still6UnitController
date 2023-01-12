namespace BrewHub.Controller;

public class LogEvents
{
    // Conventions:
    // xx__: Which major section of the application is it in
    // __9_: Critical failures. Unrecoverable errors which will cause application to exit
    // __8_: Errors
    // __7_: Warnings
    // __00: OK. Major section complete
    // __[1-6][1-9]: Generally should be debug-level messages. Put the big stuff in the OK log

    // 1. Execute
    public const int ExecuteStartOK     = 100;
    public const int ExecuteFinished    = 101;
    public const int ExecuteFailed      = 190;

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
    public const int TelemetrySentOne   = 501;
    public const int TelemetryNoMachinery = 570;

    // 10. Commands
    public const int Command1OK        = 1100;
    public const int Command1Error     = 1180;
    public const int Command2OK        = 1200;
    public const int Command2Error     = 1280;

    // 20. Properties
    public const int PropertyRequest = 2001;
    public const int PropertyResponse = 2002;
    public const int PropertySingleFailure = 2081;
    public const int PropertyUnknown   = 2071;
    public const int PropertyMultipleFailure = 2082;
    public const int PropertyTelemetryPeriodOK = 2100;
    public const int PropertyTelemetryPeriodResponse = 2101; 
    public const int PropertyTelemetryPeriodReadFailure = 2181;
    public const int PropertyTelemetryPeriodFormatFailure = 2182;
    public const int PropertyMachineryInfoOK = 2200;
    public const int PropertyMachineryInfoResponse = 2201;
    public const int PropertyMachineryInfoParseFailure = 2281;
    public const int PropertyComponentOK = 2300;
}