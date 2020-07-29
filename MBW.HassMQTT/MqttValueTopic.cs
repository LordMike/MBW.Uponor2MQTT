using System;
using MBW.HassMQTT.DiscoveryModels.Helpers;

namespace MBW.HassMQTT
{
    public class MqttValueTopic
    {
        private object _value;
        public string Topic { get; }
        public bool Dirty { get; private set; }

        public object Value
        {
            get => _value;
            set
            {
                if (ComparisonHelper.IsSameValue(value, Value))
                    return;

                // TODO: _logger.Verbose("Setting value {value}, for {topic}", newValue, _topic);

                Value = value;
                Dirty = true;
            }
        }

        public MqttValueTopic(string topic)
        {
            Topic = topic;
        }
        
        private static bool TryConvertStateValue(object val, out string str)
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

        public object GetValue(bool resetDirty)
        {
            if (resetDirty)
                Dirty = false;

            if (TryConvertStateValue(Value, out var asString))
                return asString;

            return Value;
        }
    }
}