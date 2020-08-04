using System;
using MBW.HassMQTT.DiscoveryModels.Enum;
using MBW.HassMQTT.Extensions;
using MBW.HassMQTT.Interfaces;
using MBW.Uponor2MQTT.HASS;
using MBW.UponorApi;
using MBW.UponorApi.Enums;

namespace MBW.Uponor2MQTT.Features
{
    internal class UhomeFeature : FeatureBase
    {
        public UhomeFeature(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        public override void Process(UponorResponseContainer values)
        {
            string deviceId = HassUniqueIdBuilder.GetUhomeDeviceId();
            if (!HassMqttManager.TryGetSensor(deviceId, "uhome", out ISensorContainer sensor))
                return;

            sensor.SetValue(HassTopicKind.State, "discovered");

            if (values.TryGetValue(UponorObjects.System(UponorSystem.DeviceObject), UponorProperties.ApplicationVersion, out object val))
                sensor.SetAttribute("application_version", val);

            if (values.TryGetValue(UponorObjects.System(UponorSystem.DeviceObject), UponorProperties.DeviceName, out val))
                sensor.SetAttribute("device_name", val);

            if (values.TryGetValue(UponorObjects.System(UponorSystem.DeviceObject), UponorProperties.DeviceId, out val))
                sensor.SetAttribute("device_id", val);

            if (values.TryGetValue(UponorObjects.System(UponorSystem.DeviceObject), UponorProperties.SerialNumber, out val))
                sensor.SetAttribute("serial_number", val);

            if (values.TryGetValue(UponorObjects.System(UponorSystem.DeviceObject), UponorProperties.ProductName, out val))
                sensor.SetAttribute("product_name", val);

            if (values.TryGetValue(UponorObjects.System(UponorSystem.DeviceObject), UponorProperties.Supplier, out val))
                sensor.SetAttribute("supplier", val);

            if (values.TryGetValue(UponorObjects.System(UponorSystem.DeviceObject), UponorProperties.MacAddress, out val))
                sensor.SetAttribute("macaddress", val);
        }
    }
}