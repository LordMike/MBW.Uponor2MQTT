using System.Threading;
using System.Threading.Tasks;
using MQTTnet;

namespace MBW.Uponor2MQTT.MQTT
{
    internal interface IMqttMessageReceiver
    {
        Task ReceiveAsync(MqttApplicationMessage argApplicationMessage, CancellationToken token = default);
    }
}