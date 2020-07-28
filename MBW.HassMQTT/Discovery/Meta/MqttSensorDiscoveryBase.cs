using System.Threading;
using System.Threading.Tasks;
using MBW.HassMQTT.Helpers;
using MBW.HassMQTT.Interfaces;
using MQTTnet.Client;
using Newtonsoft.Json.Linq;

namespace MBW.HassMQTT.Discovery.Meta
{
    /// <summary>
    /// All MQTT discovery types are documented here:
    /// https://www.home-assistant.io/docs/mqtt/discovery/
    /// </summary>
    public abstract class MqttSensorDiscoveryBase : ICanFlush
    {
        private readonly string _topic;
        private readonly JObject _discover;

        public bool Dirty { get; private set; }

        public MqttDeviceDocument Device { get; }

        public string UniqueId
        {
            get => _discover.GetOrDefault<string>("unique_id", null);
            set => _discover.SetIfChanged("unique_id", value, SetDirty);
        }

        public MqttSensorDiscoveryBase(string topic, string uniqueId)
        {
            _topic = topic;
            _discover = new JObject();

            JObject deviceDoc = new JObject();
            Device = new MqttDeviceDocument(deviceDoc, SetDirty);

            _discover["device"] = deviceDoc;

            UniqueId = uniqueId;
        }

        public JObject GetJsonObject(bool clearDirtyFlag)
        {
            Dirty = false;
            return (JObject)_discover.DeepClone();
        }

        protected void SetValue<T>(string name, T value)
        {
            _discover.SetIfChanged(name, value, SetDirty);
        }

        protected T GetValue<T>(string name, T @default)
        {
            return _discover.GetOrDefault(name, @default);
        }

        private void SetDirty()
        {
            // To avoid 200x lambdas
            Dirty = true;
        }

        public async Task<bool> Flush(IMqttClient mqttClient, bool forceFlush = false, CancellationToken cancellationToken = default)
        {
            if (!Dirty && !forceFlush)
                return false;

            // TODO: _logger.Debug("Publishing discovery doc to {topic} for {uniqueId}", _topic, UniqueId);
            await mqttClient.SendJsonAsync(_topic, _discover, cancellationToken);

            return true;
        }
    }
}