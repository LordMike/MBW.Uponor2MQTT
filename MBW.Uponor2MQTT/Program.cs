using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MBW.HassMQTT.Mqtt;
using MBW.Uponor2MQTT.Commands;
using MBW.Uponor2MQTT.Configuration;
using MBW.Uponor2MQTT.Features;
using MBW.Uponor2MQTT.HASS;
using MBW.Uponor2MQTT.Helpers;
using MBW.Uponor2MQTT.MQTT;
using MBW.Uponor2MQTT.Service;
using MBW.Uponor2MQTT.UhomeUponor;
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

            IHostBuilder hostBuilder = CreateHostBuilder(args);

            {
                //IHost host = hostBuilder.Build();
                //UhomeUponorClient cl = host.Services.GetRequiredService<UhomeUponorClient>();

                //SystemProperties res2 = await cl.GetSystemInfo();

                //string str2 = JsonConvert.SerializeObject(res2, Formatting.Indented);

                //UponorResponseContainer res = await cl.GetAll();
                //string str = JsonConvert.SerializeObject(res, Formatting.Indented);

                //while (true)
                //{
                //    //SystemProperties res = await cl.GetSystemInfo();
                //    var res = await cl.GetAll();

                //    string str = JsonConvert.SerializeObject(res, Formatting.Indented);
                //    Console.WriteLine(str);

                //    await Task.Delay(TimeSpan.FromSeconds(5));
                //}
            }

            await hostBuilder.RunConsoleAsync();
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
                .AddSingleton<IMqttFactory>(provider =>
                {
                    ILoggerFactory loggerFactory = provider.GetRequiredService<ILoggerFactory>();
                    ExtensionsLoggingMqttLogger logger = new ExtensionsLoggingMqttLogger(loggerFactory, "MqttNet");

                    return new MqttFactory(logger);
                })
                .AddHostedService<HassAliveAndWillService>()
                .AddHostedService<MqttConnectionService>()
                .AddSingleton<MqttEvents>()
                .AddHostedService<MqttMessageDistributor>()
                .AddSingleton<IMqttClientOptions>(provider =>
                {
                    MqttConfiguration mqttConfig = provider.GetOptions<MqttConfiguration>();
                    HassTopicBuilder topicBuilder = provider.GetRequiredService<HassTopicBuilder>();

                    // Prepare options
                    MqttClientOptionsBuilder optionsBuilder = new MqttClientOptionsBuilder()
                        .WithTcpServer(mqttConfig.Server, mqttConfig.Port)
                        .WithCleanSession(false)
                        .WithClientId(mqttConfig.ClientId)
                        .WithWillMessage(new MqttApplicationMessage
                        {
                            Topic = topicBuilder.GetSystemTopic("status"),
                            Payload = Encoding.UTF8.GetBytes(HassAliveAndWillService.ProblemMessage),
                            Retain = true
                        });

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

            services
                .Configure<HassConfiguration>(context.Configuration.GetSection("HASS"))
                .Configure<UponorConfiguration>(context.Configuration.GetSection("Uponor"))
                .Configure<ProxyConfiguration>(context.Configuration.GetSection("Proxy"))
                .AddSingleton(x => new HassTopicBuilder(x.GetOptions<HassConfiguration>()))
                .AddSingleton<HassUniqueIdBuilder>()
                .AddHostedService<UponorConnectedService>()
                .AddHttpClient("uponor")
                .AddTransientHttpErrorPolicy(builder => builder.WaitAndRetryAsync(new[]
                {
                    TimeSpan.FromSeconds(1), 
                    //TimeSpan.FromSeconds(30),
                    //TimeSpan.FromSeconds(60)
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
                .AddSingleton<SensorStore>()
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
                .AddSingleton<MqttCommandService>()
                .AddHostedService(x => x.GetRequiredService<MqttCommandService>())
                .AddSingleton<IMqttMessageReceiver>(x => x.GetRequiredService<MqttCommandService>())
                .AddSingleton<ICommandHandler, SetSetpointCommand>()
                .AddSingleton<ICommandHandler, SetRoomnameCommand>();
        }
    }
}
