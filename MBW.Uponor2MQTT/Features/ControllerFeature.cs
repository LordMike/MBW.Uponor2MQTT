﻿using System;
using MBW.HassMQTT;
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

                string deviceId = IdBuilder.GetControllerId(controller);
                MqttValueTopic sensor = HassMqttManager.GetEntityStateValue(deviceId, "controller", "state");
                MqttAttributesTopic attributes = HassMqttManager.GetAttributesValue(deviceId, "controller");

                sensor.Value = "discovered";
                attributes.SetAttribute("sw_version", val);
            }
        }
    }
}