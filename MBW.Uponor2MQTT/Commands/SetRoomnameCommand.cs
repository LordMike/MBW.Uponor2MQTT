using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MBW.HassMQTT;
using MBW.Uponor2MQTT.Features;
using MBW.UponorApi;
using MBW.UponorApi.Enums;
using Microsoft.Extensions.Logging;
using MQTTnet;

namespace MBW.Uponor2MQTT.Commands
{
    internal class SetRoomnameCommand : ICommandHandler
    {
        private readonly ILogger<SetRoomnameCommand> _logger;
        private readonly UhomeUponorClient _client;
        private readonly FeatureManager _featureManager;
        private readonly HassMqttManager _hassMqttManager;
        private readonly Regex _thermostatRegex = new Regex(@"^uponor_c(?<controller>[0-9]+)_t(?<thermostat>[0-9]+)$", RegexOptions.Compiled);

        public SetRoomnameCommand(ILogger<SetRoomnameCommand> logger, UhomeUponorClient client, FeatureManager featureManager, HassMqttManager hassMqttManager)
        {
            _logger = logger;
            _client = client;
            _featureManager = featureManager;
            _hassMqttManager = hassMqttManager;
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

            _logger.LogInformation("Setting c{Controller} t{Thermostat} name to {Value}", controller, thermostat, newName);

            await _client.SetValue(obj, UponorProperties.Value, newName, token);

            // Perform new read
            UponorResponseContainer newValues = await _client.ReadValue(obj, UponorProperties.Value, token);

            _featureManager.Process(newValues);
            await _hassMqttManager.FlushAll(token);
        }
    }
}