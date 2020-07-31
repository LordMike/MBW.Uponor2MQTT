using System.Collections.Generic;
using MBW.HassMQTT.DiscoveryModels.Helpers;
using MBW.HassMQTT.Interfaces;

namespace MBW.HassMQTT
{
    public class MqttAttributesTopic : IMqttValueContainer
    {
        public string Topic { get; }
        public bool Dirty { get; private set; }

        private readonly Dictionary<string, object> _attributes;

        public MqttAttributesTopic(string topic)
        {
            Topic = topic;
            _attributes = new Dictionary<string, object>();
        }

        public void RemoveAttribute(string name)
        {
            if (_attributes.Remove(name))
                Dirty = true;
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

            _attributes[name] = value;
            Dirty = true;
        }

        public object GetSerializedValue(bool resetDirty)
        {
            if (resetDirty)
                Dirty = false;

            return _attributes;
        }
    }
}