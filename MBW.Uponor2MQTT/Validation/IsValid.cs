namespace MBW.Uponor2MQTT.Validation
{
    internal static class IsValid
    {
        public static bool Temperature(float floatVal)
        {
            return -5 <= floatVal && floatVal <= 60;
        }

        public static bool Humidity(in float floatVal)
        {
            return -5 <= floatVal && floatVal <= 105;
        }
    }
}