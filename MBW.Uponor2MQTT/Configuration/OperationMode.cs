namespace MBW.Uponor2MQTT.Configuration
{
    public enum OperationMode
    {
        /// <summary>
        /// Describe Uponor setup as 'auto', overriding any modes from the system
        /// </summary>
        Normal,

        /// <summary>
        /// Modes will be "off" and "heating" (or "cooling"), will trick HASS into showing thermostats as what they're *currently* doing.
        /// </summary>
        ModeWorkaround
    }
}