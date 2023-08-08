// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved
// Use of this source code is governed by the MIT license (see LICENSE file)

using BrewHub.Devices.Platform.Common.Logging;
using BrewHub.Devices.Platform.Common.Models;
using BrewHub.Devices.Platform.Common.Providers;
using System.Reflection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace BrewHub.Devices.Platform.Common.Workers;

/// <summary>
/// Common device worker for connecting to an IoT Service
/// </summary>
/// <remarks>
/// Override with specific connection and transmission details
/// </remarks>
public class DeviceWorker : BackgroundService
{
#region Injected Fields

    private readonly IRootModel _model;

    // We will log events on behalf of the parent
    private readonly ILogger _logger;
    // Note that we need the entire config, because we have to pass unstructured
    // InitialState properties to the model
    private readonly IConfiguration _config;
    private readonly IHostEnvironment _hostenv;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ITransportProvider _transport;

    #endregion

    #region Fields
    private DateTimeOffset NextPropertyUpdateTime = DateTimeOffset.MinValue;
    private TimeSpan PropertyUpdatePeriod = TimeSpan.FromMinutes(1);
    protected readonly TimeSpan TelemetryRetryPeriod = TimeSpan.FromMinutes(1);
    private readonly TimeSpan MaxPropertyUpdateInterval = TimeSpan.FromMinutes(5);
    private DateTimeOffset LastTelemetryUpdateTime = DateTimeOffset.MinValue;
    #endregion

    #region Constructor
    public DeviceWorker(
        ILoggerFactory logfact, 
        IRootModel model, 
        IConfiguration config, 
        IHostEnvironment hostenv, 
        IHostApplicationLifetime lifetime,
        ITransportProvider transport
    )
    {
        // For more compact logs, only use the class name itself, NOT fully-qualified class name
        _logger = logfact.CreateLogger(nameof(DeviceWorker));
        _model = model;
        _config = config;
        _hostenv = hostenv;
        _lifetime = lifetime;
        _transport = transport;

        transport.PropertyReceived += PropertyReceivedHandler;
    }
#endregion

#region Execute
    /// <summary>
    /// Do the work of this worker
    /// </summary>
    /// <remarks>
    /// Generally not needed to override this
    /// </remarks>

    /// <param name="stoppingToken">Cancellation to indicate it's time stop</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation(LogEvents.ExecuteStartOK,"Started OK");

            await LoadInitialState();

            _logger.LogInformation(LogEvents.ExecuteDeviceInfo,"Device: {device}", _model);
            _logger.LogInformation(LogEvents.ExecuteDeviceModel,"Model: {dtmi}", _model.dtmi);

            await _transport.ConnectAsync();
            //await ProvisionDevice();
            //await OpenConnection();

            while (!stoppingToken.IsCancellationRequested)
            {
                await SendTelemetry();
                await ManageReportedProperties();
                await Task.Delay(_model.TelemetryPeriod > TimeSpan.Zero ? _model.TelemetryPeriod : TelemetryRetryPeriod, stoppingToken);
            }

            // TODO: Need handle this in mqtt worker
            /*
            if (mqttClient is not null)
            {
                mqttClient.Dispose();
            }
            */
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation(LogEvents.ExecuteFinished,"Execute: Stopped");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(LogEvents.ExecuteFailed,"Execute: Failed {type} {message}", ex.GetType().Name, ex.Message);
        }

        await Task.Delay(500);
        _lifetime.StopApplication();
    }
#endregion

#region Startup
    /// <summary>
    /// Loads initial state of components out of config "InitialState" section
    /// </summary>
    /// <remarks>
    /// Generally not needed to override this
    /// </remarks>
    /// <returns></returns>
    protected Task LoadInitialState()
    {
        try
        {
            var initialstate = _config.GetSection("InitialState");
            if (initialstate.Exists())
            {
                int numkeys = 0;
                var root = initialstate.GetSection("Root");
                if (root.Exists())
                {
                    var dictionary = root.GetChildren().ToDictionary(x => x.Key, x => x.Value ?? string.Empty);
                    _model.SetInitialState(dictionary);
                    numkeys += dictionary.Keys.Count;
                }

                var components = initialstate.GetSection("Components");
                if (components.Exists())
                {
                    var value = String.Join(',',components.GetChildren().Select(x => $"{x.Key}={x.Value}"));
                    _model.SetInitialState(new Dictionary<string,string>() { { "Components", value }});
                    numkeys += 1;
                }

                foreach(var component in _model.Components)
                {
                    var section = initialstate.GetSection(component.Key);
                    if (section.Exists())
                    {
                        var dictionary = section.GetChildren().ToDictionary(x => x.Key, x => x.Value ?? string.Empty);
                        component.Value.SetInitialState(dictionary);
                        numkeys += dictionary.Keys.Count;
                    }
                }

                _logger.LogInformation(LogEvents.ConfigOK,"Initial State: OK Applied {numkeys} keys",numkeys);
            }
            else
            {
                _logger.LogWarning(LogEvents.ConfigNoExists,"Initial State: Not specified");
            }

            // We allow for the build system to inject a resource named "version.txt"
            // which contains the software build version. If it's not here, we'll just
            // continue with no version information.
            var assembly = Assembly.GetAssembly(_model.GetType());
            var resource = assembly!.GetManifestResourceNames().Where(x => x.EndsWith(".version.txt")).SingleOrDefault();
            if (resource is not null)
            {
                using var stream = assembly.GetManifestResourceStream(resource);
                using var streamreader = new StreamReader(stream!);
                var version = streamreader.ReadLine();

                // Where to store the software build version is solution-dependent.
                // Thus, we will pass it in as a root-level "Version" initial state, and let the
                // solution decide what to do with it.
                _model.SetInitialState(new Dictionary<string,string>() {{ "Version", version! }});
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(LogEvents.ConfigFailed,"Initial State: Failed {message}", ex.Message);
            throw;
        }

        return Task.CompletedTask;
    }
    #endregion

    #region Commands
    #endregion

    #region Telemetry
    /// <summary>
    /// Send latest telemetry from root and all components
    /// </summary>
    protected async Task SendTelemetry()
    {
        try
        {
            int numsent = 0;

            // Send telemetry from root

            if (_model.TelemetryPeriod > TimeSpan.Zero)
            {
                if (!_transport.IsConnected)
                {
                    _logger.LogWarning(LogEvents.MqttNotConnectedTelemetryNotSent,"Telemetry: Client not connected. Telemetry not sent.");
                    return;
                }

                // Task 1657: Log a warning when telemetry is taking longer than the telemetry period

                if (LastTelemetryUpdateTime > DateTimeOffset.MinValue)
                {
                    var elapsed = DateTimeOffset.UtcNow - LastTelemetryUpdateTime;
                    if (elapsed > _model.TelemetryPeriod * 3)
                    {
                        _logger.LogWarning(LogEvents.TelemetryDelayed, "Telemetry: Delayed {elapsed} since last send", elapsed);
                    }
                }

                // User Story 1670: Failed telemetry should not stop other telemetry from sending
                try
                {
                    // Obtain readings from the root
                    var readings = _model.GetTelemetry();

                    // If telemetry exists
                    if (readings is not null)
                    {
                        // Send them
                        await _transport.SendTelemetryAsync(readings,null,_model.dtmi);
                        ++numsent;
                    }
                }
                catch (AggregateException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(LogEvents.TelemetrySingleError,ex,"Telemetry: Error");
                }

                // Send telemetry from components

                foreach(var kvp in _model.Components)
                {
                    try
                    {
                        // Obtain readings from this component
                        var readings = kvp.Value.GetTelemetry();
                        if (readings is not null)
                        {
                            // Note that official PnP messages can only come from a single component at a time.
                            // This is a weakness that drives up the message count. So, will have to decide later
                            // if it's worth keeping this, or scrapping PnP compatibility and collecting them all
                            // into a single message.

                            // Send them
                            await _transport.SendTelemetryAsync(readings,kvp.Key,kvp.Value.dtmi);
                            ++numsent;
                        }
                    }
                    catch (AggregateException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(LogEvents.TelemetrySingleError,ex,"Telemetry: Error");
                    }                    
                }

                if (numsent == 0)
                    _logger.LogWarning(LogEvents.TelemetryNotSent,"Telemetry: No components had available readings. Nothing sent");
            }
            else
                _logger.LogWarning(LogEvents.TelemetryNoPeriod,"Telemetry: Telemetry period not configured. Nothing sent. Will try again in {period}",TelemetryRetryPeriod);
        }
        catch (AggregateException ex)
        {
            foreach (Exception exception in ex.InnerExceptions)
            {
                _logger.LogError(LogEvents.TelemetryMultipleError, exception, "Telemetry: Multiple Errors");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(LogEvents.TelemetrySystemError,ex,"Telemetry: System Error");
        }
        finally
        {
            LastTelemetryUpdateTime = DateTimeOffset.UtcNow;
        }
    }
    #endregion

    #region Properties
    /// <summary>
    /// Check if it's time to update reported properties, and
    /// update them if so
    /// </summary>
    /// <remarks>
    /// Generally not needed to override this method. Implement 
    /// UpdateReportedProperties to do the actual update.
    /// </remarks>
    protected async Task ManageReportedProperties()
    {
        try
        {
            if (DateTimeOffset.Now < NextPropertyUpdateTime)
                return;

            if (!_transport.IsConnected)
            {
                _logger.LogWarning(LogEvents.MqttNotConnectedPropertyNotSent,"Properties: Client not connected. Properties not sent.");
                return;
            }

            // Get device properties
            var props = _model.GetProperties();

            // If properties exist
            if (props is not null)
            {
                // We can just send them as a telemetry messages
                // Right now telemetry and props messages are no different
                await _transport.SendPropertiesAsync(props,null,_model.dtmi);
            }

            // Send properties from components

            foreach(var kvp in _model.Components)
            {
                // Obtain readings from this component
                props = kvp.Value.GetProperties();
                if (props is not null)
                {
                    // Note that official PnP messages can only come from a single component at a time.
                    // This is a weakness that drives up the message count. So, will have to decide later
                    // if it's worth keeping this, or scrapping PnP compatibility and collecting them all
                    // into a single message.

                    // Send them
                    await _transport.SendPropertiesAsync(props,kvp.Key,kvp.Value.dtmi);
                }
            }

            _logger.LogInformation(LogEvents.PropertyReportedOK,"Property: Reported OK. Next update after {delay}",PropertyUpdatePeriod);

            // Manage back-off of property updates
            NextPropertyUpdateTime = DateTimeOffset.Now + PropertyUpdatePeriod;
            PropertyUpdatePeriod += PropertyUpdatePeriod;
            if (PropertyUpdatePeriod > MaxPropertyUpdateInterval)
                PropertyUpdatePeriod = MaxPropertyUpdateInterval;
        }
        catch (ApplicationException ex)
        {
            // An application exception is a soft error. Don't need to log the whole exception,
            // just give the message and move on
            _logger.LogError(LogEvents.PropertyReportApplicationError,"Property: Application Error. {message}", ex.Message);
        }
        catch (AggregateException ex)
        {
            foreach (Exception exception in ex.InnerExceptions)
            {
                _logger.LogError(LogEvents.PropertyReportMultipleErrors, exception, "Property: Multiple reporting errors");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(LogEvents.PropertyReportSingleError,ex,"Property: Reporting error");
        }
    }

    private void PropertyReceivedHandler(object? sender, PropertyReceivedEventArgs e)
    {
        object updated;

        if (string.IsNullOrEmpty(e.Component))
        {
            updated = _model.SetProperty(e.PropertyName, e.JsonValue);
        }
        else
        {
            updated = _model.Components[e.Component].SetProperty(e.PropertyName, e.JsonValue);
        }

        _logger.LogInformation(
            LogEvents.PropertyUpdateOK, 
            "Property: OK. Updated {component}/{property} to {updated}", 
            string.IsNullOrEmpty(e.Component) ? "device" : e.Component,
            e.PropertyName,
            updated
        );
    }

    #endregion
}
