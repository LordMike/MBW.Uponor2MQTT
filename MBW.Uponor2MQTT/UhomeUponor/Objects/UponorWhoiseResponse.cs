using Newtonsoft.Json;

namespace MBW.Uponor2MQTT.UhomeUponor.Objects
{
    public class UponorWhoiseResponse
    {
        [JsonProperty("deviceid")]
        public string DeviceId { get; set; }

        [JsonProperty("devicetype")]
        public string DeviceType { get; set; }

        [JsonProperty("devicename")]
        public string DeviceName { get; set; }

        [JsonProperty("serialnumber")]
        public string SerialNumber { get; set; }

        [JsonProperty("swversion")]
        public string SwVersion { get; set; }

        [JsonProperty("deviceversion")]
        public string DeviceVersion { get; set; }

        [JsonProperty("applicationversion")]
        public string ApplicationVersion { get; set; }

        [JsonProperty("ipaddress")]
        public string IpAddress { get; set; }

        [JsonProperty("macaddress")]
        public string MacAddress { get; set; }

        [JsonProperty("gateway")]
        public string Gateway { get; set; }

        [JsonProperty("netmask")]
        public string Netmask { get; set; }

        [JsonProperty("supplier")]
        public string Supplier { get; set; }

        [JsonProperty("accessip")]
        public string AccessIp { get; set; }
    }
}