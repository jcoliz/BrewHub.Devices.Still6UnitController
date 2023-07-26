// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved
// Use of this source code is governed by the MIT license (see LICENSE file)

namespace BrewHub.Devices.Platform.Common.Logging;

public class LogEvents
{
    // Conventions:
    // xx__: Which major section of the application is it in
    // _x__: System events (described here)
    // x___: Application-specific events (unique to each application)
    // __9_: Critical failures. Unrecoverable errors which will cause application to exit
    // __8_: Errors
    // __7_: Warnings
    // __00: OK. Major section complete
    // ___0: OK. Minor section complete
    // __[1-6][1-9]: Generally should be debug-level messages. Put the big stuff in the OK log

    // 1. Execute
    public const int ExecuteStartOK             = 100;
    public const int ExecuteDeviceInfo          = 101;
    public const int ExecuteDeviceModel         = 102;
    public const int ExecuteFinished            = 110;
    public const int ExecuteFailed              = 199;

    // 2. Config
    public const int ConfigLoaded               = 201;
    public const int ConfigNoExists             = 207;
    public const int ConfigOK                   = 200;
    public const int ConfigFailed               = 299;

    // 3. Provision
    public const int ProvisionConfig            = 301;
    public const int ProvisionInit              = 302;
    public const int ProvisionStatus            = 303;
    public const int ProvisionOK                = 300;
    public const int ProvisionError             = 388;
    public const int ProvisionFailed            = 399;

    // 4. Connect
    public const int ConnectOK                  = 400;
    public const int ConnectAuth                = 401;
    public const int Connecting                 = 402;
    public const int ConnectDisconnectedOK      = 410;
    public const int ConnectDisconnectedError   = 488;
    public const int ConnectFailed              = 499;

    // 5. Telemetry
    public const int TelemetryOK                = 500;
    public const int TelemetrySentOne           = 501;
    public const int TelemetrySentRoot          = 502;
    public const int TelemetryDelayed           = 576;
    public const int TelemetryNotSent           = 577;
    public const int TelemetryNoPeriod          = 578;
    public const int TelemetrySingleError       = 687;
    public const int TelemetryMultipleError     = 688;

    // 6. Commands
    public const int CommandOK                  = 600;
    public const int CommandReceived            = 601;
    public const int CommandSingleError         = 687;
    public const int CommandMultipleErrors      = 688;
 
    // 7. Properties
    public const int PropertyUpdateOK           = 700;
    public const int PropertyRequest            = 701;
    public const int PropertyResponse           = 702;
    public const int PropertyUpdateComponentOK  = 710;
    public const int PropertyReportedOK         = 720;
    public const int PropertyReportedDetail     = 721;
    public const int PropertyReportApplicationError = 783;
    public const int PropertyReportSingleError  = 784;
    public const int PropertyReportMultipleErrors = 785;
    public const int PropertyUpdateError        = 786;
    public const int PropertyUpdateSingleError  = 787;
    public const int PropertyUpdateMultipleErrors = 788;


    public const int MqttDataMessageSent   = 1003;
    public const int MqttDataMessageReady   = 1010;
    public const int MqttConnectingWaiting  = 1402;
    public const int MqttServerNone = 1481;

    public const int MqttNotConnectedTelemetryNotSent = 1071;
    public const int MqttNotConnectedPropertyNotSent = 1072;

}