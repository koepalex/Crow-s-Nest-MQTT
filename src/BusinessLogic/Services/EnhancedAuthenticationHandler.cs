using System.IdentityModel.Tokens.Jwt;
using System.Text;

using CrowsNestMqtt.BusinessLogic.Configuration;
using CrowsNestMqtt.Utils;
using MQTTnet;
using MQTTnet.Protocol;

namespace CrowsNestMqtt.BusinessLogic.Services;

public class EnhancedAuthenticationHandler: IMqttEnhancedAuthenticationHandler
{
    private readonly MqttClientOptions _mqttClientOptions;
    private readonly IAccessTokenProvider? _accessTokenProvider;

    public EnhancedAuthenticationHandler(MqttClientOptions mqttClientOptions)
        : this(mqttClientOptions, null)
    {
    }

    /// <summary>
    /// Creates a handler that can refresh OAuth tokens on demand using the provided
    /// access-token provider. Required when the connection uses
    /// <see cref="AzureAuthenticationMode.AuthenticationMethod"/> ("OAUTH2-JWT").
    /// </summary>
    public EnhancedAuthenticationHandler(
        MqttClientOptions mqttClientOptions,
        IAccessTokenProvider? accessTokenProvider)
    {
        _mqttClientOptions = mqttClientOptions;
        _accessTokenProvider = accessTokenProvider;
    }

    public void Configure()
    {
        if (_mqttClientOptions.AuthenticationMethod == "K8S-SAT" && _mqttClientOptions.AuthenticationData.Length > 0)
        {
            LogJwtValidity(_mqttClientOptions.AuthenticationData, "K8S-SAT");
        }
        else if (_mqttClientOptions.AuthenticationMethod == AzureAuthenticationMode.AuthenticationMethod
                 && _mqttClientOptions.AuthenticationData.Length > 0)
        {
            LogJwtValidity(_mqttClientOptions.AuthenticationData, AzureAuthenticationMode.AuthenticationMethod);
        }
        else
        {
            AppLogger.Warning("Enhanced Authentication: Token not found in UserProperties.");
        }

        _mqttClientOptions.EnhancedAuthenticationHandler = this;
    }

    public async Task HandleEnhancedAuthenticationAsync(MqttEnhancedAuthenticationEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        AppLogger.Information(
            $"Enhanced Authentication: server-initiated AUTH packet received (method={eventArgs.AuthenticationMethod}, reason={eventArgs.ReasonCode}).");

        if (eventArgs.AuthenticationMethod == AzureAuthenticationMode.AuthenticationMethod
            && _accessTokenProvider is not null)
        {
            try
            {
                var token = await _accessTokenProvider
                    .GetTokenAsync(eventArgs.CancellationToken)
                    .ConfigureAwait(false);

                var tokenBytes = Encoding.UTF8.GetBytes(token.Token);
                await eventArgs
                    .SendAsync(
                        new SendMqttEnhancedAuthenticationDataOptions
                        {
                            Data = tokenBytes,
                        },
                        eventArgs.CancellationToken)
                    .ConfigureAwait(false);

                AppLogger.Information(
                    $"Enhanced Authentication: refreshed OAuth token sent to broker (new expiry {token.ExpiresOn:O}).");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "Enhanced Authentication: failed to refresh OAuth token.");
            }
        }
    }

    private static void LogJwtValidity(byte[] data, string method)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtTokenAsString = Encoding.UTF8.GetString(data);
            var jwtSecurityToken = handler.ReadJwtToken(jwtTokenAsString);

            var now = DateTime.UtcNow;
            if (jwtSecurityToken.ValidTo > now)
            {
                AppLogger.Information(
                    $"Enhanced Authentication ({method}): Token is valid until {jwtSecurityToken.ValidTo:O}.");
            }
            else
            {
                AppLogger.Warning(
                    $"Enhanced Authentication ({method}): Token has expired on {jwtSecurityToken.ValidTo:O}.");
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warning(
                ex,
                $"Enhanced Authentication ({method}): Failed to parse token as JWT.");
        }
    }
}