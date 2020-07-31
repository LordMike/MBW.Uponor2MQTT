using System.Collections.Generic;
using MBW.UponorApi;

namespace MBW.Uponor2MQTT.Features
{
    internal class FeatureManager
    {
        private readonly IEnumerable<FeatureBase> _features;

        public FeatureManager(IEnumerable<FeatureBase> features)
        {
            _features = features;
        }

        public void Process(UponorResponseContainer values)
        {
            foreach (FeatureBase feature in _features)
            {
                feature.Process(values);
            }
        }
    }
}