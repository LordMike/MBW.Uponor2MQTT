using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MBW.HassMQTT;
using MBW.HassMQTT.CommonServices.AliveAndWill;
using MBW.HassMQTT.CommonServices.Commands;
using MBW.HassMQTT.CommonServices.MqttReconnect;
using MBW.HassMQTT.Services;
using MBW.HassMQTT.Topics;
using MBW.Uponor2MQTT.Commands;
using MBW.Uponor2MQTT.Configuration;
using MBW.Uponor2MQTT.Features;
using MBW.Uponor2MQTT.HASS;
using MBW.Uponor2MQTT.Helpers;
using MBW.Uponor2MQTT.Service;
using MBW.UponorApi;
using MBW.UponorApi.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
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
                .Configure<MqttConfiguration>(context.Configuration.GetSection("MQTT"))
                .AddMqttClientFactoryWithLogging()
                .AddSingleton<IMqttClientOptions>(provider =>
                {
                    MqttConfiguration mqttConfig = provider.GetOptions<MqttConfiguration>();

                    // Prepare options
                    MqttClientOptionsBuilder optionsBuilder = new MqttClientOptionsBuilder()
                        .WithTcpServer(mqttConfig.Server, mqttConfig.Port)
                        .WithCleanSession(false)
                        .WithClientId(mqttConfig.ClientId)
                        .ConfigureHassConnectedEntityServiceLastWill(provider);

                    if (!string.IsNullOrEmpty(mqttConfig.Username))
                        optionsBuilder.WithCredentials(mqttConfig.Username, mqttConfig.Password);

                    if (mqttConfig.KeepAlivePeriod.HasValue)
                        optionsBuilder.WithKeepAlivePeriod(mqttConfig.KeepAlivePeriod.Value);

                    return optionsBuilder.Build();
                })
                .AddSingleton<IMqttClient>(provider =>
                {
                    IHostApplicationLifetime appLifetime = provider.GetRequiredService<IHostApplicationLifetime>();
                    CancellationToken stoppingtoken = appLifetime.ApplicationStopping;

                    MqttEvents mqttEvents = provider.GetRequiredService<MqttEvents>();

                    // TODO: Support TLS & client certs
                    IMqttFactory factory = provider.GetRequiredService<IMqttFactory>();

                    // Prepare options
                    IMqttClientOptions options = provider.GetRequiredService<IMqttClientOptions>();

                    // Create client
                    IMqttClient mqttClient = factory.CreateMqttClient();

                    // Hook up event handlers
                    // TODO: Move to MBW.HassMQTT
                    mqttClient.UseDisconnectedHandler(async args =>
                    {
                        await mqttEvents.InvokeDisconnectHandler(args, stoppingtoken);
                    });
                    mqttClient.UseConnectedHandler(async args =>
                    {
                        await mqttEvents.InvokeConnectHandler(args, stoppingtoken);
                    });

                    // Connect
                    mqttClient.ConnectAsync(options, stoppingtoken);

                    return mqttClient;
                });

            // MQTT Services
            services
                .AddMqttMessageReceiverService()
                .AddMqttEvents();

            // MQTT Reconnect service
            services
                .AddMqttReconnectService()
                .Configure<MqttReconnectionServiceConfig>(context.Configuration.GetSection("MQTT"));

            // Hass Connected service (MQTT Last Will)
            services
                .AddHassConnectedEntityServiceExtensions()
                .Configure<HassConnectedEntityServiceConfig>(x =>
                {
                    x.DeviceId = "Uponor2MQTT";
                    x.DiscoveryDeviceName = "Uponor2MQTT";
                    x.DiscoveryEntityName = "Uponor2MQTT Status";
                });

            services
                .Configure<HassConfiguration>(x => x.TopicPrefix = "uhomeuponor")
                .Configure<HassConfiguration>(context.Configuration.GetSection("HASS"))
                .Configure<UponorConfiguration>(context.Configuration.GetSection("Uponor"))
                .Configure<UponorOperationConfiguration>(context.Configuration.GetSection("Uponor"))
                .Configure<ProxyConfiguration>(context.Configuration.GetSection("Proxy"))
                .AddSingleton(x => new HassMqttTopicBuilder(x.GetOptions<HassConfiguration>()))
                .AddSingleton<HassUniqueIdBuilder>()
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
                .AddSingleton<HassMqttManager>()
                .AddSingleton<FeatureManager>()
                .AddSingleton<FeatureBase, ControllerFeature>()
                .AddSingleton<FeatureBase, UhomeFeature>()
                .AddSingleton<FeatureBase, ThermostatFeature>()
                .AddSingleton<FeatureBase, ThermostatAlarmsFeature>()
                .AddSingleton<FeatureBase, ThermostatHumidityFeature>();

            services
                .AddSingleton<SystemDetailsContainer>()
                .AddHostedService<UponorDiscoveryService>()
                .AddHostedService<UponorSystemService>()
                .AddHostedService<UponorThermostatsService>();

            services
                .AddMqttCommandService()
                .AddMqttCommandHandler<SetSetpointCommand>()
                .AddMqttCommandHandler<SetRoomnameCommand>();
        }
    }
}
