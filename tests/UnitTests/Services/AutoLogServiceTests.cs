using System.Text;
using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.BusinessLogic.Configuration;
using CrowsNestMqtt.BusinessLogic.Services;
using Microsoft.Data.Sqlite;
using MQTTnet;
using MQTTnet.Protocol;
using Xunit;

namespace CrowsNestMqtt.UnitTests.Services;

public class AutoLogServiceTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "crowsnest-autolog-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void GetTableNameForTopic_ReturnsStableReadableName()
    {
        var tableName = AutoLogService.GetTableNameForTopic("factory/line1/temperature");

        Assert.StartsWith("mqtt_factory_line1_temperature_", tableName);
        Assert.Equal(tableName, AutoLogService.GetTableNameForTopic("factory/line1/temperature"));
    }

    [Theory]
    [InlineData("sensors/room1/temp", "sensors/#", true)]
    [InlineData("sensors/room1/temp", "sensors/+/temp", true)]
    [InlineData("sensors/room1/humidity", "sensors/+/temp", false)]
    public void MatchTopic_UsesMqttWildcardSemantics(string topic, string filter, bool expected)
    {
        Assert.Equal(expected, AutoLogService.MatchTopic(topic, filter) >= 0);
    }

    [Fact]
    public async Task LogBatchAsync_JsonPayload_CreatesTopicTableWithGeneratedJsonColumns()
    {
        Directory.CreateDirectory(_tempDirectory);
        using var service = new AutoLogService();
        service.UpdateConfiguration(
            _tempDirectory,
            new[] { new AutoLogTopicRule("sensors/#") },
            100 * 1024 * 1024);

        var message = new MqttApplicationMessageBuilder()
            .WithTopic("sensors/room1/temp")
            .WithPayload("{\"temperature\":23.5,\"unit\":\"c\"}")
            .WithContentType("application/json")
            .WithPayloadFormatIndicator(MqttPayloadFormatIndicator.CharacterData)
            .WithUserProperty("source", Encoding.UTF8.GetBytes("test"))
            .Build();

        var args = new IdentifiedMqttApplicationMessageReceivedEventArgs(Guid.NewGuid(), message, "client-1");

        await service.LogBatchAsync(new[] { args });

        var databasePath = Path.Combine(_tempDirectory, "crowsnest-auto-log.sqlite");
        Assert.True(File.Exists(databasePath));

        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();

        var tableName = AutoLogService.GetTableNameForTopic("sensors/room1/temp");
        await using (var mapping = connection.CreateCommand())
        {
            mapping.CommandText = "SELECT table_name, source_filter FROM _auto_log_topics WHERE topic = 'sensors/room1/temp';";
            await using var reader = await mapping.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(tableName, reader.GetString(0));
            Assert.Equal("sensors/#", reader.GetString(1));
        }

        await using (var query = connection.CreateCommand())
        {
            query.CommandText = $"SELECT json_temperature, json_unit, json_extract(user_properties_json, '$.source') FROM \"{tableName}\";";
            await using var reader = await query.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("23.5", reader.GetString(0));
            Assert.Equal("c", reader.GetString(1));
            Assert.Equal("test", reader.GetString(2));
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
