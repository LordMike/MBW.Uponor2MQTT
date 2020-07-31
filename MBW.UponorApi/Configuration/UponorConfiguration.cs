using System;

namespace MBW.UponorApi.Configuration
{
    public class UponorConfiguration
    {
        public Uri Host { get; set; }

        public TimeSpan UpdateInterval { get; set; } = TimeSpan.FromSeconds(30);

        public TimeSpan DiscoveryInterval { get; set; } = TimeSpan.FromHours(1);
    }
}