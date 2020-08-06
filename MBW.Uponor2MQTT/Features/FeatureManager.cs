using System.Collections.Generic;
using System.Linq;
using MBW.UponorApi;

namespace MBW.Uponor2MQTT.Features
{
    internal class FeatureManager
    {
        private readonly object _lock = new object();
        private readonly List<FeatureBase> _features;

        public FeatureManager(IEnumerable<FeatureBase> features)
        {
            _features = features.ToList();
        }

        public void Process(UponorResponseContainer values)
        {
            lock (_lock)
            {
                foreach (FeatureBase feature in _features)
                {
                    feature.Process(values);
                }
            }
        }
    }
}