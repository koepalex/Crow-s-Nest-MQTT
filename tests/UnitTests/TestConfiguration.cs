using Microsoft.Extensions.Configuration;

namespace CrowsNestMqtt.UnitTests
{
    public static class TestConfiguration
    {
        private static readonly IConfiguration _configuration;
        
        static TestConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.test.json", optional: false, reloadOnChange: true);
            
            _configuration = builder.Build();
        }
        
        public static string MqttHostname => _configuration["TestMqttBroker:Hostname"] ?? "localhost";
        public static int MqttPort => int.Parse(_configuration["TestMqttBroker:Port"] ?? "1883");
    }
}
