using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MBW.HassMQTT;
using MBW.Uponor2MQTT.Configuration;
using MBW.Uponor2MQTT.Features;
using MBW.Uponor2MQTT.UhomeUponor;
using MBW.Uponor2MQTT.UhomeUponor.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet.Client;

namespace MBW.Uponor2MQTT.Service
{
    internal class UponorThermostatsService : BackgroundService
    {
        private readonly ILogger<UponorThermostatsService> _logger;
        private readonly FeatureManager _featureManager;
        private readonly UhomeUponorClient _uponorClient;
        private readonly SensorStore _sensorStore;
        private readonly IMqttClient _mqttClient;
        private readonly SystemDetailsContainer _detailsContainer;
        private readonly UponorConfiguration _config;

        public UponorThermostatsService(
            ILogger<UponorThermostatsService> logger,
            IOptions<UponorConfiguration> config,
            FeatureManager featureManager,
            UhomeUponorClient uponorClient,
            SensorStore sensorStore,
            IMqttClient mqttClient,
            SystemDetailsContainer detailsContainer)
        {
            _logger = logger;
            _featureManager = featureManager;
            _uponorClient = uponorClient;
            _sensorStore = sensorStore;
            _mqttClient = mqttClient;
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
                    await _sensorStore.FlushAll(_mqttClient, stoppingToken);
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
            });

            UponorResponseContainer values = await _uponorClient.ReadValues(objects, new[] { UponorProperties.Value }, stoppingToken);

            _featureManager.Process(values);
        }
    }
}