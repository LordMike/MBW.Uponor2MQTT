using System;
using MBW.HassMQTT;
using MBW.Uponor2MQTT.Configuration;
using MBW.UponorApi;
using MBW.UponorApi.Enums;
using Microsoft.Extensions.Options;

namespace MBW.Uponor2MQTT.Features
{
    internal class ThermostatFeature : FeatureBase
    {
        private readonly UponorOperationConfiguration _operationConfig;
        private readonly SystemDetailsContainer _systemDetails;

        public ThermostatFeature(IServiceProvider serviceProvider, IOptions<UponorOperationConfiguration> operationConfig, SystemDetailsContainer systemDetails) : base(serviceProvider)
        {
            _operationConfig = operationConfig.Value;
            _systemDetails = systemDetails;
        }

        public override void Process(UponorResponseContainer values)
        {
            foreach ((int controller, int thermostat) in _systemDetails.GetAvailableThermostats())
            {
                string deviceId = IdBuilder.GetThermostatId(controller, thermostat);

                // Temperature
                MqttStateValueTopic sensor = HassMqttManager.GetEntityStateValue(deviceId, "temp", "state");

                if (values.TryGetValue(
                    UponorObjects.Thermostat(UponorThermostats.RoomTemperature, controller, thermostat),
                    UponorProperties.Value, out object objVal))
                {
                    sensor.Value = objVal;
                }

                // Setpoint
                sensor = HassMqttManager.GetEntityStateValue(deviceId, "temp", "setpoint");

                if (values.TryGetValue(
                    UponorObjects.Thermostat(UponorThermostats.RoomSetpoint, controller, thermostat),
                    UponorProperties.Value, out objVal))
                {
                    sensor.Value = objVal;
                }

                // Action & Mode
                sensor = HassMqttManager.GetEntityStateValue(deviceId, "temp", "action");
                MqttStateValueTopic modeSensor = HassMqttManager.GetEntityStateValue(deviceId, "temp", "mode");

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

                    sensor.Value = action;
                    modeSensor.Value = mode;
                }

                // Home/away
                sensor = HassMqttManager.GetEntityStateValue(deviceId, "temp", "awaymode");

                if (values.TryGetValue(
                    UponorObjects.Thermostat(UponorThermostats.HomeAwayModeStatus, controller, thermostat),
                    UponorProperties.Value, out intVal))
                {
                    if (intVal > 0)
                        // Away
                        sensor.Value = "on";
                    else
                        // Home
                        sensor.Value = "off";
                }
            }
        }
    }
}