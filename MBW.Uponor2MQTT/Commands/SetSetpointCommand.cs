using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EnumsNET;
using MBW.HassMQTT;
using MBW.HassMQTT.CommonServices.Commands;
using MBW.HassMQTT.DiscoveryModels.Enum;
using MBW.Uponor2MQTT.Features;
using MBW.Uponor2MQTT.Helpers;
using MBW.UponorApi;
using MBW.UponorApi.Enums;
using Microsoft.Extensions.Logging;
using MQTTnet;

namespace MBW.Uponor2MQTT.Commands
{
    internal class SetSetpointCommand : IMqttCommandHandler
    {
        private readonly ILogger<SetSetpointCommand> _logger;
        private readonly UhomeUponorClient _client;
        private readonly FeatureManager _featureManager;
        private readonly HassMqttManager _hassMqttManager;
        private readonly CultureInfo _parsingCulture = CultureInfo.InvariantCulture;
        private readonly Regex _thermostatRegex = new Regex(@"^uponor_c(?<controller>[0-9]+)_t(?<thermostat>[0-9]+)$", RegexOptions.Compiled);

        public SetSetpointCommand(ILogger<SetSetpointCommand> logger, UhomeUponorClient client, FeatureManager featureManager, HassMqttManager hassMqttManager)
        {
            _logger = logger;
            _client = client;
            _featureManager = featureManager;
            _hassMqttManager = hassMqttManager;
        }

        public string[] GetFilter()
        {
            // <cN_tN>/temp/temperature_command => Set setpoint
            return new[] { null, "temp", HassTopicKind.TemperatureCommand.AsString(EnumFormat.EnumMemberValue) };
        }

        public async Task Handle(string[] topicLevels, MqttApplicationMessage message, CancellationToken token = default)
        {
            Match mtch = _thermostatRegex.Match(topicLevels[0]);
            int controller = int.Parse(mtch.Groups["controller"].Value);
            int thermostat = int.Parse(mtch.Groups["thermostat"].Value);

            int obj = UponorObjects.Thermostat(UponorThermostats.RoomSetpoint, controller, thermostat);

            string convertPayloadToString = message.ConvertPayloadToString();
            float newSetpoint = float.Parse(convertPayloadToString, _parsingCulture);

            _logger.LogInformation("Setting c{Controller} t{Thermostat} setpoint to {Value}", controller, thermostat, newSetpoint);

            await _client.SetValue(obj, UponorProperties.Value, newSetpoint, token);

            // Perform new read, wait until the value is applied
            using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using CancellationTokenSource timeoutToken = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, token);

            (_, UponorResponseContainer responseContainer) = await _client.WaitUntil(obj, UponorProperties.Value,
                testContainer =>
                {
                    if (!testContainer.TryGetValue(obj, UponorProperties.Value, out float appliedFloat))
                        return false;

                    return Math.Abs(appliedFloat - newSetpoint) < 0.5f;
                }, timeoutToken.Token);

            _featureManager.Process(responseContainer);
            await _hassMqttManager.FlushAll(token);
        }
    }
}