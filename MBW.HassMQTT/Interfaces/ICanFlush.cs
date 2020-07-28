using System.Threading;
using System.Threading.Tasks;
using MQTTnet.Client;

namespace MBW.HassMQTT.Interfaces
{
    public interface ICanFlush
    {
        Task<bool> Flush(IMqttClient mqttClient, bool forceFlush = false, CancellationToken cancellationToken = default);
    }
}