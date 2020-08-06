using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MBW.Uponor2MQTT.Helpers
{
    internal static class ServiceProviderExtensions
    {
        public static TOptions GetOptions<TOptions>(this IServiceProvider provider) where TOptions : class, new()
        {
            return provider.GetRequiredService<IOptions<TOptions>>().Value;
        }

        public static ILogger<T> GetLogger<T>(this IServiceProvider provider)
        {
            return provider.GetRequiredService<ILogger<T>>();
        }

        public static ILogger GetLogger(this IServiceProvider provider, Type type)
        {
            return provider.GetRequiredService<ILoggerFactory>().CreateLogger(type);
        }
    }
}