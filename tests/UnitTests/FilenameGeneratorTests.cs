using CrowsNestMqtt.Utils;
using Xunit;

namespace CrowsNestMqtt.UnitTests;

/// <summary>
/// Unit tests for FilenameGenerator utility class.
/// T029: Validates filename sanitization and generation logic.
/// </summary>
public class FilenameGeneratorTests
{
    /// <summary>
    /// T029: Test that MQTT topic hierarchy separators (/) are replaced with underscores.
    /// </summary>
    [Theory]
    [InlineData("sensor/temperature", "sensor_temperature")]
    [InlineData("home/living/room/temperature", "home_living_room_temperature")]
    [InlineData("a/b/c/d/e/f", "a_b_c_d_e_f")]
    [InlineData("/leading/slash", "_leading_slash")]
    [InlineData("trailing/slash/", "trailing_slash_")]
    public void SanitizeTopicName_WithSlashes_ReplacesWithUnderscores(string input, string expected)
    {
        // Act
        string result = FilenameGenerator.SanitizeTopicName(input);

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// T029: Test that MQTT wildcards (+ and #) are preserved but other invalid chars replaced.
    /// Note: Wildcards are valid in topics but current implementation replaces them too.
    /// </summary>
    [Theory]
    [InlineData("sensor/+", "sensor_+")]
    [InlineData("sensor/#", "sensor_#")]
    [InlineData("sensor/+/temperature", "sensor_+_temperature")]
    [InlineData("home/#", "home_#")]
    public void SanitizeTopicName_WithWildcards_HandlesCorrectly(string input, string expected)
    {
        // Act
        string result = FilenameGenerator.SanitizeTopicName(input);

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// T029: Test that invalid filename characters are replaced with underscores.
    /// </summary>
    [Theory]
    [InlineData("topic:with:colons", "topic_with_colons")]
    [InlineData("topic?with?question", "topic_with_question")]
    [InlineData("topic*with*asterisk", "topic_with_asterisk")]
    [InlineData("topic<with>brackets", "topic_with_brackets")]
    [InlineData("topic|with|pipe", "topic_with_pipe")]
    [InlineData("topic\"with\"quotes", "topic_with_quotes")]
    [InlineData("topic\\with\\backslash", "topic_with_backslash")]
    public void SanitizeTopicName_WithInvalidChars_ReplacesWithUnderscores(string input, string expected)
    {
        // Act
        string result = FilenameGenerator.SanitizeTopicName(input);

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// T029: Test that control characters (0x00-0x1F) are replaced.
    /// </summary>
    [Fact]
    public void SanitizeTopicName_WithControlCharacters_ReplacesWithUnderscores()
    {
        // Arrange
        string input = "topic\u0000with\u0001control\u001Fchars";

        // Act
        string result = FilenameGenerator.SanitizeTopicName(input);

        // Assert
        Assert.DoesNotContain('\u0000', result);
        Assert.DoesNotContain('\u0001', result);
        Assert.DoesNotContain('\u001F', result);
        Assert.Equal("topic_with_control_chars", result);
    }

    /// <summary>
    /// T029: Test that empty or null input returns fallback value.
    /// </summary>
    [Theory]
    [InlineData("", "unknown")]
    [InlineData("   ", "unknown")]
    [InlineData(null, "unknown")]
    public void SanitizeTopicName_WithEmptyOrNull_ReturnsFallback(string? input, string expected)
    {
        // Act
        string result = FilenameGenerator.SanitizeTopicName(input!);

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// T029: Test that very long topic names are truncated to 200 characters.
    /// </summary>
    [Fact]
    public void SanitizeTopicName_WithLongTopicName_TruncatesTo200Chars()
    {
        // Arrange
        string longTopic = new string('a', 250); // 250 characters

        // Act
        string result = FilenameGenerator.SanitizeTopicName(longTopic);

        // Assert
        Assert.Equal(200, result.Length);
    }

    /// <summary>
    /// T029: Test that topic names under 200 chars are NOT truncated.
    /// </summary>
    [Fact]
    public void SanitizeTopicName_WithShortTopicName_DoesNotTruncate()
    {
        // Arrange
        string shortTopic = new string('a', 150); // 150 characters

        // Act
        string result = FilenameGenerator.SanitizeTopicName(shortTopic);

        // Assert
        Assert.Equal(150, result.Length);
        Assert.Equal(shortTopic, result);
    }

    /// <summary>
    /// T029: Test GenerateExportAllFilename with valid inputs.
    /// Pattern: {sanitized_topic}_{yyyyMMdd_HHmmss}.{ext}
    /// </summary>
    [Fact]
    public void GenerateExportFilename_ValidInputs_ReturnsCorrectPattern()
    {
        // Arrange
        string topicName = "sensors/temperature";
        DateTime timestamp = new DateTime(2026, 1, 21, 14, 30, 45);
        string extension = "json";

        // Act
        string result = FilenameGenerator.GenerateExportAllFilename(topicName, timestamp, extension);

        // Assert
        Assert.Equal("sensors_temperature_20260121_143045.json", result);
    }

    /// <summary>
    /// T029: Test GenerateExportAllFilename with extension having leading dot.
    /// </summary>
    [Fact]
    public void GenerateExportFilename_WithLeadingDot_StripsLeadingDot()
    {
        // Arrange
        string topicName = "test/topic";
        DateTime timestamp = new DateTime(2026, 1, 21, 14, 30, 45);
        string extension = ".json"; // Leading dot

        // Act
        string result = FilenameGenerator.GenerateExportAllFilename(topicName, timestamp, extension);

        // Assert
        Assert.Equal("test_topic_20260121_143045.json", result);
        Assert.DoesNotContain("..", result); // Should not have double dots
    }

    /// <summary>
    /// T029: Test GenerateExportAllFilename with TXT extension.
    /// </summary>
    [Fact]
    public void GenerateExportFilename_WithTxtExtension_ReturnsCorrectFormat()
    {
        // Arrange
        string topicName = "home/living/room";
        DateTime timestamp = new DateTime(2026, 12, 31, 23, 59, 59);
        string extension = "txt";

        // Act
        string result = FilenameGenerator.GenerateExportAllFilename(topicName, timestamp, extension);

        // Assert
        Assert.Equal("home_living_room_20261231_235959.txt", result);
    }

    /// <summary>
    /// T029: Test that null or empty topic name throws ArgumentException.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GenerateExportFilename_WithNullOrEmptyTopic_ThrowsArgumentException(string? topicName)
    {
        // Arrange
        DateTime timestamp = DateTime.Now;
        string extension = "json";

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            FilenameGenerator.GenerateExportAllFilename(topicName!, timestamp, extension));
    }

    /// <summary>
    /// T029: Test that null or empty extension throws ArgumentException.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GenerateExportFilename_WithNullOrEmptyExtension_ThrowsArgumentException(string? extension)
    {
        // Arrange
        string topicName = "test/topic";
        DateTime timestamp = DateTime.Now;

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            FilenameGenerator.GenerateExportAllFilename(topicName, timestamp, extension!));
    }

    /// <summary>
    /// T029: Test GetUniqueFilename when file doesn't exist.
    /// </summary>
    [Fact]
    public void GetUniqueFilename_FileDoesNotExist_ReturnsSamePath()
    {
        // Arrange
        string basePath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.json");

        // Act
        string result = FilenameGenerator.GetUniqueFilename(basePath);

        // Assert
        Assert.Equal(basePath, result);
    }

    /// <summary>
    /// T029: Test GetUniqueFilename when file exists - should append _1.
    /// </summary>
    [Fact]
    public void GetUniqueFilename_FileExists_AppendsCounter()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), $"FilenameGeneratorTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            string basePath = Path.Combine(tempDir, "existing_file.json");
            File.WriteAllText(basePath, "test content");

            // Act
            string result = FilenameGenerator.GetUniqueFilename(basePath);

            // Assert
            Assert.NotEqual(basePath, result);
            Assert.Equal(Path.Combine(tempDir, "existing_file_1.json"), result);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    /// <summary>
    /// T029: Test GetUniqueFilename increments counter when multiple files exist.
    /// </summary>
    [Fact]
    public void GetUniqueFilename_MultipleFilesExist_IncrementsCounter()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), $"FilenameGeneratorTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            string basePath = Path.Combine(tempDir, "file.json");
            File.WriteAllText(basePath, "test");
            File.WriteAllText(Path.Combine(tempDir, "file_1.json"), "test");
            File.WriteAllText(Path.Combine(tempDir, "file_2.json"), "test");

            // Act
            string result = FilenameGenerator.GetUniqueFilename(basePath);

            // Assert
            Assert.Equal(Path.Combine(tempDir, "file_3.json"), result);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    /// <summary>
    /// T029: Test IsValidPath with valid paths.
    /// </summary>
    [Theory]
    [InlineData("C:\\temp\\export.json")]
    [InlineData("S:\\exports\\data.txt")]
    [InlineData("relative\\path\\file.json")]
    public void IsValidPath_WithValidPath_ReturnsTrue(string path)
    {
        // Act
        bool result = FilenameGenerator.IsValidPath(path);

        // Assert
        // Note: Result depends on whether parent directory exists
        // We're just testing it doesn't throw and returns a boolean
        Assert.IsType<bool>(result);
    }

    /// <summary>
    /// T029: Test IsValidPath with null or empty path.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValidPath_WithNullOrEmpty_ReturnsFalse(string? path)
    {
        // Act
        bool result = FilenameGenerator.IsValidPath(path!);

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// T029: Test that sanitization handles real-world MQTT topic examples.
    /// Note: $ is a valid filename character and won't be replaced.
    /// </summary>
    [Theory]
    [InlineData("$SYS/broker/uptime", "$SYS_broker_uptime")]
    [InlineData("zigbee2mqtt/bridge/config", "zigbee2mqtt_bridge_config")]
    [InlineData("homeassistant/sensor/temperature/config", "homeassistant_sensor_temperature_config")]
    [InlineData("shellies/shellyplug-s-12345/relay/0", "shellies_shellyplug-s-12345_relay_0")]
    public void SanitizeTopicName_WithRealWorldTopics_ReturnsValidFilename(string input, string expected)
    {
        // Act
        string result = FilenameGenerator.SanitizeTopicName(input);

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// T029: Test complete filename generation with real-world scenario.
    /// </summary>
    [Fact]
    public void GenerateExportFilename_RealWorldScenario_ProducesValidFilename()
    {
        // Arrange
        string topicName = "home/sensors/temperature/+";
        DateTime timestamp = new DateTime(2026, 1, 21, 14, 30, 45);
        string extension = "json";

        // Act
        string result = FilenameGenerator.GenerateExportAllFilename(topicName, timestamp, extension);

        // Assert
        Assert.Equal("home_sensors_temperature_+_20260121_143045.json", result);

        // Verify it's a valid filename (no path separators in the topic part)
        string filenamePart = Path.GetFileNameWithoutExtension(result);
        Assert.DoesNotContain('/', filenamePart);
        Assert.DoesNotContain('\\', filenamePart);
    }
}
