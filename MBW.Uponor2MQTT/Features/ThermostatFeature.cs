using System;
using MBW.HassMQTT.Mqtt;
using MBW.Uponor2MQTT.UhomeUponor;
using MBW.Uponor2MQTT.UhomeUponor.Enums;

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
                    sensor.Set(objVal);
                }

                // Setpoint
                topic = TopicBuilder.GetEntityTopic(deviceId, "temp", "setpoint");
                sensor = SensorStore.GetStateValue(topic);

                if (values.TryGetValue(
                    UponorObjects.Thermostat(UponorThermostats.RoomSetpoint, controller, thermostat),
                    UponorProperties.Value, out objVal))
                {
                    sensor.Set(objVal);
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

                    sensor.Set(action);
                    modeSensor.Set(mode);
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
                        sensor.Set("on");
                    else
                        // Home
                        sensor.Set("off");
                }
            }
        }
    }
}