using System.Collections.Generic;
using MBW.UponorApi;
using Microsoft.Extensions.Logging;

namespace MBW.Uponor2MQTT.Features
{
    internal class FeatureManager
    {
        private readonly ILogger<FeatureManager> _logger;
        private readonly IEnumerable<FeatureBase> _features;

        public FeatureManager(ILogger<FeatureManager> logger, IEnumerable<FeatureBase> features)
        {
            _logger = logger;
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