namespace MBW.Uponor2MQTT.HASS
{
    internal static class HassUniqueIdBuilder
    {
        public static string GetUhomeDeviceId()
        {
            return "uponor_uhome";
        }

        public static string GetControllerDeviceId(int controller)
        {
            return $"uponor_c{controller}";
        }

        public static string GetThermostatDeviceId(int controller, int thermostat)
        {
            return $"uponor_c{controller}_t{thermostat }";
        }
    }
}