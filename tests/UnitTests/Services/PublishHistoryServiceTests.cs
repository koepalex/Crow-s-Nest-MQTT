using System.Text;
using Xunit;
using CrowsNestMqtt.BusinessLogic.Models;
using CrowsNestMqtt.BusinessLogic.Services;
using MQTTnet.Packets;
using MQTTnet.Protocol;

namespace UnitTests.Services;

public class PublishHistoryServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _historyFilePath;

    public PublishHistoryServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "CrowsNestMqtt_HistoryTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _historyFilePath = Path.Combine(_testDir, "test-history.json");
    }

    private PublishHistoryService CreateService()
    {
        return new PublishHistoryService(_historyFilePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    private MqttPublishRequest CreateTestRequest(string topic = "test/topic", string payload = "hello")
    {
        return new MqttPublishRequest
        {
            Topic = topic,
            PayloadText = payload,
            QoS = MqttQualityOfServiceLevel.AtLeastOnce,
            Retain = false
        };
    }

    [Fact]
    public void AddEntry_SingleEntry_AppearsInHistory()
    {
        var service = CreateService();

        service.AddEntry(CreateTestRequest());

        var history = service.GetHistory();
        Assert.Single(history);
        Assert.Equal("test/topic", history[0].Topic);
        Assert.Equal("hello", history[0].PayloadText);
    }

    [Fact]
    public void AddEntry_MultipleEntries_MostRecentFirst()
    {
        var service = CreateService();

        service.AddEntry(CreateTestRequest("topic/first", "first"));
        service.AddEntry(CreateTestRequest("topic/second", "second"));
        service.AddEntry(CreateTestRequest("topic/third", "third"));

        var history = service.GetHistory();
        Assert.Equal(3, history.Count);
        Assert.Equal("topic/third", history[0].Topic);
        Assert.Equal("topic/second", history[1].Topic);
        Assert.Equal("topic/first", history[2].Topic);
    }

    [Fact]
    public void AddEntry_ExceedsMaxEntries_OldestRemoved()
    {
        var service = CreateService();

        for (int i = 0; i < 51; i++)
        {
            service.AddEntry(CreateTestRequest($"topic/{i}", $"payload-{i}"));
        }

        var history = service.GetHistory();
        Assert.Equal(50, history.Count);
        // Most recent (i=50) should be first
        Assert.Equal("topic/50", history[0].Topic);
        // Oldest surviving entry should be i=1 (i=0 was evicted)
        Assert.Equal("topic/1", history[49].Topic);
    }

    [Fact]
    public void GetHistory_EmptyHistory_ReturnsEmptyList()
    {
        var service = CreateService();

        var history = service.GetHistory();

        Assert.NotNull(history);
        Assert.Empty(history);
    }

    [Fact]
    public void ClearHistory_ClearsAllEntries()
    {
        var service = CreateService();
        service.AddEntry(CreateTestRequest("a", "1"));
        service.AddEntry(CreateTestRequest("b", "2"));

        service.ClearHistory();

        var history = service.GetHistory();
        Assert.Empty(history);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesEntries()
    {
        var service = CreateService();
        service.AddEntry(CreateTestRequest("round/trip", "test-payload"));
        await service.SaveAsync();

        var loadedService = CreateService();
        await loadedService.LoadAsync();

        var history = loadedService.GetHistory();
        Assert.Single(history);
        Assert.Equal("round/trip", history[0].Topic);
        Assert.Equal("test-payload", history[0].PayloadText);
    }

    [Fact]
    public void AddEntry_PreservesAllV5Properties()
    {
        var service = CreateService();
        var correlationData = new byte[] { 0x01, 0x02, 0x03 };

        var request = new MqttPublishRequest
        {
            Topic = "v5/topic",
            PayloadText = "v5 payload",
            QoS = MqttQualityOfServiceLevel.ExactlyOnce,
            Retain = true,
            ContentType = "application/json",
            PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData,
            ResponseTopic = "response/topic",
            CorrelationData = correlationData,
            MessageExpiryInterval = 3600,
            UserProperties = new List<MqttUserProperty>
            {
                new("key1", Encoding.UTF8.GetBytes("value1")),
                new("key2", Encoding.UTF8.GetBytes("value2"))
            }
        };

        service.AddEntry(request);

        var history = service.GetHistory();
        Assert.Single(history);
        var entry = history[0];
        Assert.Equal("v5/topic", entry.Topic);
        Assert.Equal("v5 payload", entry.PayloadText);
        Assert.Equal((int)MqttQualityOfServiceLevel.ExactlyOnce, entry.QoS);
        Assert.True(entry.Retain);
        Assert.Equal("application/json", entry.ContentType);
        Assert.Equal((int)MqttPayloadFormatIndicator.CharacterData, entry.PayloadFormatIndicator);
        Assert.Equal("response/topic", entry.ResponseTopic);
        Assert.Equal(Convert.ToHexString(correlationData), entry.CorrelationDataHex);
        Assert.Equal(3600u, entry.MessageExpiryInterval);
        Assert.Equal(2, entry.UserProperties.Count);
        Assert.Equal("value1", entry.UserProperties["key1"]);
        Assert.Equal("value2", entry.UserProperties["key2"]);
    }

    [Fact]
    public async Task LoadAsync_NonExistentFile_DoesNotThrow()
    {
        var nonExistentPath = Path.Combine(_testDir, "does-not-exist.json");
        var service = new PublishHistoryService(nonExistentPath);

        var exception = await Record.ExceptionAsync(() => service.LoadAsync());

        Assert.Null(exception);
        Assert.Empty(service.GetHistory());
    }
}
