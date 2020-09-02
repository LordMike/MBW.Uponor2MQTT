using System;
using System.Threading;
using System.Threading.Tasks;
using MBW.UponorApi;
using MBW.UponorApi.Enums;

namespace MBW.Uponor2MQTT.Helpers
{
    internal static class UponorClientUtilities
    {
        public static async Task<(bool, UponorResponseContainer)> WaitUntil(this UhomeUponorClient client, int @object, UponorProperties property, Func<UponorResponseContainer, bool> condition, CancellationToken token = default)
        {
            UponorResponseContainer res = new UponorResponseContainer();
            try
            {
                while (!token.IsCancellationRequested)
                {
                    // Attempt to read the value
                    res = await client.ReadValue(@object, property, token);

                    bool conditionMet = condition(res);
                    if (conditionMet)
                        return (true, res);

                    // Condition was not met, carry on
                    await Task.Delay(TimeSpan.FromMilliseconds(500), token);
                }
            }
            catch (TaskCanceledException)
            {
                return (false, res);
            }
            
            return (false, res);
        }
    }
}