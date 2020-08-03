using System;
using MBW.HassMQTT;
using MBW.UponorApi;
using Microsoft.Extensions.DependencyInjection;

namespace MBW.Uponor2MQTT.Features
{
    internal abstract class FeatureBase
    {
        protected HassMqttManager HassMqttManager { get; }

        public FeatureBase(IServiceProvider serviceProvider)
        {
            HassMqttManager = serviceProvider.GetRequiredService<HassMqttManager>();
        }

        public abstract void Process(UponorResponseContainer values);
    }
}