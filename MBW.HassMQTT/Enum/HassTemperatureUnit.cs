using System.Runtime.Serialization;

namespace MBW.HassMQTT.Enum
{
    public enum HassTemperatureUnit
    {
        None,

        [EnumMember(Value = "C")]
        Celcius,

        [EnumMember(Value = "F")]
        Fahrenheit
    }
}