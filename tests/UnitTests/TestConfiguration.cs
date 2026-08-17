using Microsoft.Extensions.Configuration;

namespace CrowsNestMqtt.UnitTests
{
    internal static class TestConfiguration
    {
        private static readonly IConfiguration _configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.test.json", optional: false, reloadOnChange: true)
            .Build();

        public static string MqttHostname => _configuration["TestMqttBroker:Hostname"] ?? "localhost";
        public static int MqttPort => int.Parse(_configuration["TestMqttBroker:Port"] ?? "1883", System.Globalization.CultureInfo.InvariantCulture);
    }
}
