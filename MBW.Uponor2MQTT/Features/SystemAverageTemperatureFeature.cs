using System;
using MBW.HassMQTT;
using MBW.HassMQTT.DiscoveryModels.Enum;
using MBW.HassMQTT.Extensions;
using MBW.HassMQTT.Interfaces;
using MBW.Uponor2MQTT.HASS;
using MBW.UponorApi;
using MBW.UponorApi.Enums;

namespace MBW.Uponor2MQTT.Features
{
    internal class SystemAverageTemperatureFeature : FeatureBase
    {
        public SystemAverageTemperatureFeature(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        public override void Process(UponorResponseContainer values)
        {
            // Average temperature
            if (!values.TryGetValue(UponorObjects.System(UponorSystem.AverageRoomTemperature),
                UponorProperties.Value, out object val))
                return;

            string deviceId = HassUniqueIdBuilder.GetUhomeDeviceId();
            ISensorContainer sensor = HassMqttManager.GetSensor(deviceId, "average_temperature");

            MqttStateValueTopic sender = sensor.GetValueSender(HassTopicKind.State);

            sender.Value = val;
        }
    }
}