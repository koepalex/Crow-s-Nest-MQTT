using System.Text;
using CrowsNestMqtt.BusinessLogic.Models;
using MQTTnet.Protocol;
using Xunit;

namespace CrowsNestMqtt.UnitTests.BusinessLogic;

public class MqttPublishRequestTests
{
    [Fact]
    public void GetEffectivePayload_BinaryPayloadSet_ReturnsBinaryPayload()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var req = new MqttPublishRequest
        {
            Topic = "t",
            Payload = bytes,
            PayloadText = "should-be-ignored"
        };

        Assert.Same(bytes, req.GetEffectivePayload());
    }

    [Fact]
    public void GetEffectivePayload_OnlyTextSet_ReturnsUtf8Encoded()
    {
        var req = new MqttPublishRequest
        {
            Topic = "t",
            PayloadText = "héllo"
        };

        Assert.Equal(Encoding.UTF8.GetBytes("héllo"), req.GetEffectivePayload());
    }

    [Fact]
    public void GetEffectivePayload_NothingSet_ReturnsEmptyArray()
    {
        var req = new MqttPublishRequest { Topic = "t" };

        var result = req.GetEffectivePayload();
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Defaults_AreAsDocumented()
    {
        var req = new MqttPublishRequest { Topic = "t" };

        Assert.Equal(MqttQualityOfServiceLevel.AtLeastOnce, req.QoS);
        Assert.False(req.Retain);
        Assert.Equal(MqttPayloadFormatIndicator.Unspecified, req.PayloadFormatIndicator);
        Assert.Equal(0u, req.MessageExpiryInterval);
        Assert.NotNull(req.UserProperties);
        Assert.Empty(req.UserProperties);
        Assert.True((DateTime.UtcNow - req.Timestamp).TotalSeconds < 5);
    }

    [Fact]
    public void Mqtt5Properties_RoundTripViaInit()
    {
        var correlation = new byte[] { 9, 9 };
        var req = new MqttPublishRequest
        {
            Topic = "req/topic",
            PayloadText = "{}",
            QoS = MqttQualityOfServiceLevel.ExactlyOnce,
            Retain = true,
            ContentType = "application/json",
            PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData,
            ResponseTopic = "resp/topic",
            CorrelationData = correlation,
            MessageExpiryInterval = 60u,
        };

        Assert.Equal("req/topic", req.Topic);
        Assert.Equal(MqttQualityOfServiceLevel.ExactlyOnce, req.QoS);
        Assert.True(req.Retain);
        Assert.Equal("application/json", req.ContentType);
        Assert.Equal(MqttPayloadFormatIndicator.CharacterData, req.PayloadFormatIndicator);
        Assert.Equal("resp/topic", req.ResponseTopic);
        Assert.Same(correlation, req.CorrelationData);
        Assert.Equal(60u, req.MessageExpiryInterval);
    }
}
