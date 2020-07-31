using MBW.UponorApi.Enums;

namespace MBW.UponorApi
{
    public static class UponorObjects
    {
        public static int System(UponorSystem @object)
        {
            // Offset: 0
            return (int)@object;
        }

        public static int Controller(UponorController @object, int controller)
        {
            // Offset: 60 + 500 x c
            return 60 + 500 * (controller - 1) + (int)@object;
        }

        public static int Thermostat(UponorThermostats @object, int controller, int thermostat)
        {
            // Offset: 80 + 500 x c + 40 x t
            // Tip: 500 = 20 (controller objects) + 12x 40 (thermostat objects) => 13 thermostats is impossible
            return 80 + 500 * (controller - 1) + 40 * (thermostat - 1) + (int)@object;
        }
    }
}