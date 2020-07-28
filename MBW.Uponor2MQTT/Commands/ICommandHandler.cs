using System.Threading;
using System.Threading.Tasks;
using MQTTnet;

namespace MBW.Uponor2MQTT.Commands
{
    internal interface ICommandHandler
    {
        string[] GetFilter();

        Task Handle(string[] topicLevels, MqttApplicationMessage message, CancellationToken token = default);
    }
}