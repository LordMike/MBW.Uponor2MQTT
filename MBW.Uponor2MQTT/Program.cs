using System;
using System.Net.Http;
using System.Threading.Tasks;
using MBW.HassMQTT;
using MBW.HassMQTT.CommonServices;
using MBW.HassMQTT.CommonServices.Commands;
using MBW.HassMQTT.CommonServices.MqttReconnect;
using MBW.HassMQTT.Topics;
using MBW.Uponor2MQTT.Commands;
using MBW.Uponor2MQTT.Configuration;
using MBW.Uponor2MQTT.Features;
using MBW.Uponor2MQTT.Helpers;
using MBW.Uponor2MQTT.Service;
using MBW.UponorApi;
using MBW.UponorApi.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Serilog;
using WebProxy = System.Net.WebProxy;

namespace MBW.Uponor2MQTT
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            // Logging to use before logging configuration is read
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Console()
                .CreateLogger();

            await CreateHostBuilder(args).RunConsoleAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(builder =>
                {
                    builder.AddJsonFile("appsettings.local.json", true);

                    string extraConfigFile = Environment.GetEnvironmentVariable("EXTRA_CONFIG_FILE");

                    if (extraConfigFile != null)
                    {
                        Log.Logger.Information("Loading extra config file at {path}", extraConfigFile);
                        builder.AddJsonFile(extraConfigFile, true);
                    }
                })
                .ConfigureLogging((context, builder) =>
                {
                    Log.Logger = new LoggerConfiguration()
                        .ReadFrom.Configuration(context.Configuration, "Logging")
                        .CreateLogger();

                    builder
                        .ClearProviders()
                        .AddSerilog();
                })
                .ConfigureServices(ConfigureServices);

        private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            services
                .AddAndConfigureMqtt("Uponor2MQTT")
                .Configure<CommonMqttConfiguration>(x=>x.ClientId = "uponor2mqtt")
                .Configure<CommonMqttConfiguration>( context.Configuration.GetSection("MQTT"))
                .Configure<MqttReconnectionServiceConfig>(context.Configuration.GetSection("MQTT"));

            services
                .Configure<HassConfiguration>(context.Configuration.GetSection("HASS"))
                .Configure<UponorConfiguration>(context.Configuration.GetSection("Uponor"))
                .Configure<UponorOperationConfiguration>(context.Configuration.GetSection("Uponor"))
                .Configure<ProxyConfiguration>(context.Configuration.GetSection("Proxy"))
                .AddSingleton(x => new HassMqttTopicBuilder(x.GetOptions<HassConfiguration>()))
                .AddHostedService<UponorConnectedService>()
                .AddHttpClient("uponor")
                .AddTransientHttpErrorPolicy(builder => builder.WaitAndRetryAsync(new[]
                {
                    TimeSpan.FromSeconds(30)
                }))
                .ConfigurePrimaryHttpMessageHandler(provider =>
                {
                    ProxyConfiguration proxyConfig = provider.GetOptions<ProxyConfiguration>();

                    SocketsHttpHandler handler = new SocketsHttpHandler();

                    if (proxyConfig.Uri != null)
                        handler.Proxy = new WebProxy(proxyConfig.Uri);

                    return handler;
                });

            services.AddSingleton(provider => ActivatorUtilities.CreateInstance<UhomeUponorClient>(provider,
                provider.GetRequiredService<IHttpClientFactory>().CreateClient("uponor")));

            services
                .AddSingleton<FeatureManager>()
                .AddSingleton<FeatureBase, ControllerFeature>()
                .AddSingleton<FeatureBase, UhomeFeature>()
                .AddSingleton<FeatureBase, ThermostatFeature>()
                .AddSingleton<FeatureBase, ThermostatAlarmsFeature>()
                .AddSingleton<FeatureBase, ThermostatHumidityFeature>()
                .AddSingleton<FeatureBase, ThermostatTemperatureFeature>();

            services
                .AddSingleton<SystemDetailsContainer>()
                .AddHostedService<UponorDiscoveryService>()
                .AddHostedService<UponorThermostatsService>();

            services
                .AddMqttCommandService()
                .AddMqttCommandHandler<SetSetpointCommand>()
                .AddMqttCommandHandler<SetRoomnameCommand>();
        }
    }
}
