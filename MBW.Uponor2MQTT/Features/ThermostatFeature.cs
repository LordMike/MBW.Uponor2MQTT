using System;
using MBW.HassMQTT;
using MBW.UponorApi;
using MBW.UponorApi.Enums;

namespace MBW.Uponor2MQTT.Features
{
    internal class ThermostatFeature : FeatureBase
    {
        private readonly SystemDetailsContainer _systemDetails;

        public ThermostatFeature(IServiceProvider serviceProvider, SystemDetailsContainer systemDetails) : base(serviceProvider)
        {
            _systemDetails = systemDetails;
        }

        public override void Process(UponorResponseContainer values)
        {
            foreach ((int controller, int thermostat) in _systemDetails.GetAvailableThermostats())
            {
                string deviceId = IdBuilder.GetThermostatId(controller, thermostat);

                // Temperature
                string topic = TopicBuilder.GetEntityTopic(deviceId, "temp", "state");
                MqttValueTopic sensor = SensorStore.GetStateValue(topic);

                if (values.TryGetValue(
                    UponorObjects.Thermostat(UponorThermostats.RoomTemperature, controller, thermostat),
                    UponorProperties.Value, out object objVal))
                {
                    sensor.Value = objVal;
                }

                // Setpoint
                topic = TopicBuilder.GetEntityTopic(deviceId, "temp", "setpoint");
                sensor = SensorStore.GetStateValue(topic);

                if (values.TryGetValue(
                    UponorObjects.Thermostat(UponorThermostats.RoomSetpoint, controller, thermostat),
                    UponorProperties.Value, out objVal))
                {
                    sensor.Value = objVal;
                }

                // Action & Mode
                topic = TopicBuilder.GetEntityTopic(deviceId, "temp", "action");
                sensor = SensorStore.GetStateValue(topic);

                topic = TopicBuilder.GetEntityTopic(deviceId, "temp", "mode");
                var modeSensor = SensorStore.GetStateValue(topic);

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

                    mode = "auto";

                    sensor.Value = action;
                    modeSensor.Value = mode;
                }

                // Home/away
                topic = TopicBuilder.GetEntityTopic(deviceId, "temp", "awaymode");
                sensor = SensorStore.GetStateValue(topic);

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