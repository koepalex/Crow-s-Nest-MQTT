using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using CrowsNestMqtt.BusinessLogic.Services;
using CrowsNestMqtt.Utils;
using Microsoft.IdentityModel.Tokens;
using MQTTnet;
using MQTTnet.Packets;
using Xunit;

namespace CrowsNestMqtt.UnitTests.Services;

public class EnhancedAuthenticationHandlerTests
{
    private string GenerateTestToken(DateTime expiry)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes("a-super-secret-key-that-is-long-enough");
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] { new Claim("name", "testuser") }),
            NotBefore = expiry.AddMinutes(-30),
            Expires = expiry,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    [Fact]
    public void ConnectAsync_WithValidToken_LogsInformation()
    {
        // Arrange
        var validToken = GenerateTestToken(DateTime.UtcNow.AddHours(1));
        var userProperties = new MqttUserProperty[] { new("token", validToken) };
        var options = new MqttClientOptions { UserProperties = userProperties.ToList() };
        var handler = new EnhancedAuthenticationHandler(options);
        
        var logMessages = new System.Collections.Generic.List<string>();
        AppLogger.OnLogMessage += (level, message) => { if (level == "Information") logMessages.Add(message); };

        // Act
        handler.Configure();

        // Assert
        Assert.Contains(logMessages, log => log.Contains("Enhanced Authentication: Token is valid"));
    }

    [Fact]
    public async Task ConnectAsync_WithExpiredToken_LogsWarning()
    {
        // Arrange
        var expiredToken = GenerateTestToken(DateTime.UtcNow.AddHours(-1));
        var userProperties = new MqttUserProperty[] { new("token", expiredToken) };
        var options = new MqttClientOptions { UserProperties = userProperties.ToList() };
        var handler = new EnhancedAuthenticationHandler(options);

        var logMessages = new System.Collections.Generic.List<string>();
        AppLogger.OnLogMessage += (level, message) => { if (level == "Warning") logMessages.Add(message); };

        // Act
        await handler.Configure();

        // Assert
        Assert.Contains(logMessages, log => log.Contains("Enhanced Authentication: Token has expired"));
    }

    [Fact]
    public async Task ConnectAsync_WithMissingToken_LogsWarning()
    {
        // Arrange
        var options = new MqttClientOptions(); // No user properties
        var handler = new EnhancedAuthenticationHandler(options);
        
        var logMessages = new System.Collections.Generic.List<string>();
        AppLogger.OnLogMessage += (level, message) => { if (level == "Warning") logMessages.Add(message); };

        // Act
        await handler.Configure();

        // Assert
        Assert.Contains(logMessages, log => log.Contains("Enhanced Authentication: Token not found"));
    }
    
    [Fact]
    public async Task ConnectAsync_WithEmptyToken_LogsWarning()
    {
        // Arrange
        var userProperties = new MqttUserProperty[] { new("token", "") };
        var options = new MqttClientOptions { UserProperties = userProperties.ToList() };
        var handler = new EnhancedAuthenticationHandler(options);

        var logMessages = new System.Collections.Generic.List<string>();
        AppLogger.OnLogMessage += (level, message) => { if (level == "Warning") logMessages.Add(message); };

        // Act
        await handler.Configure();

        // Assert
        Assert.Contains(logMessages, log => log.Contains("Enhanced Authentication: Token not found"));
    }
}