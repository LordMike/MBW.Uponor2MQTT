using System.Threading;
using System.Threading.Tasks;
using MBW.UponorApi.Enums;

namespace MBW.UponorApi
{
    public static class UhomeUponorClientExtensions
    {
        public static Task SetValue(this UhomeUponorClient client, int @object, UponorProperties property, object value, CancellationToken token = default)
        {
            return client.SetValues(new[] { (@object, property, value) }, token);
        }

        public static Task<UponorResponseContainer> ReadValue(this UhomeUponorClient client, int @object, UponorProperties property, CancellationToken token = default)
        {
            return client.ReadValues(new[] { @object }, new[] { property }, token);
        }
    }
}