using System;

namespace MBW.Uponor2MQTT.Configuration
{
    internal class UponorConfiguration
    {
        public Uri Host { get; set; }

        public TimeSpan UpdateInterval { get; set; } = TimeSpan.FromSeconds(30);

        public TimeSpan DiscoveryInterval { get; set; } = TimeSpan.FromHours(1);
    }
}