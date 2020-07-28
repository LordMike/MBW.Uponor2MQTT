namespace MBW.Uponor2MQTT.HASS
{
    internal class HassUniqueIdBuilder
    {
        public string GetUhomeId()
        {
            return "uponor_uhome";
        }

        public string GetControllerId(int controller)
        {
            return $"uponor_c{controller}";
        }

        public string GetThermostatId(int controller, int thermostat)
        {
            return $"uponor_c{controller}_t{thermostat }";
        }
    }
}