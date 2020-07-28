using MBW.Uponor2MQTT.UhomeUponor.Objects;

namespace MBW.Uponor2MQTT.UhomeUponor
{
    internal class SystemProperties
    {
        public int[] AvailableControllers { get; set; }

        public int[][] AvailableThermostats { get; set; }

        public UponorWhoiseResponse System { get; set; }

        public HcMode HcMode { get; set; }
    }
}