using System;
using MBW.HassMQTT;
using MBW.UponorApi;
using MBW.UponorApi.Enums;

namespace MBW.Uponor2MQTT.Features
{
    internal class UhomeFeature : FeatureBase
    {
        private readonly MqttAttributesTopic attributes;
        private readonly MqttValueTopic state;

        public UhomeFeature(IServiceProvider serviceProvider) : base(serviceProvider)
        {
            string uniqueId = IdBuilder.GetUhomeId();

            attributes = HassMqttManager.GetAttributesValue(uniqueId, "uhome");
            state = HassMqttManager.GetEntityStateValue(uniqueId, "uhome", "state");
        }

        public override void Process(UponorResponseContainer values)
        {
            state.Value = "discovered";

            if (values.TryGetValue(UponorObjects.System(UponorSystem.DeviceObject), UponorProperties.ApplicationVersion, out object val))
                attributes.SetAttribute("application_version", val);

            if (values.TryGetValue(UponorObjects.System(UponorSystem.DeviceObject), UponorProperties.DeviceName, out val))
                attributes.SetAttribute("device_name", val);

            if (values.TryGetValue(UponorObjects.System(UponorSystem.DeviceObject), UponorProperties.DeviceId, out val))
                attributes.SetAttribute("device_id", val);

            if (values.TryGetValue(UponorObjects.System(UponorSystem.DeviceObject), UponorProperties.SerialNumber, out val))
                attributes.SetAttribute("serial_number", val);

            if (values.TryGetValue(UponorObjects.System(UponorSystem.DeviceObject), UponorProperties.ProductName, out val))
                attributes.SetAttribute("product_name", val);

            if (values.TryGetValue(UponorObjects.System(UponorSystem.DeviceObject), UponorProperties.Supplier, out val))
                attributes.SetAttribute("supplier", val);

            if (values.TryGetValue(UponorObjects.System(UponorSystem.DeviceObject), UponorProperties.MacAddress, out val))
                attributes.SetAttribute("macaddress", val);
        }
    }
}