using MBW.HassMQTT.Mqtt;

namespace MBW.Uponor2MQTT.HASS
{
    internal static class TopicBuilderExtensions
    {
        public static string GetSystemTopic(this HassTopicBuilder topicBuilder, string name)
        {
            // <prefix>/system/<name>
            return topicBuilder.GetServiceTopic("system", name);
        }
    }
}