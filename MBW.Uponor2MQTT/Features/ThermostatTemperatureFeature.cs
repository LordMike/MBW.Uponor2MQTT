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
    internal class ThermostatTemperatureFeature : FeatureBase
    {
        private readonly ILogger<ThermostatTemperatureFeature> _logger;
        private readonly SystemDetailsContainer _systemDetails;

        public ThermostatTemperatureFeature(ILogger<ThermostatTemperatureFeature> logger, IServiceProvider serviceProvider, SystemDetailsContainer systemDetails) : base(serviceProvider)
        {
            _logger = logger;
            _systemDetails = systemDetails;
        }

        public override void Process(UponorResponseContainer values)
        {
            foreach ((int controller, int thermostat) in _systemDetails.GetAvailableThermostats())
            {
                string deviceId = HassUniqueIdBuilder.GetThermostatDeviceId(controller, thermostat);

                // Temperature
                ISensorContainer sensor = HassMqttManager.GetSensor(deviceId, "temperature");
                MqttStateValueTopic sender = sensor.GetValueSender(HassTopicKind.State);

                if (values.TryGetValue(UponorObjects.Thermostat(UponorThermostats.RoomTemperature, controller, thermostat),
                    UponorProperties.Value, out float floatVal))
                {
                    if (IsValid.Temperature(floatVal))
                        sender.Value = floatVal;
                    else
                        _logger.LogWarning("Received an invalid temperature of {Value} for {Device}", floatVal, deviceId);
                }
            }
        }
    }
}