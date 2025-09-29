using System.Text;
using CrowsNestMqtt.BusinessLogic.Exporter;
using CrowsNestMqtt.Tests.TestData;
using CrowsNestMqtt.Utils;
using MQTTnet;
using MQTTnet.Protocol;
using Xunit;
using Xunit.Abstractions;

namespace CrowsNestMqtt.Integration.Tests;

/// <summary>
/// Integration tests for Unicode and special character preservation in correlation data
/// across UI display, export, and copy operations.
///
/// This test suite validates that correlation data maintains consistent formatting
/// across all three operations: UI display, file export, and clipboard copy.
/// </summary>
public class CorrelationDataFormattingTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly MqttTestUtilities _mqttUtils;
    private readonly string _testDirectory;

    public CorrelationDataFormattingTests(ITestOutputHelper output)
    {
        _output = output;
        _mqttUtils = new MqttTestUtilities();
        _testDirectory = Path.Combine(Path.GetTempPath(), "CrowsNestMQTT_CorrelationFormatTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
    }

    public async Task InitializeAsync()
    {
        var port = await _mqttUtils.StartEmbeddedBrokerAsync();
        _output.WriteLine($"Started embedded MQTT broker on port {port}");
    }

    public async Task DisposeAsync()
    {
        await _mqttUtils.DisposeAsync();

        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Warning: Could not delete test directory {_testDirectory}. Reason: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Test Unicode character preservation across display and export operations.
    /// This test should FAIL initially due to format differences between display (hex) and export (base64).
    /// </summary>
    [Fact]
    public async Task CorrelationData_UnicodeCharacters_PreservesFormatConsistently()
    {
        // Arrange - Unicode test data with various international characters
        var testCases = new[]
        {
            ("Russian Cyrillic", "корреляция-тест"),
            ("Chinese Characters", "测试相关数据"),
            ("Arabic Text", "بيانات الارتباط"),
            ("Japanese Mixed", "テストデータ-相関"),
            ("German Umlauts", "Prüfung-Daten-Ö-Ä-Ü"),
            ("French Accents", "données-corrélées-é-è-ê"),
            ("Mixed Scripts", "test-测试-тест-🔗")
        };

        var publisher = await _mqttUtils.CreateConnectedTestClientAsync("UnicodePublisher");
        var exporter = new TextExporter();

        foreach (var (description, unicodeText) in testCases)
        {
            // Create message with Unicode correlation data
            var correlationBytes = Encoding.UTF8.GetBytes(unicodeText);
            var message = CreateTestMessage(
                topic: $"unicode/test/{description.Replace(" ", "_").ToLower()}",
                payload: $"{{\"test\": \"{description}\"}}",
                correlationData: correlationBytes
            );

            // Act - Simulate the full flow: MQTT → UI Display → Export
            await _mqttUtils.PublishTestMessagesAsync(publisher, new[] { message });
            await Task.Delay(50); // Allow message processing

            // Simulate UI display formatting (BitConverter.ToString)
            var displayFormat = BitConverter.ToString(correlationBytes).Replace("-", string.Empty);

            // Export to file (Convert.ToBase64String)
            var exportFile = exporter.ExportToFile(message.Message, message.ReceivedTimestamp, _testDirectory);
            var exportContent = await File.ReadAllTextAsync(exportFile!);

            // Extract correlation data from export content
            var correlationLine = exportContent
                .Split('\n')
                .FirstOrDefault(line => line.StartsWith("Correlation Data:"));

            Assert.NotNull(correlationLine);
            var exportFormat = correlationLine.Split(':')[1].Trim();

            // Simulate UI display formatting using the same logic as MainViewModel
            var uiDisplayFormat = BitConverter.ToString(correlationBytes).Replace("-", string.Empty);

            // Assert - This should FAIL initially due to format inconsistency
            _output.WriteLine($"Test: {description}");
            _output.WriteLine($"Original Unicode: {unicodeText}");
            _output.WriteLine($"UI Display Format (hex): {uiDisplayFormat}");
            _output.WriteLine($"Export Format (base64): {exportFormat}");
            _output.WriteLine($"Expected Display (hex): {displayFormat}");
            _output.WriteLine($"Expected Export (base64): {Convert.ToBase64String(correlationBytes)}");

            // These assertions will initially FAIL - demonstrating the inconsistency
            Assert.Equal(uiDisplayFormat, exportFormat);

            // Verify round-trip integrity for both formats
            var decodedFromDisplay = ConvertFromHexString(uiDisplayFormat);
            var decodedFromExport = Convert.FromBase64String(exportFormat);
            var unicodeFromDisplay = Encoding.UTF8.GetString(decodedFromDisplay);
            var unicodeFromExport = Encoding.UTF8.GetString(decodedFromExport);

            Assert.Equal(unicodeText, unicodeFromDisplay,
                $"Unicode text not preserved in display format for {description}");
            Assert.Equal(unicodeText, unicodeFromExport,
                $"Unicode text not preserved in export format for {description}");
        }
    }

    /// <summary>
    /// Test emoji and special symbol preservation in correlation data.
    /// </summary>
    [Fact]
    public async Task CorrelationData_EmojiAndSpecialSymbols_PreservesFormatConsistently()
    {
        var testCases = new[]
        {
            ("Simple Emoji", "🔗📊⚡🌡️"),
            ("Complex Emoji", "👨‍💻🏠🌍🚀💡"),
            ("ASCII Special", "!@#$%^&*()_+-={}|[]\\:\";'<>?,./"),
            ("Mathematical", "∑∆∞±≤≥≠√∫∂∇"),
            ("Currency", "$€£¥₹₿¢₡₦₨"),
            ("Mixed Content", "Data🔗Test$100✅")
        };

        var publisher = await _mqttUtils.CreateConnectedTestClientAsync("EmojiPublisher");
        var exporter = new TextExporter();

        foreach (var (description, symbolText) in testCases)
        {
            var correlationBytes = Encoding.UTF8.GetBytes(symbolText);
            var message = CreateTestMessage(
                topic: $"emoji/test/{description.Replace(" ", "_").ToLower()}",
                payload: $"{{\"test\": \"{description}\"}}",
                correlationData: correlationBytes
            );

            await _mqttUtils.PublishTestMessagesAsync(publisher, new[] { message });
            await Task.Delay(50);

            // Simulate UI display formatting using the same logic as MainViewModel
            var uiFormat = BitConverter.ToString(correlationBytes).Replace("-", string.Empty);

            var exportFile = exporter.ExportToFile(message.Message, message.ReceivedTimestamp, _testDirectory);
            var exportContent = await File.ReadAllTextAsync(exportFile!);
            var exportFormat = ExtractCorrelationDataFromExport(exportContent);

            _output.WriteLine($"Symbol Test: {description} - Original: {symbolText}");
            _output.WriteLine($"UI Format: {uiFormat}");
            _output.WriteLine($"Export Format: {exportFormat}");

            // This assertion should initially FAIL
            Assert.Equal(uiFormat, exportFormat,
                $"Format inconsistency for {description}: UI='{uiFormat}', Export='{exportFormat}'");

            // Verify symbol integrity - UI format is hex, export format is base64
            var decodedFromUI = ConvertFromHexString(uiFormat);
            var decodedFromExport = Convert.FromBase64String(exportFormat);

            // Both should decode to the same bytes
            Assert.Equal(decodedFromUI, decodedFromExport,
                $"Decoded bytes differ between UI and export for {description}");

            var decodedBytes = decodedFromUI;

            var recoveredText = Encoding.UTF8.GetString(decodedBytes);
            Assert.Equal(symbolText, recoveredText);
        }
    }

    /// <summary>
    /// Test control characters and boundary cases in correlation data.
    /// </summary>
    [Fact]
    public async Task CorrelationData_ControlCharactersAndBoundaries_PreservesFormatConsistently()
    {
        var testCases = new[]
        {
            ("Newlines and Tabs", "Line1\nLine2\tTabbed"),
            ("Null Bytes", "Data\0With\0Nulls"),
            ("Mixed Control", "Start\r\nMiddle\x07\x08End"),
            ("Very Long String", new string('A', 1000) + "🔗" + new string('Z', 1000)),
            ("Binary-like Text", "\x01\x02\x03Hello\xFF\xFE\xFD"),
            ("Malformed UTF-8 Recovery", "Valid\xC0\x80Invalid\xED\xA0\x80More")
        };

        var publisher = await _mqttUtils.CreateConnectedTestClientAsync("ControlCharPublisher");
        var exporter = new TextExporter();

        foreach (var (description, testData) in testCases)
        {
            byte[] correlationBytes;
            if (description.Contains("Malformed"))
            {
                // Create intentionally malformed UTF-8 for edge case testing
                correlationBytes = Encoding.Latin1.GetBytes(testData);
            }
            else
            {
                correlationBytes = Encoding.UTF8.GetBytes(testData);
            }

            var message = CreateTestMessage(
                topic: $"control/test/{description.Replace(" ", "_").ToLower()}",
                payload: $"{{\"test\": \"{description}\"}}",
                correlationData: correlationBytes
            );

            await _mqttUtils.PublishTestMessagesAsync(publisher, new[] { message });
            await Task.Delay(50);

            // Simulate UI display formatting using the same logic as MainViewModel
            var uiFormat = correlationBytes.Length > 0
                ? BitConverter.ToString(correlationBytes).Replace("-", string.Empty)
                : "";

            var exportFile = exporter.ExportToFile(message.Message, message.ReceivedTimestamp, _testDirectory);
            var exportContent = await File.ReadAllTextAsync(exportFile!);
            var exportFormat = ExtractCorrelationDataFromExport(exportContent);

            _output.WriteLine($"Control Char Test: {description}");
            _output.WriteLine($"Original Length: {correlationBytes.Length} bytes");
            _output.WriteLine($"UI Format: {uiFormat}");
            _output.WriteLine($"Export Format: {exportFormat}");

            // Format consistency check - should initially FAIL
            Assert.Equal(uiFormat, exportFormat,
                $"Format inconsistency for {description}");

            // Byte-level preservation check - UI format is hex, export format is base64
            var decodedFromUI = string.IsNullOrEmpty(uiFormat) ? Array.Empty<byte>() : ConvertFromHexString(uiFormat);
            var decodedFromExport = string.IsNullOrEmpty(exportFormat) ? Array.Empty<byte>() : Convert.FromBase64String(exportFormat);

            // Both should decode to the same bytes
            Assert.Equal(decodedFromUI, decodedFromExport,
                $"Decoded bytes differ between UI and export for {description}");

            var decodedBytes = decodedFromUI;

            Assert.Equal(correlationBytes, decodedBytes);
        }
    }

    /// <summary>
    /// Test edge cases: empty, null, and truncated correlation data.
    /// </summary>
    [Fact]
    public async Task CorrelationData_EdgeCases_HandlesConsistently()
    {
        var testCases = new[]
        {
            ("Empty Array", Array.Empty<byte>()),
            ("Single Byte", new byte[] { 0x42 }),
            ("Null Bytes Only", new byte[] { 0x00, 0x00, 0x00 }),
            ("Max Byte Value", new byte[] { 0xFF, 0xFF, 0xFF }),
            ("Random Binary", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })
        };

        var publisher = await _mqttUtils.CreateConnectedTestClientAsync("EdgeCasePublisher");
        var exporter = new TextExporter();

        foreach (var (description, testBytes) in testCases)
        {
            var message = CreateTestMessage(
                topic: $"edge/test/{description.Replace(" ", "_").ToLower()}",
                payload: "{}",
                correlationData: testBytes.Length == 0 ? null : testBytes
            );

            await _mqttUtils.PublishTestMessagesAsync(publisher, new[] { message });
            await Task.Delay(50);

            var exportFile = exporter.ExportToFile(message.Message, message.ReceivedTimestamp, _testDirectory);
            var exportContent = await File.ReadAllTextAsync(exportFile!);
            var exportFormat = ExtractCorrelationDataFromExport(exportContent);

            if (testBytes.Length == 0)
            {
                // For empty/null correlation data, export should show empty base64
                Assert.True(string.IsNullOrEmpty(exportFormat) || exportFormat == Convert.ToBase64String(Array.Empty<byte>()),
                    $"Empty correlation data should result in empty export format, got: '{exportFormat}'");
            }
            else
            {
                var uiFormat = BitConverter.ToString(testBytes).Replace("-", string.Empty);

                _output.WriteLine($"Edge Case: {description}");
                _output.WriteLine($"Bytes: [{string.Join(", ", testBytes.Select(b => $"0x{b:X2}"))}]");
                _output.WriteLine($"UI Format (hex): {uiFormat}");
                _output.WriteLine($"Export Format (base64): {exportFormat}");

                // This should initially FAIL due to format inconsistency
                Assert.Equal(uiFormat, exportFormat,
                    $"Edge case format mismatch for {description}: hex '{uiFormat}' vs base64 '{exportFormat}'");
            }
        }
    }

    /// <summary>
    /// Performance test for handling large correlation data with special characters.
    /// </summary>
    [Fact]
    public async Task CorrelationData_LargeUnicodeData_MaintainsPerformanceAndConsistency()
    {
        // Create a large correlation data payload with mixed Unicode content
        var largeUnicodeContent = string.Concat(
            Enumerable.Repeat("🔗测试корреляция-データ-🌍", 1000)) + "END_MARKER";

        var correlationBytes = Encoding.UTF8.GetBytes(largeUnicodeContent);

        var message = CreateTestMessage(
            topic: "performance/large_unicode",
            payload: "{\"performance_test\": true}",
            correlationData: correlationBytes
        );

        var publisher = await _mqttUtils.CreateConnectedTestClientAsync("PerfPublisher");
        var exporter = new TextExporter();

        // Measure processing time
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        await _mqttUtils.PublishTestMessagesAsync(publisher, new[] { message });
        await Task.Delay(50);

        // Simulate UI display formatting using the same logic as MainViewModel
        var uiFormat = BitConverter.ToString(correlationBytes).Replace("-", string.Empty);

        var exportFile = exporter.ExportToFile(message.Message, message.ReceivedTimestamp, _testDirectory);
        var exportContent = await File.ReadAllTextAsync(exportFile!);
        var exportFormat = ExtractCorrelationDataFromExport(exportContent);

        stopwatch.Stop();

        _output.WriteLine($"Large Unicode Data Performance Test:");
        _output.WriteLine($"Correlation data size: {correlationBytes.Length:N0} bytes");
        _output.WriteLine($"Processing time: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"UI format length: {uiFormat.Length:N0} chars");
        _output.WriteLine($"Export format length: {exportFormat.Length:N0} chars");

        // Performance assertion - should complete within reasonable time
        Assert.True(stopwatch.ElapsedMilliseconds < 1000,
            $"Large correlation data processing took too long: {stopwatch.ElapsedMilliseconds}ms");

        // Format consistency assertion - should initially FAIL
        Assert.Equal(uiFormat, exportFormat,
            $"Large Unicode data format inconsistency: UI hex '{uiFormat.Substring(0, Math.Min(50, uiFormat.Length))}...' vs export base64 '{exportFormat.Substring(0, Math.Min(50, exportFormat.Length))}...'");

        // Verify data integrity for large payloads
        var decodedFromUI = ConvertFromHexString(uiFormat);
        var decodedFromExport = Convert.FromBase64String(exportFormat);

        // Both should decode to the same bytes
        Assert.Equal(decodedFromUI, decodedFromExport,
            "Large Unicode data: decoded bytes differ between UI and export formats");

        var decodedBytes = decodedFromUI;

        var recoveredContent = Encoding.UTF8.GetString(decodedBytes);
        Assert.Equal(largeUnicodeContent, recoveredContent);

        Assert.EndsWith("END_MARKER", recoveredContent);
    }

    #region Helper Methods

    private BufferedMqttMessage CreateTestMessage(
        string topic,
        string payload,
        byte[]? correlationData = null)
    {
        var message = new MqttApplicationMessage
        {
            Topic = topic,
            PayloadSegment = new ArraySegment<byte>(Encoding.UTF8.GetBytes(payload)),
            QualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce,
            Retain = false,
            CorrelationData = correlationData
        };

        return new BufferedMqttMessage(Guid.NewGuid(), message, DateTime.UtcNow);
    }

    private string ExtractCorrelationDataFromExport(string exportContent)
    {
        var correlationLine = exportContent
            .Split('\n')
            .FirstOrDefault(line => line.StartsWith("Correlation Data:"));

        if (correlationLine == null)
            return string.Empty;

        return correlationLine.Split(':', 2)[1].Trim();
    }

    private bool IsHexString(string input)
    {
        return input.All(c => char.IsDigit(c) || (char.ToLower(c) >= 'a' && char.ToLower(c) <= 'f'));
    }

    private byte[] ConvertFromHexString(string hex)
    {
        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < hex.Length; i += 2)
        {
            bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        }
        return bytes;
    }

    #endregion
}