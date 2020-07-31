using MBW.HassMQTT;
using MBW.HassMQTT.Mqtt;

namespace MBW.Uponor2MQTT.HASS
{
    internal static class TopicBuilderExtensions
    {
        public static MqttStateValueTopic GetSystemValue(this HassMqttManager manager, string name)
        {
            // <prefix>/system/<name>
            return manager.GetServiceStateValue("system", name);
        }

        public static string GetSystemTopic(this HassMqttTopicBuilder topicBuilder, string name)
        {
            // <prefix>/system/<name>
            return topicBuilder.GetServiceTopic("system", name);
        }
    }
}