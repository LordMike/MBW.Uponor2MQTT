# Uponor2MQTT
[![docker hub](https://img.shields.io/docker/pulls/lordmike/uponor2mqtt)](https://hub.docker.com/repository/docker/lordmike/uponor2mqtt)

This is a proxy application to translate the status of a U@Home Uponor device, to Home Assistant using MQTT. You can run this application in docker, and it will periodically poll the Uponor API for updates.

_This project is not affiliated with or endorsed by Uponor._

# Features

* Creates binary sensors indicating issues with this service, or the U@Home webserver
* Creates sensors for each Uponor Controller
* Creates sensors for each Thermostat
  * Tracks temperature, humidity, battery levels and other alarms
* Write support
  * Setting the Setpoint (target temperature)
  * Setting room names for each thermostat

# Setup

## Environment Variables

| Name | Required | Default | Note |
|---|---|---|---|
| MQTT__Server | yes | | A hostname or IP address |
| MQTT__Port | | 1883 | |
| MQTT__Username | | | |
| MQTT__Password | | | |
| MQTT__ClientId | | `uponor2mqtt` | |
| MQTT__ReconnectInterval | | `00:00:30` | How long to wait before reconnecting to MQTT |
| HASS__DiscoveryPrefix | | `homeassistant` | Prefix of HASS discovery topics |
| HASS__TopicPrefix | | `uponor2mqtt` | Prefix of state and attribute topics |
| Uponor__Host | yes | | |
| Uponor__UpdateInterval | | 00:00:30 | Update interval, default: `30 seconds` |
| Uponor__DiscoveryInterval | | 01:00:00 | Discovery interval, default: `1 hour` |
| Uponor__OperationMode | | Normal | Override how climate "modes" are shown, by setting this to "ModeWorkaround". Lets HASS show heating thermostats as orange. |
| Proxy__Uri | | | Set this to pass U@Home API calls through an HTTP proxy |

# Docker images

## Run in Docker CLI

> docker run -d -e MQTT__Server=myqueue.local -e Uponor__Host=myhost lordmike/uponor2mqtt:latest

## Run in Docker Compose

```yaml
# docker-compose.yml
version: '2.3'

services:
  uponor2mqtt:
    image: lordmike/uponor2mqtt:latest
    environment:
      MQTT__Server: myqueue.local
      Uponor__Host: myhost
```

## Available tags

You can use one of the following tags. Architectures available: `amd64`, `armv7` and `aarch64`

* `latest` (latest, multi-arch)
* `ARCH-latest` (latest, specific architecture)
* `vA.B.C` (specific version, multi-arch)
* `ARCH-vA.B.C` (specific version, specific architecture)

For all available tags, see [Docker Hub](https://hub.docker.com/repository/docker/lordmike/uponor2mqtt/tags).


# How

Officially, Uponor U@Home does _not_ have any API available. They provide a simple website that you can access, in addition to a mobile app. The website does not provide a lot of functionality, beyond reporting alarms and setting target temperatures. I found this to be lacking for me, especially given the pricepoint of the system.

This API is reverse engineered using experimentation and other [prior efforts](https://github.com/dave-code-ruiz/uhomeuponor).

# Troubleshooting

## Log level

Adjust the logging level using this environment variable:

> Logging__MinimumLevel__Default: Error | Warning | Information | Debug | Verbose

## HTTP Requests logging

Since this is a reverse engineering effort, sometimes things go wrong. To aid in troubleshooting, the requests and responses from the U@Home API can be dumped to the console, by enabling trace logging.

Enable request logging with this environment variable:
> Logging__MinimumLevel__Override__MBW.Client.Uponor: Verbose
