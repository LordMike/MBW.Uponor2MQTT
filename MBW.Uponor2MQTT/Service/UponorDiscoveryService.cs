using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MBW.HassMQTT;
using MBW.HassMQTT.CommonServices.AliveAndWill;
using MBW.HassMQTT.DiscoveryModels.Enum;
using MBW.HassMQTT.DiscoveryModels.Interfaces;
using MBW.HassMQTT.DiscoveryModels.Models;
using MBW.HassMQTT.Extensions;
using MBW.HassMQTT.Interfaces;
using MBW.Uponor2MQTT.Configuration;
using MBW.Uponor2MQTT.Features;
using MBW.Uponor2MQTT.HASS;
using MBW.UponorApi;
using MBW.UponorApi.Configuration;
using MBW.UponorApi.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MBW.Uponor2MQTT.Service
{
    /// <summary>
    /// This service gathers enough information to discover new thermostats and controllers.
    /// It should not be run as often as the normal updates, as this will query a lot of details.
    /// </summary>
    internal class UponorDiscoveryService : BackgroundService
    {
        private readonly ILogger<UponorDiscoveryService> _logger;
        private readonly UponorOperationConfiguration _operationConfig;
        private readonly UhomeUponorClient _uponorClient;
        private readonly FeatureManager _featureManager;
        private readonly SystemDetailsContainer _detailsContainer;
        private readonly HassMqttManager _hassMqttManager;
        private readonly UponorConfiguration _config;

        public UponorDiscoveryService(
            ILogger<UponorDiscoveryService> logger,
            IOptions<UponorConfiguration> config,
            IOptions<UponorOperationConfiguration> operationConfig,
            FeatureManager featureManager,
            UhomeUponorClient uponorClient,
            SystemDetailsContainer detailsContainer,
            HassMqttManager hassMqttManager)
        {
            _logger = logger;
            _operationConfig = operationConfig.Value;
            _uponorClient = uponorClient;
            _featureManager = featureManager;
            _detailsContainer = detailsContainer;
            _hassMqttManager = hassMqttManager;
            _config = config.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Update loop
            DateTime lastRun = DateTime.MinValue;

            while (!stoppingToken.IsCancellationRequested)
            {
                TimeSpan toDelay = _config.DiscoveryInterval - (DateTime.UtcNow - lastRun);
                if (toDelay > TimeSpan.Zero)
                {
                    try
                    {
                        await Task.Delay(toDelay, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // Do nothing
                        continue;
                    }
                }

                _logger.LogDebug("Beginning discovery");

                try
                {
                    UponorResponseContainer values = await AcquireDiscoveryDetails(stoppingToken);

                    CreateEntities(values);

                    _featureManager.Process(values);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Do nothing
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "An error occurred while performing the update");
                }

                try
                {
                    await _hassMqttManager.FlushAll(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Do nothing
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "An error occurred while pushing updated values");
                }

                lastRun = DateTime.UtcNow;
            }
        }

        private async Task<UponorResponseContainer> AcquireDiscoveryDetails(CancellationToken stoppingToken)
        {
            SystemProperties systemInfo = await _uponorClient.GetSystemInfo(stoppingToken);

            // Update details container
            _detailsContainer.Update(systemInfo.AvailableControllers, systemInfo.AvailableThermostats, systemInfo.AvailableOutdoorSensors, systemInfo.HcMode);

            // Batch all Value properties
            IEnumerable<int> objects = new[]{
                    UponorObjects.System(UponorSystem.DeviceLostAlarm),
                    UponorObjects.System(UponorSystem.NoCommController1),
                    UponorObjects.System(UponorSystem.NoCommController2),
                    UponorObjects.System(UponorSystem.NoCommController3),
                    UponorObjects.System(UponorSystem.NoCommController4),
                    UponorObjects.System(UponorSystem.UhomeModuleId),
                    UponorObjects.System(UponorSystem.DeviceLostAlarm),
                }
                .Concat(systemInfo.AvailableControllers.SelectMany(c => new[]
                {
                    UponorObjects.Controller(UponorController.ControllerSwVersion, c)
                }))
                .Concat(systemInfo.AvailableOutdoorSensors.SelectMany(s => new[]
                {
                    UponorObjects.Controller(UponorController.OutdoorSensorPresence, s)
                }))
                .Concat(_detailsContainer.GetAvailableThermostats().SelectMany(c => new[]
                {
                    UponorObjects.Thermostat(UponorThermostats.RoomName, c.controller, c.thermostat),
                    UponorObjects.Thermostat(UponorThermostats.MinSetpoint, c.controller, c.thermostat),
                    UponorObjects.Thermostat(UponorThermostats.MaxSetpoint, c.controller, c.thermostat)
                }));

            UponorResponseContainer values = await _uponorClient.ReadValues(objects, new[] { UponorProperties.Value }, stoppingToken);

            // Batch other properties
            // Update some system properties
            objects = systemInfo.AvailableControllers.SelectMany(c => new[]
            {
                UponorObjects.System(UponorSystem.DeviceObject)
            });

            UponorResponseContainer systemValues = await _uponorClient.ReadValues(objects, new[]
            {
                UponorProperties.ApplicationVersion,
                UponorProperties.DeviceName,
                UponorProperties.DeviceId,
                UponorProperties.SerialNumber,
                UponorProperties.ProductName,
                UponorProperties.Supplier,
                UponorProperties.MacAddress
            }, stoppingToken);

            values.Merge(systemValues);

            return values;
        }

        private void CreateEntities(UponorResponseContainer values)
        {
            // System
            string uHomeDeviceId = HassUniqueIdBuilder.GetUhomeDeviceId();

            {
                const string entityId = "uhome";
                _hassMqttManager.ConfigureSensor<MqttSensor>(uHomeDeviceId, entityId)
                    .ConfigureTopics(HassTopicKind.State, HassTopicKind.JsonAttributes)
                    .ConfigureDevice(device =>
                    {
                        if (!device.Identifiers.Contains(uHomeDeviceId))
                            device.Identifiers.Add(uHomeDeviceId);

                        device.Name = "Uponor U@Home";
                        device.Manufacturer = "Uponor";
                    })
                    .ConfigureAliveService();

                _hassMqttManager.ConfigureSensor<MqttSensor>(uHomeDeviceId, "average_temperature")
                    .ConfigureTopics(HassTopicKind.State)
                    .ConfigureDevice(device =>
                    {
                        if (!device.Identifiers.Contains(uHomeDeviceId))
                            device.Identifiers.Add(uHomeDeviceId);

                        device.Name = "Uponor U@Home";
                        device.Manufacturer = "Uponor";
                    })
                    .ConfigureDiscovery(discovery =>
                    {
                        discovery.Name = "Average Temperature";
                        discovery.UnitOfMeasurement = "°C";
                        discovery.DeviceClass = HassSensorDeviceClass.Temperature;
                    })
                    .ConfigureAliveService();
            }

            // Controllers
            foreach (int controller in _detailsContainer.GetAvailableControllers())
            {
                string deviceId = HassUniqueIdBuilder.GetControllerDeviceId(controller);
                const string entityId = "controller";

                _hassMqttManager.ConfigureSensor<MqttSensor>(deviceId, entityId)
                    .ConfigureTopics(HassTopicKind.State, HassTopicKind.JsonAttributes)
                    .ConfigureDevice(device =>
                    {
                        if (!device.Identifiers.Contains(deviceId))
                            device.Identifiers.Add(deviceId);

                        device.Name = $"Uponor Controller {controller}";
                        device.Manufacturer = "Uponor";
                        device.ViaDevice = uHomeDeviceId;
                    })
                    .ConfigureAliveService();
            }

            // Outdoor sensors
            foreach (int controller in _detailsContainer.GetAvailableOutdoorSensors())
            {
                string controllerId = HassUniqueIdBuilder.GetControllerDeviceId(controller);
                const string entityId = "outdoor_sensor";

                _hassMqttManager.ConfigureSensor<MqttSensor>(controllerId, entityId)
                    .ConfigureTopics(HassTopicKind.State)
                    .ConfigureDevice(device =>
                    {
                        if (!device.Identifiers.Contains(controllerId))
                            device.Identifiers.Add(controllerId);

                        device.Name = $"Uponor Controller {controller}";
                        device.Manufacturer = "Uponor";
                        device.ViaDevice = controllerId;
                    })
                    .ConfigureDiscovery(discovery =>
                    {
                        discovery.Name = $"Controller {controller} Outdoor Sensor";
                        discovery.DeviceClass = HassSensorDeviceClass.Temperature;
                        discovery.UnitOfMeasurement = "°C";
                        discovery.StateClass = HassStateClass.Measurement;
                    })
                    .ConfigureAliveService();
            }

            // Thermostats
            void SetThermostatDeviceInfo<TEntity>(IDiscoveryDocumentBuilder<TEntity> builder, string name, string deviceId, string controllerId) where TEntity : IHassDiscoveryDocument
            {
                builder.ConfigureDevice(device =>
                {
                    if (!device.Identifiers.Contains(deviceId))
                        device.Identifiers.Add(deviceId);

                    device.Name = name;
                    device.Manufacturer = "Uponor";
                    device.ViaDevice = controllerId;
                });
            }

            foreach ((int controller, int thermostat) in _detailsContainer.GetAvailableThermostats())
            {
                string controllerId = HassUniqueIdBuilder.GetControllerDeviceId(controller);
                string deviceId = HassUniqueIdBuilder.GetThermostatDeviceId(controller, thermostat);

                // Name
                string deviceName = $"Thermostat {controller}.{thermostat}";
                if (values.TryGetValue(UponorObjects.Thermostat(UponorThermostats.RoomName, controller, thermostat),
                    UponorProperties.Value, out string stringVal) && !string.IsNullOrWhiteSpace(stringVal))
                    deviceName = stringVal;

                // Climate
                IDiscoveryDocumentBuilder<MqttClimate> climateBuilder = _hassMqttManager.ConfigureSensor<MqttClimate>(deviceId, "temp")
                    .ConfigureTopics(HassTopicKind.JsonAttributes)
                    .ConfigureTopics(HassTopicKind.CurrentTemperature, HassTopicKind.Action, HassTopicKind.ModeState)
                    .ConfigureTopics(HassTopicKind.TemperatureCommand, HassTopicKind.TemperatureState)
                    .ConfigureDiscovery(discovery =>
                    {
                        discovery.Name = "Thermostat";
                        discovery.Precision = 0.1f;
                        discovery.TempStep = 0.5f;
                    })
                    .ConfigureAliveService();

                SetThermostatDeviceInfo(climateBuilder, deviceName, deviceId, controllerId);

                // Hacks: HASS has an odd way of determining what Climate devices do. 
                // With HASS, the mode of the device is what the device is set to do. Ie, in a heating-only climate system, they will _always_ be heating
                // While I prefer that the device is shown as what it's currently doing, given my "auto" settings.
                switch (_operationConfig.OperationMode)
                {
                    case OperationMode.Normal:
                        climateBuilder.Discovery.Modes = new[] { "auto" };
                        break;
                    case OperationMode.ModeWorkaround:
                        climateBuilder.Discovery.Modes = new[] { "off", _detailsContainer.HcMode == HcMode.Heating ? "heat" : "cool" };
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (values.TryGetValue(UponorObjects.Thermostat(UponorThermostats.MinSetpoint, controller, thermostat),
                    UponorProperties.Value, out float floatVal))
                    climateBuilder.Discovery.MinTemp = floatVal;

                if (values.TryGetValue(UponorObjects.Thermostat(UponorThermostats.MaxSetpoint, controller, thermostat),
                    UponorProperties.Value, out floatVal))
                    climateBuilder.Discovery.MaxTemp = floatVal;

                // Temperature
                IDiscoveryDocumentBuilder<MqttSensor> sensorBuilder = _hassMqttManager.ConfigureSensor<MqttSensor>(deviceId, "temperature")
                    .ConfigureTopics(HassTopicKind.State)
                    .ConfigureDiscovery(discovery =>
                    {
                        discovery.Name = "Temperature";
                        discovery.DeviceClass = HassSensorDeviceClass.Temperature;
                        discovery.UnitOfMeasurement = "°C";
                        discovery.StateClass = HassStateClass.Measurement;
                    })
                    .ConfigureAliveService();

                SetThermostatDeviceInfo(sensorBuilder, deviceName, deviceId, controllerId);

                // Humidity
                sensorBuilder = _hassMqttManager.ConfigureSensor<MqttSensor>(deviceId, "humidity")
                    .ConfigureTopics(HassTopicKind.State)
                    .ConfigureDiscovery(discovery =>
                    {
                        discovery.Name = "Humidity";
                        discovery.DeviceClass = HassSensorDeviceClass.Humidity;
                        discovery.UnitOfMeasurement = "%";
                        discovery.StateClass = HassStateClass.Measurement;
                    })
                    .ConfigureAliveService();

                SetThermostatDeviceInfo(sensorBuilder, deviceName, deviceId, controllerId);

                // Battery sensor
                sensorBuilder = _hassMqttManager.ConfigureSensor<MqttSensor>(deviceId, "battery")
                    .ConfigureTopics(HassTopicKind.State)
                    .ConfigureDiscovery(discovery =>
                    {
                        discovery.Name = "Battery";
                        discovery.DeviceClass = HassSensorDeviceClass.Battery;
                        discovery.UnitOfMeasurement = "%";
                    })
                    .ConfigureAliveService();

                SetThermostatDeviceInfo(sensorBuilder, deviceName, deviceId, controllerId);

                // Alarm sensor
                IDiscoveryDocumentBuilder<MqttBinarySensor> binarySensorBuilder = _hassMqttManager.ConfigureSensor<MqttBinarySensor>(deviceId, "alarms")
                    .ConfigureTopics(HassTopicKind.State, HassTopicKind.JsonAttributes)
                    .ConfigureDiscovery(discovery =>
                    {
                        discovery.Name = "Alarms";
                        discovery.DeviceClass = HassBinarySensorDeviceClass.Problem;
                    })
                    .ConfigureAliveService();

                SetThermostatDeviceInfo(binarySensorBuilder, deviceName, deviceId, controllerId);
            }
        }
    }
}