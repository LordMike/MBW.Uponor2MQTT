using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MBW.Uponor2MQTT.Features;
using MBW.Uponor2MQTT.UhomeUponor;
using MBW.Uponor2MQTT.UhomeUponor.Enums;
using MQTTnet;
using MQTTnet.Client;

namespace MBW.Uponor2MQTT.Commands
{
    internal class SetRoomnameCommand : ICommandHandler
    {
        private readonly IMqttClient _mqqClient;
        private readonly UhomeUponorClient _client;
        private readonly FeatureManager _featureManager;
        private readonly SensorStore _sensorStore;
        private readonly Regex _thermostatRegex = new Regex(@"^uponor_c(?<controller>[0-9]+)_t(?<thermostat>[0-9]+)$", RegexOptions.Compiled);

        public SetRoomnameCommand(IMqttClient mqqClient, UhomeUponorClient client, FeatureManager featureManager, SensorStore sensorStore)
        {
            _mqqClient = mqqClient;
            _client = client;
            _featureManager = featureManager;
            _sensorStore = sensorStore;
        }

        public string[] GetFilter()
        {
            // <entity>/set_name => Set name
            return new[] { null, "set_name" };
        }

        public async Task Handle(string[] topicLevels, MqttApplicationMessage message, CancellationToken token = default)
        {
            Match mtch = _thermostatRegex.Match(topicLevels[0]);
            int controller = int.Parse(mtch.Groups["controller"].Value);
            int thermostat = int.Parse(mtch.Groups["thermostat"].Value);

            int obj = UponorObjects.Thermostat(UponorThermostats.RoomName, controller, thermostat);

            string newName = message.ConvertPayloadToString();

            await _client.SetValue(obj, UponorProperties.Value, newName, token);

            // Perform new read
            UponorResponseContainer newValues = await _client.ReadValue(obj, UponorProperties.Value, token);

            _featureManager.Process(newValues);
            await _sensorStore.FlushAll(_mqqClient, token);
        }
    }
}