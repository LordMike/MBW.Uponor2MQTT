using MBW.HassMQTT.DiscoveryModels;
using MBW.HassMQTT.Mqtt;
using MBW.Uponor2MQTT.HASS;
using MBW.Uponor2MQTT.Service;

namespace MBW.Uponor2MQTT.Helpers
{
    internal static class DiscoveryHelpers
    {
        public static void ApplyAvailabilityInformation(MqttEntitySensorDiscoveryBase discovery, HassTopicBuilder topicBuilder)
        {
            discovery.AvailabilityTopic = topicBuilder.GetSystemTopic("status");;
            discovery.PayloadAvailable = HassAliveAndWillService.OkMessage;
            discovery.PayloadNotAvailable = HassAliveAndWillService.ProblemMessage;
        }
    }
}