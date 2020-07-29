using System;
using MBW.HassMQTT;
using MBW.HassMQTT.Mqtt;
using MBW.Uponor2MQTT.HASS;
using MBW.Uponor2MQTT.UhomeUponor;
using Microsoft.Extensions.DependencyInjection;

namespace MBW.Uponor2MQTT.Features
{
    internal abstract class FeatureBase
    {
        protected SensorStore SensorStore { get; }

        protected HassTopicBuilder TopicBuilder { get; }

        protected HassUniqueIdBuilder IdBuilder { get; }

        public FeatureBase(IServiceProvider serviceProvider)
        {
            SensorStore = serviceProvider.GetRequiredService<SensorStore>();
            TopicBuilder = serviceProvider.GetRequiredService<HassTopicBuilder>();
            IdBuilder = serviceProvider.GetRequiredService<HassUniqueIdBuilder>();
        }

        public abstract void Process(UponorResponseContainer values);
    }
}