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
    internal class ControllerFeature : FeatureBase
    {
        private readonly SystemDetailsContainer _systemDetails;

        public ControllerFeature(IServiceProvider serviceProvider, SystemDetailsContainer systemDetails) : base(serviceProvider)
        {
            _systemDetails = systemDetails;
        }

        public override void Process(UponorResponseContainer values)
        {
            // Software versions
            foreach (int controller in _systemDetails.GetAvailableControllers())
            {
                if (!values.TryGetValue(UponorObjects.Controller(UponorController.ControllerSwVersion, controller),
                    UponorProperties.Value, out object val))
                    continue;

                string deviceId = HassUniqueIdBuilder.GetControllerDeviceId(controller);
                ISensorContainer sensor = HassMqttManager.GetSensor(deviceId, "controller");

                MqttStateValueTopic sender = sensor.GetValueSender(HassTopicKind.State);
                MqttAttributesTopic attributes = sensor.GetAttributesSender();

                sender.Value = "discovered";
                attributes.SetAttribute("sw_version", val);
            }
        }
    }
}