using System;
using MBW.HassMQTT.Enum;

namespace MBW.HassMQTT.Discovery.Meta
{
    [AttributeUsage(AttributeTargets.Class)]
    public class DeviceTypeAttribute : Attribute
    {
        public HassDeviceType DeviceType { get; }

        public DeviceTypeAttribute(HassDeviceType deviceType)
        {
            DeviceType = deviceType;
        }
    }
}