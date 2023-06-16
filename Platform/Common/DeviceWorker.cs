
// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved
// Use of this source code is governed by the MIT license (see LICENSE file)

using System.Reflection;

namespace BrewHub.Devices.Platform.Common;

public abstract class DeviceWorker : BackgroundService
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

    #endregion

    #region Fields
    private DateTimeOffset NextPropertyUpdateTime = DateTimeOffset.MinValue;
    private TimeSpan PropertyUpdatePeriod = TimeSpan.FromMinutes(1);
    private readonly TimeSpan TelemetryRetryPeriod = TimeSpan.FromMinutes(1);
    #endregion

    #region Constructor
    public DeviceWorker(ILogger logger, IRootModel model, IConfiguration config, IHostEnvironment hostenv, IHostApplicationLifetime lifetime)
    {
        _logger = logger;
        _model = model;
        _config = config;
        _hostenv = hostenv;
        _lifetime = lifetime;
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

            await ProvisionDevice();
            await OpenConnection();
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
            _logger.LogInformation(LogEvents.ExecuteFinished,"IoTHub Device Worker: Stopped");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(LogEvents.ExecuteFailed,"IoTHub Device Worker: Failed {type} {message}", ex.GetType().Name, ex.Message);
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
    protected virtual Task LoadInitialState()
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

    /// <summary>
    /// Provision this device according to the config supplied in "Provisioning" section
    /// </summary>
    /// <remarks>
    /// Note that the Provisioning section is designed to follow the format of the config.toml
    /// used by Azure IoT Edge. So if you generate a config.toml for an edge device, you can
    /// use it here. Just be sure to add the config.toml to the HostConfiguration during setup.
    /// (See examples for how this is done.)
    /// </remarks>
    /// <exception cref="ApplicationException">Thrown if provisioning fails (critical error)</exception>
    protected abstract Task ProvisionDevice();

    /// <summary>
    /// Open a connection to the destination service where our communication is headed.
    /// </summary>
    /// <exception cref="ApplicationException">Thrown if connection fails (critical error)</exception>
    protected abstract Task OpenConnection();

    #endregion

    #region Commands
    #endregion

    #region Telemetry
    /// <summary>
    /// Send latest telemetry from root and all components
    /// </summary>
    protected abstract Task SendTelemetry();
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
    protected virtual async Task ManageReportedProperties()
    {
        try
        {
            if (DateTimeOffset.Now < NextPropertyUpdateTime)
                return;

            await UpdateReportedProperties();

            _logger.LogInformation(LogEvents.PropertyReportedOK,"Property: Reported OK. Next update after {delay}",PropertyUpdatePeriod);

            // Manage back-off of property updates
            NextPropertyUpdateTime = DateTimeOffset.Now + PropertyUpdatePeriod;
            PropertyUpdatePeriod += PropertyUpdatePeriod;
            TimeSpan oneday = TimeSpan.FromDays(1);
            if (PropertyUpdatePeriod > oneday)
                PropertyUpdatePeriod = oneday;
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

    /// <summary>
    /// Send latest values of properties from root and all components
    /// </summary>
    protected abstract Task UpdateReportedProperties();
    #endregion
}
