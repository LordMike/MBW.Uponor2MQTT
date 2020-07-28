using System.Linq;
using System.Text;
using EnumsNET;
using MBW.HassMQTT.Discovery.Meta;
using MBW.HassMQTT.Helpers;

namespace MBW.HassMQTT.Mqtt
{
    public class HassTopicBuilder
    {
        private const char Seperator = '/';
        private readonly string _discoveryPrefix;
        private readonly string _servicePrefix;

        public HassTopicBuilder(HassConfiguration hassOptions)
        {
            _discoveryPrefix = hassOptions.HomeassistantDiscoveryPrefix.TrimEnd('/');
            _servicePrefix = hassOptions.TopicPrefix.TrimEnd('/');
        }

        private string Combine(string prefix, string[] segments)
        {
            StringBuilder sb = new StringBuilder(prefix.Length + segments.Length + segments.Sum(s => s.Length));

            sb.Append(prefix);

            foreach (string segment in segments)
            {
                sb.Append(Seperator);
                sb.Append(segment);
            }

            return sb.ToString();
        }

        public string GetDiscoveryTopic(params string[] segments)
        {
            return Combine(_discoveryPrefix, segments);
        }

        public string GetServiceTopic(params string[] segments)
        {
            return Combine(_servicePrefix, segments);
        }

        public string GetDiscoveryTopic<TEntity>(string deviceId, string entityId) where TEntity : MqttSensorDiscoveryBase
        {
            // homeassistant/<sensor>/<my_device>/<my_entity>/config
            return GetDiscoveryTopic(DiscoveryHelper.GetDeviceType<TEntity>().AsString(EnumFormat.EnumMemberValue), deviceId, entityId, "config");
        }

        public string GetAttributesTopic(string deviceId, string entityId)
        {
            // <prefix>/<my_device>/<my_entity>/attributes
            return GetServiceTopic(deviceId, entityId, "attributes");
        }

        public string GetEntityTopic(string deviceId, string entityId, string kind)
        {
            // <prefix>/<my_device>/<my_entity>/<kind>
            return GetServiceTopic(deviceId, entityId, kind);
        }
    }
}