using Newtonsoft.Json;

namespace MBW.Uponor2MQTT.UhomeUponor.Objects
{
    internal class UponorResponse<TResult>
    {
        [JsonProperty("jsonrpc")]
        public string JsonRPC { get; set; }

        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("result")]
        public TResult Result { get; set; }
    }
}