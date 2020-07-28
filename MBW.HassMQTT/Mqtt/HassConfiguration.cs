namespace MBW.HassMQTT.Mqtt
{
    public class HassConfiguration
    {
        public string HomeassistantDiscoveryPrefix { get; set; } = "homeassistant";

        public string TopicPrefix { get; set; }
    }
}