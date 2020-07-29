using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MBW.HassMQTT;
using MBW.Uponor2MQTT.Configuration;
using MBW.Uponor2MQTT.Features;
using MBW.Uponor2MQTT.UhomeUponor;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet.Client;

namespace MBW.Uponor2MQTT.Service
{
    internal class UponorSystemService : BackgroundService
    {
        private readonly ILogger<UponorSystemService> _logger;
        private readonly FeatureManager _featureManager;
        private readonly UhomeUponorClient _uponorClient;
        private readonly SensorStore _sensorStore;
        private readonly IMqttClient _mqttClient;
        private readonly SystemDetailsContainer _detailsContainer;
        private readonly UponorConfiguration _config;

        public UponorSystemService(
            ILogger<UponorSystemService> logger,
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
            DateTime lastRun = DateTime.MinValue;

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

                _logger.LogDebug("Beginning system update");

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
                    _logger.LogError(e, "An error occurred while performing the system update");
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
            ICollection<int> controllers = _detailsContainer.GetAvailableControllers();

            // TODO: Determine system-wide features to query
            //var objects = controllers.SelectMany(c => new[]
            //{
            //    UponorObjects.Controller(UponorController.ControllerSwVersion, c)
            //});

            //var values = await _uponorClient.ReadValues(objects, new[] { UponorProperties.Value }, stoppingToken);
            

            //_featureManager.Process(values);
        }
    }
}