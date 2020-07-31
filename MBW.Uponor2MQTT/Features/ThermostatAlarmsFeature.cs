using System;
using System.Collections.Generic;
using System.Linq;
using MBW.HassMQTT;
using MBW.UponorApi;
using MBW.UponorApi.Enums;

namespace MBW.Uponor2MQTT.Features
{
    internal class ThermostatAlarmsFeature : FeatureBase
    {
        private readonly SystemDetailsContainer _systemDetails;

        private const int BatteryOk = 100;
        private const int BatteryLow = 10;

        public ThermostatAlarmsFeature(IServiceProvider serviceProvider, SystemDetailsContainer systemDetails) : base(serviceProvider)
        {
            _systemDetails = systemDetails;
        }

        public override void Process(UponorResponseContainer values)
        {
            List<string> problems = new List<string>();

            foreach ((int controller, int thermostat) in _systemDetails.GetAvailableThermostats())
            {
                string deviceId = IdBuilder.GetThermostatId(controller, thermostat);

                // Battery sensor
                // We don't know what the battery level is with Uponor. So we can only say it's "good" or "bad"
                string topic = TopicBuilder.GetEntityTopic(deviceId, "battery", "state");
                MqttValueTopic sensor = SensorStore.GetStateValue(topic);

                if (values.TryGetValue(
                    UponorObjects.Thermostat(UponorThermostats.BatteryAlarm, controller, thermostat),
                    UponorProperties.Value, out object objVal) && objVal != null)
                    sensor.Value = BatteryLow;
                else
                    sensor.Value = BatteryOk;

                // Alarm sensor
                topic = TopicBuilder.GetEntityTopic(deviceId, "alarms", "state");
                sensor = SensorStore.GetStateValue(topic);

                string attributesTopic = TopicBuilder.GetAttributesTopic(deviceId, "alarms");
                MqttAttributesTopic attributes = SensorStore.GetAttributesValue(attributesTopic);

                problems.Clear();

                // Check one of: RfAlarm, BatteryAlarm, TechnicalAlarm, TamperIndication
                if (values.TryGetValue(
                    UponorObjects.Thermostat(UponorThermostats.RfAlarm, controller, thermostat),
                    UponorProperties.Value, out objVal) && objVal != null)
                {
                    problems.Add("No signal");
                    attributes.SetAttribute("signal", "alarm");
                }
                else
                    attributes.SetAttribute("signal", "ok");

                if (values.TryGetValue(
                    UponorObjects.Thermostat(UponorThermostats.TechnicalAlarm, controller, thermostat),
                    UponorProperties.Value, out objVal) && objVal != null)
                {
                    problems.Add("Technical (?)");
                    attributes.SetAttribute("technical", "alarm");
                }
                else
                    attributes.SetAttribute("technical", "ok");

                if (values.TryGetValue(
                    UponorObjects.Thermostat(UponorThermostats.TamperIndication, controller, thermostat),
                    UponorProperties.Value, out objVal) && objVal != null)
                {
                    problems.Add("Tampering");
                    attributes.SetAttribute("tampering", "alarm");
                }
                else
                    attributes.SetAttribute("tampering", "ok");

                if (problems.Any())
                {
                    sensor.Value = "on";
                    attributes.SetAttribute("problem", string.Join(", ", problems));
                }
                else
                {
                    sensor.Value = "off";
                    attributes.SetAttribute("problem", string.Empty);
                }
            }
        }
    }
}