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
    internal class ControllerOutdoorSensorFeature : FeatureBase
    {
        private readonly SystemDetailsContainer _systemDetails;

        public ControllerOutdoorSensorFeature(IServiceProvider serviceProvider, SystemDetailsContainer systemDetails) : base(serviceProvider)
        {
            _systemDetails = systemDetails;
        }

        public override void Process(UponorResponseContainer values)
        {
            // Outdoor sensors
            foreach (int controller in _systemDetails.GetAvailableOutdoorSensors())
            {
                if (!values.TryGetValue(UponorObjects.Controller(UponorController.MeasuredOutdoorTemperature, controller),
                    UponorProperties.Value, out object val))
                    continue;

                string deviceId = HassUniqueIdBuilder.GetControllerDeviceId(controller);
                ISensorContainer sensor = HassMqttManager.GetSensor(deviceId, "outdoor_sensor");

                MqttStateValueTopic sender = sensor.GetValueSender(HassTopicKind.State);

                sender.Value = val;
            }
        }
    }
}