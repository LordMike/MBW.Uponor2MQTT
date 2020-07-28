using System;
using System.Threading;
using System.Threading.Tasks;
using MBW.HassMQTT.Helpers;
using MBW.HassMQTT.Interfaces;
using MQTTnet.Client;
using Newtonsoft.Json.Linq;

namespace MBW.HassMQTT.Mqtt
{
    public class MqttValueTopic : ICanFlush
    {
        private readonly string _topic;
        private bool _dirty;
        private object _value;

        public MqttValueTopic(string topic)
        {
            _topic = topic;
        }

        public void Set(object newValue)
        {
            if (ComparisonHelper.IsSameValue(newValue, _value))
                return;

            // TODO: _logger.Verbose("Setting value {value}, for {topic}", newValue, _topic);

            _value = newValue;
            _dirty = true;
        }

        private static bool TryConvertValue(object val, out string str)
        {
            switch (val)
            {
                case DateTime asDateTime:
                    str = asDateTime.ToString("O");
                    return true;
                case string asString:
                    str = asString;
                    return true;
                default:
                    str = null;
                    return false;
            }
        }

        public async Task<bool> Flush(IMqttClient mqttClient, bool forceFlush = false, CancellationToken cancellationToken = default)
        {
            if (!_dirty && !forceFlush)
                return false;

            if (TryConvertValue(_value, out string str))
                await mqttClient.SendValueAsync(_topic, str, cancellationToken);
            else
                await mqttClient.SendJsonAsync(_topic, JToken.FromObject(_value), cancellationToken);

            _dirty = false;
            return true;
        }
    }
}