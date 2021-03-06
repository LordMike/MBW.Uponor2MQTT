using System;
using System.Collections.Generic;
using System.Linq;

namespace MBW.UponorApi
{
    /// <summary>
    /// Thread safe container for controller & thermostats
    /// </summary>
    public class SystemDetailsContainer
    {
        private Container _container;

        public SystemDetailsContainer()
        {
            _container = new Container
            {
                _availableControllers = Array.Empty<int>(),
                _availableThermostats = Array.Empty<int[]>()
            };
        }

        public void Update(int[] controllers, int[][] thermostats, int[] outdoorSensors, HcMode hcMode)
        {
            _container = new Container
            {
                _availableControllers = controllers,
                _availableThermostats = thermostats,
                _availableOutdoorSensors = outdoorSensors,
                HcMode = hcMode
            };
        }

        public ICollection<int> GetAvailableControllers()
        {
            return _container._availableControllers;
        }

        public IEnumerable<(int controller, int thermostat)> GetAvailableThermostats()
        {
            return _container._availableThermostats.SelectMany((x, idx) => x == null ? Array.Empty<(int, int)>() : x.Select(s => (idx, s)));
        }

        public ICollection<int> GetAvailableOutdoorSensors()
        {
            return _container._availableOutdoorSensors;
        }

        public HcMode HcMode => _container.HcMode;

        private class Container
        {
            public int[] _availableControllers;
            public int[][] _availableThermostats;
            public int[] _availableOutdoorSensors;
            public HcMode HcMode;
        }
    }
}