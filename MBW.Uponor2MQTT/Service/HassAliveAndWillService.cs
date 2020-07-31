using System;
using System.Threading;
using System.Threading.Tasks;
using MBW.HassMQTT;
using MBW.HassMQTT.DiscoveryModels.Enum;
using MBW.HassMQTT.DiscoveryModels.Models;
using MBW.HassMQTT.Helpers;
using MBW.HassMQTT.Mqtt;
using MBW.Uponor2MQTT.HASS;
using Microsoft.Extensions.Hosting;
using MQTTnet.Client;

namespace MBW.Uponor2MQTT.Service
{
    internal class HassAliveAndWillService : BackgroundService
    {
        private readonly IMqttClient _mqttClient;
        private readonly MqttEvents _mqttEvents;
        private readonly HassMqttManager _hassMqttManager;

        public const string OkMessage = "ok";
        public const string ProblemMessage = "problem";

        private readonly string _version;
        private const string DeviceId = "uponor2mqtt";
        private const string EntityId = "status";

        private readonly string _stateTopic;
        private readonly string _attributesTopic;

        public HassAliveAndWillService(IMqttClient mqttClient,
            MqttEvents mqttEvents,
            HassMqttManager hassMqttManager,
            HassMqttTopicBuilder topicBuilder)
        {
            _mqttClient = mqttClient;
            _mqttEvents = mqttEvents;
            _hassMqttManager = hassMqttManager;

            _version = typeof(Program).Assembly.GetName().Version.ToString(3);

            _stateTopic = topicBuilder.GetSystemTopic(EntityId);
            _attributesTopic = topicBuilder.GetAttributesTopic(DeviceId, EntityId);
        }

        private void CreateSystemEntities()
        {
            MqttBinarySensor sensor = _hassMqttManager.ConfigureDiscovery<MqttBinarySensor>(DeviceId, EntityId);

            sensor.Device.Name = "Uponor2MQTT";
            sensor.Device.Identifiers = new[] { DeviceId };
            sensor.Device.SwVersion = _version;

            sensor.Name = "Uponor2MQTT Status";
            sensor.DeviceClass = HassDeviceClass.Problem;

            sensor.PayloadOn = ProblemMessage;
            sensor.PayloadOff = OkMessage;

            sensor.StateTopic = _stateTopic;
            sensor.JsonAttributesTopic = _attributesTopic;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            CreateSystemEntities();

            // Push starting values
            MqttAttributesTopic attributes = _hassMqttManager.GetAttributesValue(DeviceId, EntityId);

            attributes.SetAttribute("version", _version);
            attributes.SetAttribute("started", DateTime.UtcNow);

            // Hook to on connect
            // Connected: Push "ok" message
            // Testament (When disconnected): Leave "problem" message

            _mqttEvents.OnConnect += async (args, token) =>
            {
                if (stoppingToken.IsCancellationRequested)
                    return;

                await _mqttClient.SendValueAsync(_stateTopic, OkMessage, token);
            };

            // Send initial Ok message
            await _mqttClient.SendValueAsync(_stateTopic, OkMessage, stoppingToken);
        }
    }
}