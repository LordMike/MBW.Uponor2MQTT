using System;
using MBW.HassMQTT;
using MBW.HassMQTT.DiscoveryModels.Enum;
using MBW.HassMQTT.Extensions;
using MBW.HassMQTT.Interfaces;
using MBW.Uponor2MQTT.HASS;
using MBW.Uponor2MQTT.Validation;
using MBW.UponorApi;
using MBW.UponorApi.Enums;
using Microsoft.Extensions.Logging;

namespace MBW.Uponor2MQTT.Features
{
    internal class ThermostatHumidityFeature : FeatureBase
    {
        private readonly ILogger<ThermostatHumidityFeature> _logger;
        private readonly SystemDetailsContainer _systemDetails;

        public ThermostatHumidityFeature(ILogger<ThermostatHumidityFeature> logger, IServiceProvider serviceProvider, SystemDetailsContainer systemDetails) : base(serviceProvider)
        {
            _logger = logger;
            _systemDetails = systemDetails;
        }

        public override void Process(UponorResponseContainer values)
        {
            foreach ((int controller, int thermostat) in _systemDetails.GetAvailableThermostats())
            {
                string deviceId = HassUniqueIdBuilder.GetThermostatDeviceId(controller, thermostat);

                // Humidity
                ISensorContainer sensor = HassMqttManager.GetSensor(deviceId, "humidity");
                MqttStateValueTopic sender = sensor.GetValueSender(HassTopicKind.State);

                if (values.TryGetValue(UponorObjects.Thermostat(UponorThermostats.RhValue, controller, thermostat),
                    UponorProperties.Value, out float floatVal))
                {
                    if (IsValid.Humidity(floatVal))
                        sender.Value = floatVal;
                    else
                        _logger.LogWarning("Received an invalid humidity of {Value} for {Device}", floatVal, deviceId);
                }
            }
        }
    }
}