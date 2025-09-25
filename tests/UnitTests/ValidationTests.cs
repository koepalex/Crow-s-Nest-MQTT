using System;
using Xunit;
using NSubstitute;
using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.BusinessLogic.Services;
using CrowsNestMqtt.Utils.Models;
using Microsoft.Extensions.Logging;

namespace CrowsNestMqtt.UnitTests;

/// <summary>
/// Unit tests for delete topic validation logic.
/// Tests the ValidateDeleteOperation method in isolation.
/// </summary>
public class ValidationTests
{
    [Fact]
    public void ValidateDeleteOperation_WithValidTopicPattern_ReturnsSuccess()
    {
        // Arrange
        var topicPattern = "sensor/temperature";
        var maxTopicLimit = 500;

        // Act
        var result = CreateDeleteTopicService().ValidateDeleteOperation(topicPattern, maxTopicLimit);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsValid);
        Assert.Empty(result.ErrorMessages);
    }

    [Fact]
    public void ValidateDeleteOperation_WithEmptyTopicPattern_ReturnsError()
    {
        // Arrange
        var topicPattern = "";
        var maxTopicLimit = 500;

        // Act
        var result = CreateDeleteTopicService().ValidateDeleteOperation(topicPattern, maxTopicLimit);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.ErrorMessages);
        Assert.Contains("Topic pattern cannot be empty", result.ErrorMessages);
    }

    [Fact]
    public void ValidateDeleteOperation_WithNullTopicPattern_ReturnsError()
    {
        // Arrange
        string topicPattern = null!;
        var maxTopicLimit = 500;

        // Act
        var result = CreateDeleteTopicService().ValidateDeleteOperation(topicPattern, maxTopicLimit);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.ErrorMessages);
        Assert.Contains("Topic pattern cannot be empty", result.ErrorMessages);
    }

    [Fact]
    public void ValidateDeleteOperation_WithInvalidWildcardPlacement_ReturnsError()
    {
        // Arrange
        var topicPattern = "sensor/#/temperature"; // # must be at end
        var maxTopicLimit = 500;

        // Act
        var result = CreateDeleteTopicService().ValidateDeleteOperation(topicPattern, maxTopicLimit);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.ErrorMessages);
        Assert.Contains("Multi-level wildcard '#' must be at the end of topic pattern", result.ErrorMessages);
    }

    [Fact]
    public void ValidateDeleteOperation_WithInvalidSingleLevelWildcard_ReturnsError()
    {
        // Arrange
        var topicPattern = "sensor/temp+ature"; // + not properly separated
        var maxTopicLimit = 500;

        // Act
        var result = CreateDeleteTopicService().ValidateDeleteOperation(topicPattern, maxTopicLimit);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.ErrorMessages);
        Assert.Contains("Single-level wildcard '+' must be separated by '/' characters", result.ErrorMessages);
    }

    [Fact]
    public void ValidateDeleteOperation_WithValidWildcards_ReturnsSuccess()
    {
        // Arrange
        var topicPattern = "sensor/+/temperature/#";
        var maxTopicLimit = 500;

        // Act
        var result = CreateDeleteTopicService().ValidateDeleteOperation(topicPattern, maxTopicLimit);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsValid);
        Assert.Empty(result.ErrorMessages);
    }

    [Fact]
    public void ValidateDeleteOperation_WithZeroMaxLimit_ReturnsError()
    {
        // Arrange
        var topicPattern = "sensor/temperature";
        var maxTopicLimit = 0;

        // Act
        var result = CreateDeleteTopicService().ValidateDeleteOperation(topicPattern, maxTopicLimit);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.ErrorMessages);
        Assert.Contains("Maximum topic limit must be greater than 0", result.ErrorMessages);
    }

    [Fact]
    public void ValidateDeleteOperation_WithNegativeMaxLimit_ReturnsError()
    {
        // Arrange
        var topicPattern = "sensor/temperature";
        var maxTopicLimit = -1;

        // Act
        var result = CreateDeleteTopicService().ValidateDeleteOperation(topicPattern, maxTopicLimit);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.ErrorMessages);
        Assert.Contains("Maximum topic limit must be greater than 0", result.ErrorMessages);
    }

    [Fact]
    public void ValidateDeleteOperation_WithExcessiveMaxLimit_ReturnsError()
    {
        // Arrange
        var topicPattern = "sensor/temperature";
        var maxTopicLimit = 50000; // Exceeds system maximum

        // Act
        var result = CreateDeleteTopicService().ValidateDeleteOperation(topicPattern, maxTopicLimit);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.ErrorMessages);
        Assert.Contains("Maximum topic limit exceeds system maximum (10,000)", result.ErrorMessages);
    }

    [Theory]
    [InlineData("#")]
    [InlineData("+")]
    [InlineData("sensor/#")]
    [InlineData("sensor/+")]
    public void ValidateDeleteOperation_WithBroadPatterns_ReturnsWarnings(string topicPattern)
    {
        // Arrange
        var maxTopicLimit = 500;

        // Act
        var result = CreateDeleteTopicService().ValidateDeleteOperation(topicPattern, maxTopicLimit);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsValid); // Valid but with warnings
        Assert.NotEmpty(result.WarningMessages);
        Assert.Contains("This pattern may match a very large number of topics", result.WarningMessages);
    }

    [Theory]
    [InlineData("sensor/temperature")]
    [InlineData("building/floor1/room101")]
    [InlineData("device/status/online")]
    public void ValidateDeleteOperation_WithValidSpecificPatterns_ReturnsSuccess(string topicPattern)
    {
        // Arrange
        var maxTopicLimit = 500;

        // Act
        var result = CreateDeleteTopicService().ValidateDeleteOperation(topicPattern, maxTopicLimit);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsValid);
        Assert.Empty(result.ErrorMessages);
        Assert.Empty(result.WarningMessages);
    }

    private static IDeleteTopicService CreateDeleteTopicService()
    {
        // Create a service instance for testing validation logic
        var mockMqttService = Substitute.For<IMqttService>();
        var mockLogger = Substitute.For<ILogger<DeleteTopicService>>();

        return new DeleteTopicService(mqttService: mockMqttService, logger: mockLogger);
    }
}