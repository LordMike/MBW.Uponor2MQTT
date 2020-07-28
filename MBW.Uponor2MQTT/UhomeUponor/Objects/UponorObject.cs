using System.Collections.Generic;
using Newtonsoft.Json;

namespace MBW.Uponor2MQTT.UhomeUponor.Objects
{
    internal class UponorObject
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("properties")]
        public Dictionary<int, UponorValueContainer> Properties { get; set; } = new Dictionary<int, UponorValueContainer>();
    }
}