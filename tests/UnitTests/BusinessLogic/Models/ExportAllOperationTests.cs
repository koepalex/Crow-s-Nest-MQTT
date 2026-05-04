namespace CrowsNestMqtt.UnitTests.BusinessLogic.Models;

using CrowsNestMqtt.BusinessLogic.Exporter;
using CrowsNestMqtt.BusinessLogic.Models;
using Xunit;

public class ExportAllOperationTests
{
    #region Create - Valid Inputs

    [Fact]
    public void Create_WithValidInputs_ReturnsCorrectOperation()
    {
        var result = ExportAllOperation.Create("sensor/temp", 50, ExportTypes.json, @"C:\export\data.json");

        Assert.Equal("sensor/temp", result.TopicName);
        Assert.Equal(50, result.MessageCount);
        Assert.Equal(50, result.ExportedCount);
        Assert.Equal(ExportTypes.json, result.ExportFormat);
        Assert.Equal(@"C:\export\data.json", result.OutputFilePath);
        Assert.True(result.Timestamp <= DateTime.UtcNow);
    }

    [Fact]
    public void Create_WithTotalMessagesAbove100_CapsExportedCountAt100()
    {
        var result = ExportAllOperation.Create("topic/test", 500, ExportTypes.txt, "output.txt");

        Assert.Equal(500, result.MessageCount);
        Assert.Equal(100, result.ExportedCount);
    }

    [Fact]
    public void Create_WithTotalMessagesExactly100_SetsExportedCountTo100()
    {
        var result = ExportAllOperation.Create("topic/test", 100, ExportTypes.json, "output.json");

        Assert.Equal(100, result.MessageCount);
        Assert.Equal(100, result.ExportedCount);
    }

    [Fact]
    public void Create_WithZeroMessages_SetsExportedCountToZero()
    {
        var result = ExportAllOperation.Create("topic/test", 0, ExportTypes.json, "output.json");

        Assert.Equal(0, result.MessageCount);
        Assert.Equal(0, result.ExportedCount);
    }

    #endregion

    #region Create - Invalid Inputs

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrEmptyTopicName_ThrowsArgumentException(string? topicName)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ExportAllOperation.Create(topicName!, 10, ExportTypes.json, "output.json"));

        Assert.Equal("topicName", ex.ParamName);
    }

    [Fact]
    public void Create_WithNegativeTotalMessages_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ExportAllOperation.Create("topic", -1, ExportTypes.json, "output.json"));

        Assert.Equal("totalMessages", ex.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrEmptyOutputFilePath_ThrowsArgumentException(string? outputFilePath)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ExportAllOperation.Create("topic", 10, ExportTypes.json, outputFilePath!));

        Assert.Equal("outputFilePath", ex.ParamName);
    }

    #endregion

    #region IsLimitExceeded

    [Theory]
    [InlineData(101)]
    [InlineData(200)]
    [InlineData(1000)]
    public void IsLimitExceeded_WhenMessageCountAbove100_ReturnsTrue(int messageCount)
    {
        var operation = ExportAllOperation.Create("topic", messageCount, ExportTypes.json, "output.json");

        Assert.True(operation.IsLimitExceeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void IsLimitExceeded_WhenMessageCountAtOrBelow100_ReturnsFalse(int messageCount)
    {
        var operation = ExportAllOperation.Create("topic", messageCount, ExportTypes.json, "output.json");

        Assert.False(operation.IsLimitExceeded);
    }

    #endregion

    #region GetStatusMessage

    [Fact]
    public void GetStatusMessage_WhenLimitExceeded_ReturnsLimitEnforcedMessage()
    {
        var operation = ExportAllOperation.Create("topic", 200, ExportTypes.json, @"C:\exports\data.json");

        var message = operation.GetStatusMessage();

        Assert.Equal("Exported 100 of 200 messages to data.json (limit enforced)", message);
    }

    [Fact]
    public void GetStatusMessage_WhenNotLimited_ReturnsSimpleMessage()
    {
        var operation = ExportAllOperation.Create("topic", 42, ExportTypes.txt, @"C:\exports\data.txt");

        var message = operation.GetStatusMessage();

        Assert.Equal("Exported 42 messages to data.txt", message);
    }

    [Fact]
    public void GetStatusMessage_ExtractsFileNameFromFullPath()
    {
        var operation = ExportAllOperation.Create("topic", 10, ExportTypes.json, @"C:\some\deep\path\export-file.json");

        var message = operation.GetStatusMessage();

        Assert.Contains("export-file.json", message);
        Assert.DoesNotContain(@"C:\some\deep\path", message);
    }

    #endregion

    #region IsValid

    [Fact]
    public void IsValid_WithValidState_ReturnsTrue()
    {
        var operation = ExportAllOperation.Create("sensor/data", 50, ExportTypes.json, "output.json");

        Assert.True(operation.IsValid());
    }

    [Fact]
    public void IsValid_WithEmptyTopicName_ReturnsFalse()
    {
        var operation = new ExportAllOperation
        {
            TopicName = "",
            MessageCount = 10,
            ExportedCount = 10,
            ExportFormat = ExportTypes.json,
            OutputFilePath = "output.json",
            Timestamp = DateTime.UtcNow
        };

        Assert.False(operation.IsValid());
    }

    [Fact]
    public void IsValid_WithNegativeMessageCount_ReturnsFalse()
    {
        var operation = new ExportAllOperation
        {
            TopicName = "topic",
            MessageCount = -1,
            ExportedCount = 0,
            ExportFormat = ExportTypes.json,
            OutputFilePath = "output.json",
            Timestamp = DateTime.UtcNow
        };

        Assert.False(operation.IsValid());
    }

    [Fact]
    public void IsValid_WithExportedCountExceedingLimit_ReturnsFalse()
    {
        var operation = new ExportAllOperation
        {
            TopicName = "topic",
            MessageCount = 200,
            ExportedCount = 150,
            ExportFormat = ExportTypes.json,
            OutputFilePath = "output.json",
            Timestamp = DateTime.UtcNow
        };

        Assert.False(operation.IsValid());
    }

    [Fact]
    public void IsValid_WithEmptyOutputFilePath_ReturnsFalse()
    {
        var operation = new ExportAllOperation
        {
            TopicName = "topic",
            MessageCount = 10,
            ExportedCount = 10,
            ExportFormat = ExportTypes.json,
            OutputFilePath = "   ",
            Timestamp = DateTime.UtcNow
        };

        Assert.False(operation.IsValid());
    }

    [Fact]
    public void IsValid_WithNegativeExportedCount_ReturnsFalse()
    {
        var operation = new ExportAllOperation
        {
            TopicName = "topic",
            MessageCount = 10,
            ExportedCount = -1,
            ExportFormat = ExportTypes.json,
            OutputFilePath = "output.json",
            Timestamp = DateTime.UtcNow
        };

        Assert.False(operation.IsValid());
    }

    #endregion
}
