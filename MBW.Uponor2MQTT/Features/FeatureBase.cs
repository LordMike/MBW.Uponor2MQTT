using System;
using MBW.HassMQTT;
using MBW.Uponor2MQTT.HASS;
using MBW.UponorApi;
using Microsoft.Extensions.DependencyInjection;

namespace MBW.Uponor2MQTT.Features
{
    internal abstract class FeatureBase
    {
        protected HassMqttManager HassMqttManager { get; }

        protected HassUniqueIdBuilder IdBuilder { get; }

        public FeatureBase(IServiceProvider serviceProvider)
        {
            HassMqttManager = serviceProvider.GetRequiredService<HassMqttManager>();
            IdBuilder = serviceProvider.GetRequiredService<HassUniqueIdBuilder>();
        }

        public abstract void Process(UponorResponseContainer values);
    }
}