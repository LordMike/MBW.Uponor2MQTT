using System;
using System.Collections.Generic;
using System.Linq;
using MBW.Uponor2MQTT.UhomeUponor.Enums;
using Newtonsoft.Json;

namespace MBW.Uponor2MQTT.UhomeUponor
{
    internal class UponorResponseContainer
    {
        [JsonProperty]
        private Dictionary<int, Dictionary<UponorProperties, object>> _values = new Dictionary<int, Dictionary<UponorProperties, object>>();

        public void AddResponse(int @object, UponorProperties property, object value)
        {
            if (!_values.TryGetValue(@object, out Dictionary<UponorProperties, object> values))
                _values[@object] = values = new Dictionary<UponorProperties, object>();

            values[property] = value;
        }

        public void Merge(UponorResponseContainer container)
        {
            foreach ((int @object, UponorProperties property, object value) in container.GetValues())
                AddResponse(@object, property, value);
        }

        public IEnumerable<int> GetObjects()
        {
            return _values.Keys;
        }

        public IEnumerable<UponorProperties> GetProperties(int @object)
        {
            if (!_values.TryGetValue(@object, out Dictionary<UponorProperties, object> values))
                return Array.Empty<UponorProperties>();

            return values.Keys;
        }

        public bool TryGetValue(int @object, UponorProperties property, out object value)
        {
            value = default;
            if (!_values.TryGetValue(@object, out Dictionary<UponorProperties, object> values))
                return false;

            if (!values.TryGetValue(property, out value))
                return false;

            return true;
        }

        public bool TryGetValue<TVal>(int @object, UponorProperties property, out TVal value)
        {
            value = default;
            if (!TryGetValue(@object, property, out object objVal))
                return false;

            value = (TVal)Convert.ChangeType(objVal, typeof(TVal));

            return true;
        }

        public IEnumerable<(UponorProperties property, object value)> GetValues(int @object)
        {
            if (!_values.TryGetValue(@object, out Dictionary<UponorProperties, object> values))
                yield break;

            foreach ((UponorProperties property, object value) in values)
                yield return (property, value);
        }

        public IEnumerable<(int @object, UponorProperties property, object value)> GetValues()
        {
            return _values.SelectMany(x => x.Value.Select(s => (x.Key, s.Key, s.Value)));
        }
    }
}