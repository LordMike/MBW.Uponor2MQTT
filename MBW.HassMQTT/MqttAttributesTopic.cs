using System.Collections.Generic;
using MBW.HassMQTT.DiscoveryModels.Helpers;
using Newtonsoft.Json.Linq;

namespace MBW.HassMQTT
{
    public class MqttAttributesTopic
    {
        public string Topic { get; }
        public bool Dirty { get; private set; }

        private readonly Dictionary<string, object> _attributes;

        public MqttAttributesTopic(string topic)
        {
            Topic = topic;
            _attributes = new Dictionary<string, object>();
        }

        public void SetAttribute(string name, object value)
        {
            if (value == default)
            {
                if (_attributes.Remove(name))
                    Dirty = true;

                return;
            }

            if (_attributes.TryGetValue(name, out var existing) && ComparisonHelper.IsSameValue(existing, value))
                return;

            // TODO: _logger.Verbose("Setting attribute {name} to {value}, for {topic}", name, value, _topic);

            _attributes[name] = JToken.FromObject(value);
            Dirty = true;
        }

        public JObject GetJsonObject(bool resetDirty)
        {
            if (resetDirty)
                Dirty = false;

            return JObject.FromObject(_attributes);
        }
    }
}