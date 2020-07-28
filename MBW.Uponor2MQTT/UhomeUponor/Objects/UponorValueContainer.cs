﻿using Newtonsoft.Json;

namespace MBW.Uponor2MQTT.UhomeUponor.Objects
{
    internal class UponorValueContainer
    {
        public static UponorValueContainer EmptyValueContainer = new UponorValueContainer();

        public UponorValueContainer()
        {

        }

        public UponorValueContainer(object value)
        {
            Value = value;
        }

        [JsonProperty("value", NullValueHandling = NullValueHandling.Ignore)]
        public object Value { get; set; }
    }
}