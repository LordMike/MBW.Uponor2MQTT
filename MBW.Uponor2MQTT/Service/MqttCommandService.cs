using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MBW.HassMQTT.Mqtt;
using MBW.Uponor2MQTT.Commands;
using MBW.Uponor2MQTT.Configuration;
using MBW.Uponor2MQTT.Features;
using MBW.Uponor2MQTT.MQTT;
using MBW.Uponor2MQTT.UhomeUponor;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;

namespace MBW.Uponor2MQTT.Service
{
    internal class MqttCommandService : IHostedService, IMqttMessageReceiver
    {
        private readonly ILogger<MqttCommandService> _logger;
        private readonly FeatureManager _featureManager;
        private readonly UhomeUponorClient _uponorClient;
        private readonly SensorStore _sensorStore;
        private readonly IMqttClient _mqttClient;
        private readonly SystemDetailsContainer _detailsContainer;
        private readonly UponorConfiguration _config;
        private readonly string _topicPrefix;

        private readonly List<(string[] filter, ICommandHandler handler)> _handlers = new List<(string[] filter, ICommandHandler handler)>();

        public MqttCommandService(
            ILogger<MqttCommandService> logger,
            IOptions<UponorConfiguration> config,
            IOptions<HassConfiguration> hassConfig,
            IServiceProvider serviceProvider,
            FeatureManager featureManager,
            UhomeUponorClient uponorClient,
            SensorStore sensorStore,
            IMqttClient mqttClient,
            SystemDetailsContainer detailsContainer,
            IEnumerable<ICommandHandler> handlers)
        {
            _logger = logger;
            _featureManager = featureManager;
            _uponorClient = uponorClient;
            _sensorStore = sensorStore;
            _mqttClient = mqttClient;
            _detailsContainer = detailsContainer;
            _config = config.Value;
            _topicPrefix = hassConfig.Value.TopicPrefix.TrimEnd('/');

            foreach (ICommandHandler handler in handlers)
                _handlers.Add((handler.GetFilter(), handler));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Listen to topics we want to handle
            // Take the filters of each command, and built a topic filter from it
            // Null segments mean "+" (placeholder for one segment)

            foreach ((string[] filter, ICommandHandler _) in _handlers)
            {
                string subscription = $"{_topicPrefix}/{string.Join("/", filter.Select(x => x ?? "+"))}";

                _logger.LogDebug("Subscribing to {filter}", subscription);

                await _mqttClient.SubscribeAsync(subscription);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public async Task ReceiveAsync(MqttApplicationMessage argApplicationMessage, CancellationToken token = default)
        {
            // Skip prefix, split topic
            string[] topicLevels = argApplicationMessage.Topic.Substring(_topicPrefix.Length + 1).Split('/');

            foreach ((string[] filter, ICommandHandler handler) in _handlers)
            {
                if (filter.Length != topicLevels.Length)
                    continue;

                bool wasMatch = true;
                for (int i = 0; i < filter.Length; i++)
                {
                    if (filter[i] == null || filter[i] == topicLevels[i])
                        continue;

                    wasMatch = false;
                    break;
                }

                if (!wasMatch)
                    continue;

                try
                {
                    await handler.Handle(topicLevels, argApplicationMessage, token);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Unable to handle message from {topic} using {handler}", argApplicationMessage.Topic, handler.GetType().FullName);
                }
            }
        }
    }
}