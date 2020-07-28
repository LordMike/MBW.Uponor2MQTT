using System;
using System.Threading;
using System.Threading.Tasks;
using MBW.HassMQTT.Discovery;
using MBW.HassMQTT.Enum;
using MBW.HassMQTT.Mqtt;
using MBW.Uponor2MQTT.Features;
using MBW.Uponor2MQTT.HASS;
using MBW.Uponor2MQTT.Helpers;
using MBW.Uponor2MQTT.UhomeUponor;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet.Client;
using Nito.AsyncEx;

namespace MBW.Uponor2MQTT.Service
{
    internal class UponorConnectedService : BackgroundService
    {
        private readonly ILogger<UponorConnectedService> _logger;
        private readonly IMqttClient _mqttClient;
        private readonly SensorStore _sensorStore;
        private readonly UhomeUponorClient _uponorClient;
        private readonly HassTopicBuilder _topicBuilder;

        public const string OkMessage = "ok";
        public const string ProblemMessage = "problem";

        private readonly string _version;
        private const string DeviceId = "uponor2mqtt";
        private const string EntityId = "api_operational";

        private readonly string _stateTopic;
        private readonly string _attributesTopic;

        private readonly TimeSpan _flushInterval = TimeSpan.FromSeconds(5);
        private readonly AsyncAutoResetEvent _shouldFlush = new AsyncAutoResetEvent(false);

        public UponorConnectedService(ILogger<UponorConnectedService> logger,
            IMqttClient mqttClient,
            SensorStore sensorStore,
            UhomeUponorClient uponorClient,
            HassTopicBuilder topicBuilder)
        {
            _logger = logger;
            _mqttClient = mqttClient;
            _sensorStore = sensorStore;
            _uponorClient = uponorClient;
            _topicBuilder = topicBuilder;

            _version = typeof(Program).Assembly.GetName().Version.ToString(3);

            _stateTopic = topicBuilder.GetSystemTopic(EntityId);
            _attributesTopic = topicBuilder.GetAttributesTopic(DeviceId, EntityId);
        }

        private async Task UponorClientOnOnSuccessfulResponse()
        {
            MqttAttributesTopic attributes = _sensorStore.GetAttributesValue(_attributesTopic);
            MqttValueTopic state = _sensorStore.GetStateValue(_stateTopic);

            state.Set(OkMessage);
            attributes.SetAttribute("last_ok", DateTime.UtcNow.ToString("O"));

            _shouldFlush.Set();
        }

        private async Task UponorClientOnOnFailedResponse(string message)
        {
            MqttAttributesTopic attributes = _sensorStore.GetAttributesValue(_attributesTopic);
            MqttValueTopic state = _sensorStore.GetStateValue(_stateTopic);

            state.Set(ProblemMessage);
            attributes.SetAttribute("last_bad", DateTime.UtcNow.ToString("O"));
            attributes.SetAttribute("last_bad_status", message);

            _shouldFlush.Set();
        }

        private async Task FlushingTask(CancellationToken cancellationToken)
        {
            MqttAttributesTopic attributes = _sensorStore.GetAttributesValue(_attributesTopic);
            MqttValueTopic state = _sensorStore.GetStateValue(_stateTopic);

            while (!cancellationToken.IsCancellationRequested)
            {
                // Sleep for desired time
                await Task.Delay(_flushInterval, cancellationToken);

                // Wait for an update (if update was queued while sleeping, we pass immediately)
                // Cancellations throw, and abort the loop
                await _shouldFlush.WaitAsync(cancellationToken);

                // Flush our values
                await attributes.Flush(_mqttClient, cancellationToken: cancellationToken);
                await state.Flush(_mqttClient, cancellationToken: cancellationToken);
            }
        }

        private void CreateSystemEntities()
        {
            MqttBinarySensor sensor = _sensorStore.Configure<MqttBinarySensor>(DeviceId, EntityId);

            sensor.Device.Name = "Uponor2MQTT";
            sensor.Device.Identifiers = new[] { DeviceId };
            sensor.Device.SwVersion = _version;

            sensor.Name = "Uponor2MQTT API Operational";
            sensor.DeviceClass = HassDeviceClass.Problem;

            sensor.PayloadOn = ProblemMessage;
            sensor.PayloadOff = OkMessage;

            sensor.StateTopic = _stateTopic;
            sensor.JsonAttributesTopic = _attributesTopic;

            DiscoveryHelpers.ApplyAvailabilityInformation(sensor, _topicBuilder);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            CreateSystemEntities();

            // Push starting values
            MqttAttributesTopic attributes = _sensorStore.GetAttributesValue(_attributesTopic);

            attributes.SetAttribute("version", _version);
            attributes.SetAttribute("started", DateTime.UtcNow);

            _uponorClient.OnSuccessfulResponse += UponorClientOnOnSuccessfulResponse;
            _uponorClient.OnFailedResponse += UponorClientOnOnFailedResponse;

            // The flushing task is not really representative of this service, but it'll do
            return FlushingTask(stoppingToken);
        }
    }
}