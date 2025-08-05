using System.IdentityModel.Tokens.Jwt;
using System.Text;

using CrowsNestMqtt.Utils;
using MQTTnet;

namespace CrowsNestMqtt.BusinessLogic.Services;

public class EnhancedAuthenticationHandler: IMqttEnhancedAuthenticationHandler
{
    private readonly MqttClientOptions _mqttClientOptions;

    public EnhancedAuthenticationHandler(MqttClientOptions mqttClientOptions)
    {
        _mqttClientOptions = mqttClientOptions;
    }

    public void Configure()
    {

        if (_mqttClientOptions.AuthenticationMethod == "K8S-SAT" && _mqttClientOptions.AuthenticationData.Length > 0)
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtTokenAsString = Encoding.UTF8.GetString(_mqttClientOptions.AuthenticationData);
            var jwtSecurityToken = handler.ReadJwtToken(jwtTokenAsString);

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

        _mqttClientOptions.EnhancedAuthenticationHandler = this;
    }

    public Task HandleEnhancedAuthenticationAsync(MqttEnhancedAuthenticationEventArgs eventArgs)
    {
        AppLogger.Information($"Enhanced Authentication: {eventArgs.ReasonString}");
        return Task.CompletedTask;
    }
}