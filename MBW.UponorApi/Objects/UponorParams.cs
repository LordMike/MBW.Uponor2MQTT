using System.Collections.Generic;
using Newtonsoft.Json;

namespace MBW.UponorApi.Objects
{
    internal class UponorParams
    {
        [JsonProperty("objects")]
        public List<UponorObject> Objects { get; set; } = new List<UponorObject>();
    }
}