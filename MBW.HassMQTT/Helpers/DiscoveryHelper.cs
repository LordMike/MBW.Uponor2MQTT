using System;
using MBW.HassMQTT.Discovery.Meta;
using MBW.HassMQTT.Enum;

namespace MBW.HassMQTT.Helpers
{
    public static class DiscoveryHelper
    {
        public static HassDeviceType GetDeviceType<T>() where T : MqttSensorDiscoveryBase
        {
            Attribute attribute = Attribute.GetCustomAttribute(typeof(T), typeof(DeviceTypeAttribute));

            if (attribute == null)
                throw new Exception($"Unable to locate [DeviceType()] attribute on {typeof(T).FullName}");

            return ((DeviceTypeAttribute) attribute).DeviceType;
        }

        public static HassDeviceType GetDeviceType<T>(this T entity) where T : MqttSensorDiscoveryBase
        {
            return GetDeviceType<T>();
        }
    }
}