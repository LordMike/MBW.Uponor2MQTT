using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MBW.HassMQTT.Discovery.Meta;
using MBW.HassMQTT.Mqtt;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MQTTnet.Client;

namespace MBW.Uponor2MQTT.Features
{
    internal class SensorStore
    {
        private readonly ManualResetEventSlim _lockObject = new ManualResetEventSlim(true);
        private readonly IServiceProvider _serviceProvider;
        private readonly HassTopicBuilder _topicBuilder;
        private readonly ILogger<SensorStore> _logger;
        private readonly Dictionary<string, MqttSensorDiscoveryBase> _discoveryDocuments;
        private readonly Dictionary<string, MqttValueTopic> _values;
        private readonly Dictionary<string, MqttAttributesTopic> _attributes;

        public SensorStore(IServiceProvider serviceProvider, HassTopicBuilder topicBuilder, ILogger<SensorStore> logger)
        {
            _serviceProvider = serviceProvider;
            _topicBuilder = topicBuilder;
            _logger = logger;
            _discoveryDocuments = new Dictionary<string, MqttSensorDiscoveryBase>(StringComparer.OrdinalIgnoreCase);
            _values = new Dictionary<string, MqttValueTopic>(StringComparer.OrdinalIgnoreCase);
            _attributes = new Dictionary<string, MqttAttributesTopic>(StringComparer.OrdinalIgnoreCase);
        }

        public TComponent Configure<TComponent>(string deviceId, string entityId) where TComponent : MqttSensorDiscoveryBase
        {
            string uniqueId = $"{deviceId}_{entityId}";

            if (!_discoveryDocuments.TryGetValue(uniqueId, out MqttSensorDiscoveryBase discoveryDoc))
            {
                string discoveryTopic = _topicBuilder.GetDiscoveryTopic<TComponent>(deviceId, entityId);

                discoveryDoc = ActivatorUtilities.CreateInstance<TComponent>(_serviceProvider, discoveryTopic, uniqueId);
                _discoveryDocuments[uniqueId] = discoveryDoc;
            }

            return (TComponent)discoveryDoc;
        }

        public MqttValueTopic GetStateValue(string topic)
        {
            if (_values.TryGetValue(topic, out MqttValueTopic sensor))
                return sensor;

            return _values[topic] = new MqttValueTopic(topic);
        }

        public MqttAttributesTopic GetAttributesValue(string topic)
        {
            if (_attributes.TryGetValue(topic, out MqttAttributesTopic sensor))
                return sensor;

            return _attributes[topic] = new MqttAttributesTopic(topic);
        }

        public async Task FlushAll(IMqttClient mqttClient, CancellationToken token = default)
        {
            if (!_lockObject.Wait(0))
            {
                _logger.LogDebug("Unable to acquire exclusive flushing lock");
                return;
            }

            try
            {
                int discoveryDocs = 0, values = 0, attributes = 0;

                foreach (MqttSensorDiscoveryBase value in _discoveryDocuments.Values)
                    if (await value.Flush(mqttClient, cancellationToken: token))
                        discoveryDocs++;

                foreach (MqttValueTopic value in _values.Values)
                    if (await value.Flush(mqttClient, cancellationToken: token))
                        values++;

                foreach (MqttAttributesTopic value in _attributes.Values)
                    if (await value.Flush(mqttClient, cancellationToken: token))
                        attributes++;

                if (discoveryDocs > 0 || values > 0 || attributes > 0)
                    _logger.LogInformation("Pushed {discovery} discovery documents, {values} values and {attributes} attribute changes", discoveryDocs, values, attributes);
            }
            finally
            {
                _lockObject.Set();
            }
        }
    }
}