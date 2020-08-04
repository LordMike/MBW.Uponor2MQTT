﻿using System;

namespace MBW.Uponor2MQTT.Configuration
{
    internal class MqttConfiguration
    {
        public string Server { get; set; } = "localhost";

        public int Port { get; set; } = 1883;

        public string Username { get; set; }

        public string Password { get; set; }

        public string ClientId { get; set; } = "uponor2mqtt";

        public TimeSpan ReconnectInterval { get; set; } = TimeSpan.FromSeconds(30);

        public TimeSpan? KeepAlivePeriod { get; set; }
    }
}