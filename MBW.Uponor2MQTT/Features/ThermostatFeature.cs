using System;
using MBW.HassMQTT.DiscoveryModels.Enum;
using MBW.HassMQTT.Extensions;
using MBW.HassMQTT.Interfaces;
using MBW.Uponor2MQTT.Configuration;
using MBW.Uponor2MQTT.HASS;
using MBW.Uponor2MQTT.Validation;
using MBW.UponorApi;
using MBW.UponorApi.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MBW.Uponor2MQTT.Features
{
    internal class ThermostatFeature : FeatureBase
    {
        private readonly UponorOperationConfiguration _operationConfig;
        private readonly ILogger<ThermostatFeature> _logger;
        private readonly SystemDetailsContainer _systemDetails;

        public ThermostatFeature(ILogger<ThermostatFeature> logger, IServiceProvider serviceProvider, IOptions<UponorOperationConfiguration> operationConfig, SystemDetailsContainer systemDetails) : base(serviceProvider)
        {
            _operationConfig = operationConfig.Value;
            _logger = logger;
            _systemDetails = systemDetails;
        }

        public override void Process(UponorResponseContainer values)
        {
            foreach ((int controller, int thermostat) in _systemDetails.GetAvailableThermostats())
            {
                string deviceId = HassUniqueIdBuilder.GetThermostatDeviceId(controller, thermostat);

                // Temperature
                ISensorContainer sensor = HassMqttManager.GetSensor(deviceId, "temp");
                
                if (values.TryGetValue(
                    UponorObjects.Thermostat(UponorThermostats.RoomTemperature, controller, thermostat),
                    UponorProperties.Value, out float floatVal))
                {
                    if (IsValid.Temperature(floatVal))
                        sensor.SetValue(HassTopicKind.CurrentTemperature, floatVal);
                    else
                        _logger.LogWarning("Received an invalid temperature of {Value} for {Device}", floatVal, deviceId);
                }

                // Setpoint
                if (values.TryGetValue(
                    UponorObjects.Thermostat(UponorThermostats.RoomSetpoint, controller, thermostat),
                    UponorProperties.Value, out floatVal))
                    sensor.SetValue(HassTopicKind.TemperatureState, floatVal);

                // Action & Mode
                if (values.TryGetValue(
                    UponorObjects.Thermostat(UponorThermostats.RoomInDemand, controller, thermostat),
                    UponorProperties.Value, out int intVal))
                {
                    // If this value is >0, the room is heating/cooling (see H/C mode)
                    string action, mode;

                    // Valid values: off, heating, cooling, drying, idle, fan.
                    if (intVal <= 0)
                    {
                        action = "idle";
                        mode = "off";
                    }
                    else if (_systemDetails.HcMode == HcMode.Heating)
                    {
                        action = "heating";
                        mode = "heat";
                    }
                    else
                    {
                        action = "cooling";
                        mode = "cool";
                    }

                    // Override Mode as auto
                    if (_operationConfig.OperationMode == OperationMode.Normal)
                        mode = "auto";

                    sensor.SetValue(HassTopicKind.Action, action);
                    sensor.SetValue(HassTopicKind.ModeState, mode);
                }

                // Home/away
                if (values.TryGetValue(
                    UponorObjects.Thermostat(UponorThermostats.HomeAwayModeStatus, controller, thermostat),
                    UponorProperties.Value, out intVal))
                {
                    if (intVal > 0)
                        // Away
                        sensor.SetValue(HassTopicKind.AwayModeState, "on");
                    else
                        // Home
                        sensor.SetValue(HassTopicKind.AwayModeState, "off");
                }
            }
        }
    }
}