using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MBW.HassMQTT;
using MBW.HassMQTT.CommonServices.AliveAndWill;
using MBW.HassMQTT.DiscoveryModels;
using MBW.HassMQTT.DiscoveryModels.Models;
using MBW.HassMQTT.Topics;
using MBW.Uponor2MQTT.Configuration;
using MBW.Uponor2MQTT.Features;
using MBW.Uponor2MQTT.HASS;
using MBW.UponorApi;
using MBW.UponorApi.Configuration;
using MBW.UponorApi.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HassDeviceClass = MBW.HassMQTT.DiscoveryModels.Enum.HassDeviceClass;

namespace MBW.Uponor2MQTT.Service
{
    internal class UponorDiscoveryService : BackgroundService
    {
        private readonly ILogger<UponorDiscoveryService> _logger;
        private readonly UponorOperationConfiguration _operationConfig;
        private readonly UhomeUponorClient _uponorClient;
        private readonly HassMqttTopicBuilder _topicBuilder;
        private readonly HassUniqueIdBuilder _uniqueIdBuilder;
        private readonly FeatureManager _featureManager;
        private readonly SystemDetailsContainer _detailsContainer;
        private readonly HassMqttManager _hassMqttManager;
        private readonly AvailabilityDecoratorService _availabilityDecorator;
        private readonly UponorConfiguration _config;

        public UponorDiscoveryService(
            ILogger<UponorDiscoveryService> logger,
            IOptions<UponorConfiguration> config,
            IOptions<UponorOperationConfiguration> operationConfig,
            FeatureManager featureManager,
            UhomeUponorClient uponorClient,
            HassMqttTopicBuilder topicBuilder,
            HassUniqueIdBuilder uniqueIdBuilder,
            SystemDetailsContainer detailsContainer,
            HassMqttManager hassMqttManager,
            AvailabilityDecoratorService availabilityDecorator)
        {
            _logger = logger;
            _operationConfig = operationConfig.Value;
            _uponorClient = uponorClient;
            _topicBuilder = topicBuilder;
            _uniqueIdBuilder = uniqueIdBuilder;
            _featureManager = featureManager;
            _detailsContainer = detailsContainer;
            _hassMqttManager = hassMqttManager;
            _availabilityDecorator = availabilityDecorator;
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
            _detailsContainer.Update(systemInfo.AvailableControllers, systemInfo.AvailableThermostats, systemInfo.HcMode);

            // Update system & controller details
            IEnumerable<int> objects = systemInfo.AvailableControllers.SelectMany(c => new[]
            {
                UponorObjects.System(UponorSystem.DeviceLostAlarm),
                UponorObjects.System(UponorSystem.NoCommController1),
                UponorObjects.System(UponorSystem.NoCommController2),
                UponorObjects.System(UponorSystem.NoCommController3),
                UponorObjects.System(UponorSystem.NoCommController4),
                UponorObjects.System(UponorSystem.UhomeModuleId),
                UponorObjects.System(UponorSystem.DeviceLostAlarm),
                UponorObjects.Controller(UponorController.ControllerSwVersion, c)
            });

            UponorResponseContainer values = await _uponorClient.ReadValues(objects, new[] { UponorProperties.Value }, stoppingToken);

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

            // Prepare thermostats
            objects = _detailsContainer.GetAvailableThermostats().SelectMany(c => new[]
            {
                UponorObjects.Thermostat(UponorThermostats.RoomName, c.controller, c.thermostat),
                UponorObjects.Thermostat(UponorThermostats.MinSetpoint, c.controller, c.thermostat),
                UponorObjects.Thermostat(UponorThermostats.MaxSetpoint, c.controller, c.thermostat)
            });

            UponorResponseContainer thermostatValues = await _uponorClient.ReadValues(objects, new[]
            {
                UponorProperties.Value
            }, stoppingToken);

            values.Merge(thermostatValues);

            return values;
        }

        private void CreateEntities(UponorResponseContainer values)
        {
            // System
            string uHomeDeviceId = _uniqueIdBuilder.GetUhomeId();

            {
                const string entityId = "uhome";
                MqttSensor sensor = _hassMqttManager.ConfigureDiscovery<MqttSensor>(uHomeDeviceId, entityId);

                sensor.Device.Name = "Uponor U@Home";
                sensor.Device.Identifiers = new[] { uHomeDeviceId };
                sensor.Device.Manufacturer = "Uponor";

                sensor.StateTopic = _topicBuilder.GetEntityTopic(uHomeDeviceId, entityId, "state");
                sensor.JsonAttributesTopic = _topicBuilder.GetAttributesTopic(uHomeDeviceId, entityId);

                _availabilityDecorator.ApplyAvailabilityInformation(sensor);
            }

            // Controllers
            foreach (int controller in _detailsContainer.GetAvailableControllers())
            {
                string deviceId = _uniqueIdBuilder.GetControllerId(controller);
                const string entityId = "controller";

                MqttSensor sensor = _hassMqttManager.ConfigureDiscovery<MqttSensor>(deviceId, entityId);

                sensor.Device.Name = $"Uponor Controller {controller}";
                sensor.Device.Identifiers = new[] { deviceId };
                sensor.Device.Manufacturer = "Uponor";
                sensor.Device.ViaDevice = uHomeDeviceId;

                sensor.StateTopic = _topicBuilder.GetEntityTopic(deviceId, entityId, "state");
                sensor.JsonAttributesTopic = _topicBuilder.GetAttributesTopic(deviceId, entityId);
                
                _availabilityDecorator.ApplyAvailabilityInformation(sensor);
            }

            // Thermostats
            void SetThermostatDeviceInfo(MqttDeviceDocument device, string name, string deviceId, string controllerId)
            {
                device.Name = name;
                device.Identifiers = new[] { deviceId };
                device.Manufacturer = "Uponor";
                device.ViaDevice = controllerId;
            }

            foreach ((int controller, int thermostat) in _detailsContainer.GetAvailableThermostats())
            {
                string controllerId = _uniqueIdBuilder.GetControllerId(controller);
                string deviceId = _uniqueIdBuilder.GetThermostatId(controller, thermostat);

                // Name
                string deviceName = $"Thermostat {controller}.{thermostat}";
                if (values.TryGetValue(UponorObjects.Thermostat(UponorThermostats.RoomName, controller, thermostat),
                    UponorProperties.Value, out string stringVal) && !string.IsNullOrWhiteSpace(stringVal))
                    deviceName = stringVal;

                // Climate
                MqttClimate climate = _hassMqttManager.ConfigureDiscovery<MqttClimate>(deviceId, "temp");
                SetThermostatDeviceInfo(climate.Device, deviceName, deviceId, controllerId);
                _availabilityDecorator.ApplyAvailabilityInformation(climate);

                climate.Name = $"{deviceName} Thermostat";

                climate.JsonAttributesTopic = _topicBuilder.GetAttributesTopic(deviceId, "temp");
                climate.CurrentTemperatureTopic = _topicBuilder.GetEntityTopic(deviceId, "temp", "state");
                climate.AwayModeStateTopic = _topicBuilder.GetEntityTopic(deviceId, "temp", "awaymode");
                climate.ActionTopic = _topicBuilder.GetEntityTopic(deviceId, "temp", "action");

                climate.ModeStateTopic = _topicBuilder.GetEntityTopic(deviceId, "temp", "mode");

                // Hacks: HASS has an odd way of determining what Climate devices do. 
                // With HASS, the mode of the device is what the device is set to do. Ie, in a heating-only climate system, they will _always_ be heating
                // While I prefer that the device is shown as what it's currently doing, given my "auto" settings.
                switch (_operationConfig.OperationMode)
                {
                    case OperationMode.Normal:
                        climate.Modes = new[] { "auto" };
                        break;
                    case OperationMode.ModeWorkaround:
                        climate.Modes = new[] { "off", _detailsContainer.HcMode == HcMode.Heating ? "heat" : "cool" };
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                climate.TemperatureCommandTopic = _topicBuilder.GetEntityTopic(deviceId, "temp", "set_setpoint");
                climate.TemperatureStateTopic = _topicBuilder.GetEntityTopic(deviceId, "temp", "setpoint");
                climate.Precision = 0.1f;
                climate.TempStep = 0.5f;

                if (values.TryGetValue(UponorObjects.Thermostat(UponorThermostats.MinSetpoint, controller, thermostat),
                    UponorProperties.Value, out float floatVal))
                    climate.MinTemp = floatVal;

                if (values.TryGetValue(UponorObjects.Thermostat(UponorThermostats.MaxSetpoint, controller, thermostat),
                    UponorProperties.Value, out floatVal))
                    climate.MaxTemp = floatVal;

                // Humidity
                MqttSensor sensor = _hassMqttManager.ConfigureDiscovery<MqttSensor>(deviceId, "humidity");
                SetThermostatDeviceInfo(sensor.Device, deviceName, deviceId, controllerId);
                _availabilityDecorator.ApplyAvailabilityInformation(sensor);

                sensor.Name = $"{deviceName} Humidity";
                sensor.DeviceClass = HassDeviceClass.Humidity;
                sensor.UnitOfMeasurement = "%";

                sensor.StateTopic = _topicBuilder.GetEntityTopic(deviceId, "humidity", "state");
                sensor.JsonAttributesTopic = _topicBuilder.GetAttributesTopic(deviceId, "humidity");

                // Battery sensor
                sensor = _hassMqttManager.ConfigureDiscovery<MqttSensor>(deviceId, "battery");
                SetThermostatDeviceInfo(sensor.Device, deviceName, deviceId, controllerId);
                _availabilityDecorator.ApplyAvailabilityInformation(sensor);

                sensor.Name = $"{deviceName} Battery";
                sensor.DeviceClass = HassDeviceClass.Battery;
                sensor.UnitOfMeasurement = "%";

                sensor.StateTopic = _topicBuilder.GetEntityTopic(deviceId, "battery", "state");
                sensor.JsonAttributesTopic = _topicBuilder.GetAttributesTopic(deviceId, "battery");

                // Alarm sensor
                MqttBinarySensor binarySensor = _hassMqttManager.ConfigureDiscovery<MqttBinarySensor>(deviceId, "alarms");
                SetThermostatDeviceInfo(binarySensor.Device, deviceName, deviceId, controllerId);
                _availabilityDecorator.ApplyAvailabilityInformation(binarySensor);

                binarySensor.Name = $"{deviceName} Alarms";
                binarySensor.DeviceClass = HassDeviceClass.Problem;

                binarySensor.StateTopic = _topicBuilder.GetEntityTopic(deviceId, "alarms", "state");
                binarySensor.JsonAttributesTopic = _topicBuilder.GetAttributesTopic(deviceId, "alarms");
            }
        }
    }
}