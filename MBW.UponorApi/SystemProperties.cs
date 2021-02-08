using MBW.UponorApi.Objects;

namespace MBW.UponorApi
{
    public class SystemProperties
    {
        public int[] AvailableControllers { get; set; }

        public int[] AvailableOutdoorSensors { get; set; }

        public int[][] AvailableThermostats { get; set; }

        public UponorWhoiseResponse System { get; set; }

        public HcMode HcMode { get; set; }
    }
}