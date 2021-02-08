using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MBW.HassMQTT;
using MBW.Uponor2MQTT.Features;
using MBW.UponorApi;
using MBW.UponorApi.Configuration;
using MBW.UponorApi.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MBW.Uponor2MQTT.Service
{
    /// <summary>
    /// This service regularly calls in to discover new temperature and humidity values
    /// It does not gather enough information to successfully discover new thermostats. <see cref="UponorDiscoveryService"/> does that.
    /// </summary>
    internal class UponorThermostatsService : BackgroundService
    {
        private readonly ILogger<UponorThermostatsService> _logger;
        private readonly FeatureManager _featureManager;
        private readonly UhomeUponorClient _uponorClient;
        private readonly HassMqttManager _hassMqttManager;
        private readonly SystemDetailsContainer _detailsContainer;
        private readonly UponorConfiguration _config;

        public UponorThermostatsService(
            ILogger<UponorThermostatsService> logger,
            IOptions<UponorConfiguration> config,
            FeatureManager featureManager,
            UhomeUponorClient uponorClient,
            HassMqttManager hassMqttManager,
            SystemDetailsContainer detailsContainer)
        {
            _logger = logger;
            _featureManager = featureManager;
            _uponorClient = uponorClient;
            _hassMqttManager = hassMqttManager;
            _detailsContainer = detailsContainer;
            _config = config.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Update loop
            DateTime lastRun = DateTime.UtcNow;

            while (!stoppingToken.IsCancellationRequested)
            {
                TimeSpan toDelay = _config.UpdateInterval - (DateTime.UtcNow - lastRun);
                if (toDelay > TimeSpan.Zero)
                {
                    try
                    {
                        await Task.Delay(toDelay, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // Do nothing
                        continue;
                    }
                }

                _logger.LogDebug("Beginning thermostats update");

                try
                {
                    await PerformUpdate(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Do nothing
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "An error occurred while performing the thermostats update");
                }

                try
                {
                    await _hassMqttManager.FlushAll(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Do nothing
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "An error occurred while pushing updated values");
                }

                lastRun = DateTime.UtcNow;
            }
        }

        private async Task PerformUpdate(CancellationToken stoppingToken)
        {
            // Update system & controller details
            IEnumerable<int> objects = _detailsContainer.GetAvailableThermostats().SelectMany(c => new[]
                {
                    // Generic
                    UponorObjects.Thermostat(UponorThermostats.RoomName, c.controller, c.thermostat),
                    UponorObjects.Thermostat(UponorThermostats.RoomInDemand, c.controller, c.thermostat),
                    // Humidity
                    UponorObjects.Thermostat(UponorThermostats.RhValue, c.controller, c.thermostat),
                    // Temperature
                    UponorObjects.Thermostat(UponorThermostats.RoomSetpoint, c.controller, c.thermostat),
                    UponorObjects.Thermostat(UponorThermostats.RoomTemperature, c.controller, c.thermostat),
                    // Alarms
                    UponorObjects.Thermostat(UponorThermostats.TamperIndication, c.controller, c.thermostat),
                    UponorObjects.Thermostat(UponorThermostats.BatteryAlarm, c.controller, c.thermostat),
                    UponorObjects.Thermostat(UponorThermostats.RfAlarm, c.controller, c.thermostat),
                    UponorObjects.Thermostat(UponorThermostats.TechnicalAlarm, c.controller, c.thermostat)
                })
                .Concat(_detailsContainer.GetAvailableOutdoorSensors().SelectMany(c => new[]
                {
                    // Outdoor sensor
                    UponorObjects.Controller(UponorController.MeasuredOutdoorTemperature, c),
                }));

            UponorResponseContainer values = await _uponorClient.ReadValues(objects, new[] { UponorProperties.Value }, stoppingToken);

            _featureManager.Process(values);
        }
    }
}