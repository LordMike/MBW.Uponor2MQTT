using Newtonsoft.Json;

namespace MBW.UponorApi.Objects
{
    internal class UponorRequest
    {
        [JsonProperty("jsonrpc")]
        public string JsonRPC { get; set; } = "2.0";

        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("method")]
        public string Method { get; set; } = "read";

        [JsonProperty("params")]
        public UponorParams Params { get; set; } = new UponorParams();
    }
}