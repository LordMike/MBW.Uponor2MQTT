using System;
using System.Threading;
using System.Threading.Tasks;
using MBW.HassMQTT;
using MBW.HassMQTT.CommonServices.AliveAndWill;
using MBW.HassMQTT.DiscoveryModels.Enum;
using MBW.HassMQTT.DiscoveryModels.Models;
using MBW.HassMQTT.Extensions;
using MBW.HassMQTT.Interfaces;
using MBW.UponorApi;
using Microsoft.Extensions.Hosting;
using Nito.AsyncEx;

namespace MBW.Uponor2MQTT.Service
{
    internal class UponorConnectedService : BackgroundService
    {
        private readonly HassMqttManager _hassMqttManager;
        private readonly UhomeUponorClient _uponorClient;

        public const string OkMessage = "ok";
        public const string ProblemMessage = "problem";

        private readonly string _version;
        private const string DeviceId = "Uponor2MQTT";
        private const string EntityId = "api_operational";

        private readonly TimeSpan _flushInterval = TimeSpan.FromSeconds(5);
        private readonly AsyncAutoResetEvent _shouldFlush = new AsyncAutoResetEvent(false);

        public UponorConnectedService(HassMqttManager hassMqttManager,
            UhomeUponorClient uponorClient)
        {
            _hassMqttManager = hassMqttManager;
            _uponorClient = uponorClient;

            _version = typeof(Program).Assembly.GetName().Version.ToString(3);
        }

        private Task UponorClientOnOnSuccessfulResponse()
        {
            ISensorContainer sensor = _hassMqttManager.GetSensor(DeviceId, EntityId);

            sensor.SetValue(HassTopicKind.State, OkMessage);
            sensor.SetAttribute("last_ok", DateTime.UtcNow.ToString("O"));

            _shouldFlush.Set();

            return Task.CompletedTask;
        }

        private Task UponorClientOnOnFailedResponse(string message)
        {
            ISensorContainer sensor = _hassMqttManager.GetSensor(DeviceId, EntityId);

            sensor.SetValue(HassTopicKind.State, ProblemMessage);
            sensor.SetAttribute("last_bad", DateTime.UtcNow.ToString("O"));
            sensor.SetAttribute("last_bad_status", message);

            _shouldFlush.Set();

            return Task.CompletedTask;
        }

        private async Task FlushingTask(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Sleep for desired time
                await Task.Delay(_flushInterval, cancellationToken);

                // Wait for an update (if update was queued while sleeping, we pass immediately)
                // Cancellations throw, and abort the loop
                await _shouldFlush.WaitAsync(cancellationToken);

                // Flush our values
                await _hassMqttManager.FlushAll(cancellationToken);
            }
        }

        private void CreateSystemEntities()
        {
            _hassMqttManager.ConfigureSensor<MqttBinarySensor>(DeviceId, EntityId)
                .ConfigureTopics(HassTopicKind.State, HassTopicKind.JsonAttributes)
                .ConfigureDevice(device =>
                {
                    if (!device.Identifiers.Contains(DeviceId))
                        device.Identifiers.Add(DeviceId);

                    device.Name = "Uponor2MQTT";
                    device.SwVersion = _version;
                })
                .ConfigureDiscovery(discovery =>
                {
                    discovery.Name = "Uponor2MQTT API Operational";
                    discovery.DeviceClass = HassBinarySensorDeviceClass.Problem;

                    discovery.PayloadOn = ProblemMessage;
                    discovery.PayloadOff = OkMessage;
                })
                .ConfigureAliveService();
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            CreateSystemEntities();

            // Push starting values
            MqttAttributesTopic attributes = _hassMqttManager.GetSensor(DeviceId, EntityId)
                .GetAttributesSender();

            attributes.SetAttribute("version", _version);
            attributes.SetAttribute("started", DateTime.UtcNow);

            _uponorClient.OnSuccessfulResponse += UponorClientOnOnSuccessfulResponse;
            _uponorClient.OnFailedResponse += UponorClientOnOnFailedResponse;

            // The flushing task is not really representative of this service, but it'll do
            return FlushingTask(stoppingToken);
        }
    }
}