using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;
using CrowsNestMqtt.Utils;
using MQTTnet;
using MQTTnet.Packets;

namespace CrowsNestMqtt.BusinessLogic.Services;

public class EnhancedAuthenticationHandler
{
    private readonly MqttClientOptions _mqttClientOptions;

    public EnhancedAuthenticationHandler(MqttClientOptions mqttClientOptions)
    {
        _mqttClientOptions = mqttClientOptions;
    }

    public Task ConnectAsync()
    {
        var tokenProperty = _mqttClientOptions.UserProperties?.FirstOrDefault(p => p.Name == "token");
        if (tokenProperty != null && !string.IsNullOrEmpty(tokenProperty.Value))
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtSecurityToken = handler.ReadJwtToken(tokenProperty.Value);

            var now = System.DateTime.UtcNow;
            if (jwtSecurityToken.ValidTo > now)
            {
                AppLogger.Information($"Enhanced Authentication: Token is valid until {jwtSecurityToken.ValidTo}.");
            }
            else
            {
                AppLogger.Warning($"Enhanced Authentication: Token has expired on {jwtSecurityToken.ValidTo}.");
            }
        }
        else
        {
            AppLogger.Warning("Enhanced Authentication: Token not found in UserProperties.");
        }

        // This is a placeholder for the actual connection logic.
        // The real implementation will be added in a future step.
        return Task.CompletedTask;
    }
}