using System.Collections.Generic;
using Newtonsoft.Json;

namespace MBW.Uponor2MQTT.UhomeUponor.Objects
{
    internal class UponorParams
    {
        [JsonProperty("objects")]
        public List<UponorObject> Objects { get; set; } = new List<UponorObject>();
    }
}