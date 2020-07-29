using System;
using Newtonsoft.Json.Linq;

namespace MBW.HassMQTT.DiscoveryModels.Helpers
{
    internal static class JsonHelper
    {
        public static T GetOrDefault<T>(this JObject obj, string name, T @default)
        {
            if (!obj.TryGetValue(name, out JToken val) || val == null)
                return @default;

            return val.Value<T>();
        }

        public static void SetIfChanged<T>(this JObject obj, string name, T newValue, Action onSet)
        {
            if (Equals(newValue, default(T)))
            {
                // Remove a value
                if (obj.Remove(name))
                    onSet();

                return;
            }

            if (obj.TryGetValue(name, out JToken val) && val != null)
            {
                // Compare
                T existing = val.Value<T>();

                if (!ComparisonHelper.IsSameValue(existing, newValue))
                {
                    obj[name] = JToken.FromObject(newValue);
                    onSet();
                }
            }
            else
            {
                // Not set
                obj[name] = JToken.FromObject(newValue);
                onSet();
            }

            // TODO: _logger.Verbose("Setting attribute {name} to {value}, for {topic}", name, value, _topic);
        }
    }
}