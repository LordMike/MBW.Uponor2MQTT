using System;
using MBW.HassMQTT;
using MBW.UponorApi;
using MBW.UponorApi.Enums;

namespace MBW.Uponor2MQTT.Features
{
    internal class ThermostatHumidityFeature : FeatureBase
    {
        private readonly SystemDetailsContainer _systemDetails;

        public ThermostatHumidityFeature(IServiceProvider serviceProvider, SystemDetailsContainer systemDetails) : base(serviceProvider)
        {
            _systemDetails = systemDetails;
        }

        public override void Process(UponorResponseContainer values)
        {
            foreach ((int controller, int thermostat) in _systemDetails.GetAvailableThermostats())
            {
                string deviceId = IdBuilder.GetThermostatId(controller, thermostat);

                // Humidity
                MqttValueTopic sensor = HassMqttManager.GetEntityStateValue(deviceId, "humidity", "state");

                if (values.TryGetValue(UponorObjects.Thermostat(UponorThermostats.RhValue, controller, thermostat),
                    UponorProperties.Value, out object objVal))
                {
                    sensor.Value = objVal;
                }
            }
        }
    }
}