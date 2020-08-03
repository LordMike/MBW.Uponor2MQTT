using System;
using System.Collections.Generic;
using System.Linq;
using MBW.HassMQTT.DiscoveryModels.Enum;
using MBW.HassMQTT.Extensions;
using MBW.HassMQTT.Interfaces;
using MBW.Uponor2MQTT.HASS;
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
                string deviceId = HassUniqueIdBuilder.GetThermostatDeviceId(controller, thermostat);

                // Battery sensor
                // We don't know what the battery level is with Uponor. So we can only say it's "good" or "bad"
                ISensorContainer sensor = HassMqttManager.GetSensor(deviceId, "battery");

                if (values.TryGetValue(
                    UponorObjects.Thermostat(UponorThermostats.BatteryAlarm, controller, thermostat),
                    UponorProperties.Value, out object objVal) && objVal != null)
                    sensor.SetValue(HassTopicKind.State, BatteryLow);
                else
                    sensor.SetValue(HassTopicKind.State, BatteryOk);

                // Alarm sensor
                sensor = HassMqttManager.GetSensor(deviceId, "alarms");

                problems.Clear();

                // Check one of: RfAlarm, BatteryAlarm, TechnicalAlarm, TamperIndication
                if (values.TryGetValue(
                    UponorObjects.Thermostat(UponorThermostats.RfAlarm, controller, thermostat),
                    UponorProperties.Value, out objVal) && objVal != null)
                {
                    problems.Add("No signal");
                    sensor.SetAttribute("signal", "alarm");
                }
                else
                    sensor.SetAttribute("signal", "ok");

                if (values.TryGetValue(
                    UponorObjects.Thermostat(UponorThermostats.TechnicalAlarm, controller, thermostat),
                    UponorProperties.Value, out objVal) && objVal != null)
                {
                    problems.Add("Technical (?)");
                    sensor.SetAttribute("technical", "alarm");
                }
                else
                    sensor.SetAttribute("technical", "ok");

                if (values.TryGetValue(
                    UponorObjects.Thermostat(UponorThermostats.TamperIndication, controller, thermostat),
                    UponorProperties.Value, out objVal) && objVal != null)
                {
                    problems.Add("Tampering");
                    sensor.SetAttribute("tampering", "alarm");
                }
                else
                    sensor.SetAttribute("tampering", "ok");

                if (problems.Any())
                {
                    sensor.SetValue(HassTopicKind.State, "on");
                    sensor.SetAttribute("problem", string.Join(", ", problems));
                }
                else
                {
                    sensor.SetValue(HassTopicKind.State, "off");
                    sensor.SetAttribute("problem", string.Empty);
                }
            }
        }
    }
}