using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MBW.HassMQTT.DiscoveryModels;
using MBW.HassMQTT.Helpers;
using MBW.HassMQTT.Mqtt;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MQTTnet.Client;
using Newtonsoft.Json.Linq;

namespace MBW.HassMQTT
{
    public class HassMqttManager
    {
        private readonly ManualResetEventSlim _lockObject = new ManualResetEventSlim(true);
        private readonly IServiceProvider _serviceProvider;
        private readonly IMqttClient _mqttClient;
        private readonly HassMqttTopicBuilder _topicBuilder;
        private readonly ILogger<HassMqttManager> _logger;
        private readonly Dictionary<string, MqttSensorDiscoveryBase> _discoveryDocuments;
        private readonly Dictionary<string, MqttValueTopic> _values;
        private readonly Dictionary<string, MqttAttributesTopic> _attributes;

        public HassMqttManager(IServiceProvider serviceProvider, IMqttClient mqttClient, HassMqttTopicBuilder topicBuilder, ILogger<HassMqttManager> logger)
        {
            _serviceProvider = serviceProvider;
            _mqttClient = mqttClient;
            _topicBuilder = topicBuilder;
            _logger = logger;
            _discoveryDocuments = new Dictionary<string, MqttSensorDiscoveryBase>(StringComparer.OrdinalIgnoreCase);
            _values = new Dictionary<string, MqttValueTopic>(StringComparer.OrdinalIgnoreCase);
            _attributes = new Dictionary<string, MqttAttributesTopic>(StringComparer.OrdinalIgnoreCase);
        }

        public TComponent ConfigureDiscovery<TComponent>(string deviceId, string entityId) where TComponent : MqttSensorDiscoveryBase
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

        public MqttAttributesTopic GetAttributesValue(string deviceId, string entityId)
        {
            string topic = _topicBuilder.GetAttributesTopic(deviceId, entityId);

            if (_attributes.TryGetValue(topic, out MqttAttributesTopic sensor))
                return sensor;

            return _attributes[topic] = new MqttAttributesTopic(topic);
        }
        
        public MqttValueTopic GetServiceStateValue(string deviceId, string entityId)
        {
            string topic = _topicBuilder.GetServiceTopic(deviceId, entityId);

            if (_values.TryGetValue(topic, out MqttValueTopic sensor))
                return sensor;

            return _values[topic] = new MqttValueTopic(topic);
        }

        public MqttValueTopic GetEntityStateValue(string deviceId, string entityId, string kind)
        {
            string topic = _topicBuilder.GetEntityTopic(deviceId, entityId, kind);

            if (_values.TryGetValue(topic, out MqttValueTopic sensor))
                return sensor;

            return _values[topic] = new MqttValueTopic(topic);
        }

        public MqttValueTopic GetStateValue1(string topic)
        {
            if (_values.TryGetValue(topic, out MqttValueTopic sensor))
                return sensor;

            return _values[topic] = new MqttValueTopic(topic);
        }

        public MqttAttributesTopic GetAttributesValue1(string topic)
        {
            if (_attributes.TryGetValue(topic, out MqttAttributesTopic sensor))
                return sensor;

            return _attributes[topic] = new MqttAttributesTopic(topic);
        }

        public async Task FlushAll(IMqttClient mqttClient1, CancellationToken token = default)
        {
            if (!_lockObject.Wait(0))
            {
                _logger.LogDebug("Unable to acquire exclusive flushing lock");
                return;
            }

            try
            {
                int discoveryDocs = 0, values = 0, attributes = 0;

                foreach (MqttSensorDiscoveryBase value in _discoveryDocuments.Values.Where(s => s.Dirty))
                {
                    await _mqttClient.SendJsonAsync(value.Topic, value.GetJsonObject(true), token);
                    discoveryDocs++;
                }

                foreach (MqttValueTopic value in _values.Values.Where(s => s.Dirty))
                {
                    // Apply cursory conversion to state value
                    object toSend = value.GetValue(true);

                    if (toSend is string str)
                        await _mqttClient.SendValueAsync(value.Topic, str, token);
                    else
                        await _mqttClient.SendJsonAsync(value.Topic, JToken.FromObject(value.Value), token);

                    values++;
                }

                foreach (MqttAttributesTopic value in _attributes.Values.Where(s => s.Dirty))
                {
                    await _mqttClient.SendJsonAsync(value.Topic, value.GetJsonObject(true), token);
                    attributes++;
                }

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