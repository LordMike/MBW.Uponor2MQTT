using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MBW.HassMQTT.Helpers;
using MBW.HassMQTT.Interfaces;
using MQTTnet.Client;
using Newtonsoft.Json.Linq;

namespace MBW.HassMQTT.Mqtt
{
    public class MqttAttributesTopic : ICanFlush
    {
        private readonly string _topic;
        private bool _dirty;
        private readonly Dictionary<string, object> _attributes;

        public MqttAttributesTopic(string topic)
        {
            _topic = topic;
            _attributes = new Dictionary<string, object>();
        }

        public void SetAttribute(string name, object value)
        {
            if (value == default)
            {
                if (_attributes.Remove(name))
                    _dirty = true;

                return;
            }

            // If EnableReportingUnchangedValues is set, always move forward
            if (_attributes.TryGetValue(name, out object existing) && ComparisonHelper.IsSameValue(existing, value))
                return;

            // TODO: _logger.Verbose("Setting attribute {name} to {value}, for {topic}", name, value, _topic);

            _attributes[name] = value;
            _dirty = true;
        }

        public async Task<bool> Flush(IMqttClient mqttClient, bool forceFlush = false, CancellationToken cancellationToken = default)
        {
            if (!_dirty && !forceFlush)
                return false;

            await mqttClient.SendJsonAsync(_topic, JToken.FromObject(_attributes), cancellationToken);

            _dirty = false;
            return true;
        }
    }
}