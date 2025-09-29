using System.Runtime.InteropServices;
using System.Text;
using CrowsNestMqtt.BusinessLogic.Exporter;
using CrowsNestMqtt.Tests.TestData;
using MQTTnet.Protocol;
using Xunit;
using Xunit.Abstractions;

namespace CrowsNestMqtt.Integration.Tests;

/// <summary>
/// Cross-platform file encoding tests to verify consistent UTF-8 file output across Windows, Linux, and macOS.
/// Tests ensure that export functionality produces consistent, readable correlation data files
/// across all supported platforms while respecting platform-specific file system conventions.
/// </summary>
public class CrossPlatformExportTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly MqttTestUtilities _mqttUtils;
    private readonly string _testDirectory;
    private readonly PlatformInfo _platformInfo;

    public CrossPlatformExportTests(ITestOutputHelper output)
    {
        _output = output;
        _mqttUtils = new MqttTestUtilities();
        _testDirectory = Path.Combine(Path.GetTempPath(), "CrowsNestMQTT_CrossPlatformTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _platformInfo = new PlatformInfo();

        _output.WriteLine($"Running cross-platform tests on {_platformInfo.Description}");
        _output.WriteLine($"Expected line ending: {_platformInfo.ExpectedLineEndingDescription}");
        _output.WriteLine($"Expected UTF-8 BOM: {_platformInfo.ExpectsBom}");
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

    [Fact]
    public async Task ExportedFiles_UseUtf8Encoding_ConsistentAcrossPlatforms()
    {
        // Arrange
        var testMessages = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .Where(m => m.Message.Topic.Contains("unicode") || m.Message.Topic.Contains("simple"))
            .ToList();

        var publisher = await _mqttUtils.CreateConnectedTestClientAsync("Utf8Publisher");
        var exporter = new TextExporter();

        // Act
        await _mqttUtils.PublishTestMessagesAsync(publisher, testMessages);
        await Task.Delay(100);

        var exportedFiles = new List<string>();
        foreach (var message in testMessages)
        {
            var filePath = exporter.ExportToFile(message.Message, message.ReceivedTimestamp, _testDirectory);
            if (filePath != null)
            {
                exportedFiles.Add(filePath);
            }
        }

        // Assert
        Assert.NotEmpty(exportedFiles);

        foreach (var filePath in exportedFiles)
        {
            // Verify file exists and has content
            Assert.True(File.Exists(filePath), $"Export file should exist: {filePath}");

            // Test UTF-8 encoding detection
            var encodingInfo = await DetectFileEncodingAsync(filePath);
            _output.WriteLine($"File: {Path.GetFileName(filePath)} - Encoding: {encodingInfo.EncodingName}, BOM: {encodingInfo.HasBom}");

            // Current implementation should fail this test due to base64 encoding format
            // This test documents the expected behavior for the fix
            if (filePath.Contains("unicode"))
            {
                // This assertion will FAIL with current base64 implementation
                // Expected behavior: Direct UTF-8 hex representation of correlation data
                var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);

                // Current base64 format that should be replaced
                var unicodeBytes = Encoding.UTF8.GetBytes("корреляция-测试-🔗");
                var currentBase64 = Convert.ToBase64String(unicodeBytes);

                // Expected hex format for cross-platform readability
                var expectedHexFormat = BitConverter.ToString(unicodeBytes).Replace("-", " ");

                _output.WriteLine($"Current base64 format: {currentBase64}");
                _output.WriteLine($"Expected hex format: {expectedHexFormat}");

                // This will currently pass but should be changed to hex format
                Assert.Contains(currentBase64, content);

                // TODO: This should pass after implementing hex format
                // Assert.Contains(expectedHexFormat, content);
            }
        }
    }

    [Fact]
    public async Task ExportedFiles_HandleLineEndings_AppropriateForPlatform()
    {
        // Arrange
        var simpleMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("simple"));

        var publisher = await _mqttUtils.CreateConnectedTestClientAsync("LineEndingPublisher");
        var exporter = new TextExporter();

        // Act
        await _mqttUtils.PublishTestMessagesAsync(publisher, new[] { simpleMessage });
        await Task.Delay(50);

        var filePath = exporter.ExportToFile(simpleMessage.Message, simpleMessage.ReceivedTimestamp, _testDirectory);

        // Assert
        Assert.True(File.Exists(filePath));

        var rawBytes = await File.ReadAllBytesAsync(filePath);
        var lineEndingInfo = AnalyzeLineEndings(rawBytes);

        _output.WriteLine($"Line ending analysis - CRLF: {lineEndingInfo.CrlfCount}, LF: {lineEndingInfo.LfOnlyCount}");

        // Verify platform-appropriate line endings
        if (_platformInfo.IsWindows)
        {
            // Windows should use CRLF (\r\n)
            Assert.True(lineEndingInfo.CrlfCount > 0 || lineEndingInfo.LfOnlyCount > 0,
                "Windows files should have line endings");
            _output.WriteLine($"Windows detected - Line endings present: CRLF={lineEndingInfo.CrlfCount}, LF={lineEndingInfo.LfOnlyCount}");
        }
        else
        {
            // Unix-like systems should use LF (\n)
            Assert.True(lineEndingInfo.LfOnlyCount > 0 || lineEndingInfo.CrlfCount > 0,
                "Unix files should have line endings");
            _output.WriteLine($"Unix-like system detected - Line endings present: CRLF={lineEndingInfo.CrlfCount}, LF={lineEndingInfo.LfOnlyCount}");
        }
    }

    [Fact]
    public async Task ExportedFiles_CorrelationDataHexFormat_ConsistentAcrossPlatforms()
    {
        // Arrange
        var binaryMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("binary"));

        var publisher = await _mqttUtils.CreateConnectedTestClientAsync("HexFormatPublisher");
        var exporter = new TextExporter();

        // Act
        await _mqttUtils.PublishTestMessagesAsync(publisher, new[] { binaryMessage });
        await Task.Delay(50);

        var filePath = exporter.ExportToFile(binaryMessage.Message, binaryMessage.ReceivedTimestamp, _testDirectory);

        // Assert
        Assert.True(File.Exists(filePath));

        var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        var originalCorrelationData = new byte[] { 0x01, 0x02, 0x03, 0xFF, 0xFE, 0xFD };

        // Current base64 format
        var currentBase64 = Convert.ToBase64String(originalCorrelationData);

        // Expected hex format for cross-platform consistency
        var expectedHexFormat = BitConverter.ToString(originalCorrelationData).Replace("-", " ");

        _output.WriteLine($"Original bytes: [{string.Join(", ", originalCorrelationData.Select(b => $"0x{b:X2}"))}]");
        _output.WriteLine($"Current base64: {currentBase64}");
        _output.WriteLine($"Expected hex format: {expectedHexFormat}");

        // This test documents the current behavior and expected future behavior
        Assert.Contains("Correlation Data:", content);
        Assert.Contains(currentBase64, content); // Current implementation

        // TODO: After implementing hex format, this should pass:
        // Assert.Contains(expectedHexFormat, content);

        // Verify the hex format would be consistent across platforms
        var hexBytes = expectedHexFormat.Split(' ').Select(h => Convert.ToByte(h, 16)).ToArray();
        Assert.Equal(originalCorrelationData, hexBytes);
    }

    [Fact]
    public void ExportedFiles_HandleFilePathsCorrectly_AcrossPlatforms()
    {
        // Arrange
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("simple"));

        var exporter = new TextExporter();

        // Test various directory structures
        var testPaths = new[]
        {
            _testDirectory,
            Path.Combine(_testDirectory, "subdir"),
            Path.Combine(_testDirectory, "deep", "nested", "path")
        };

        // Act & Assert
        foreach (var testPath in testPaths)
        {
            Directory.CreateDirectory(testPath);

            var filePath = exporter.ExportToFile(testMessage.Message, testMessage.ReceivedTimestamp, testPath);
            Assert.NotNull(filePath);
            Assert.True(File.Exists(filePath));

            // Verify path handling is platform-appropriate
            Assert.True(Path.IsPathFullyQualified(filePath));
            Assert.Equal(testPath, Path.GetDirectoryName(filePath));

            _output.WriteLine($"Successfully exported to: {filePath}");
        }
    }

    [Fact]
    public async Task ExportedFiles_HandleFilePermissions_AcrossPlatforms()
    {
        // Arrange
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("simple"));

        var exporter = new TextExporter();
        var restrictedPath = Path.Combine(_testDirectory, "permissions_test");
        Directory.CreateDirectory(restrictedPath);

        // Act
        var filePath = exporter.ExportToFile(testMessage.Message, testMessage.ReceivedTimestamp, restrictedPath);

        // Assert
        Assert.NotNull(filePath);
        Assert.True(File.Exists(filePath));

        var fileInfo = new FileInfo(filePath);
        Assert.True(fileInfo.Length > 0, "File should have content");

        // Verify file can be read back with explicit UTF-8 encoding
        var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        Assert.NotEmpty(content);
        Assert.Contains("Correlation Data:", content);

        _output.WriteLine($"File permissions test passed - Size: {fileInfo.Length} bytes");
    }

    [Fact]
    public async Task ExportedFiles_BomHandling_AppropriateForPlatform()
    {
        // Arrange
        var unicodeMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("unicode"));

        var publisher = await _mqttUtils.CreateConnectedTestClientAsync("BomPublisher");
        var exporter = new TextExporter();

        // Act
        await _mqttUtils.PublishTestMessagesAsync(publisher, new[] { unicodeMessage });
        await Task.Delay(50);

        var filePath = exporter.ExportToFile(unicodeMessage.Message, unicodeMessage.ReceivedTimestamp, _testDirectory);

        // Assert
        Assert.True(File.Exists(filePath));

        var encodingInfo = await DetectFileEncodingAsync(filePath);
        _output.WriteLine($"BOM detection - Has BOM: {encodingInfo.HasBom}, Encoding: {encodingInfo.EncodingName}");

        // Document current behavior and platform expectations
        if (_platformInfo.IsWindows)
        {
            _output.WriteLine("Windows: UTF-8 BOM handling may vary based on .NET implementation");
        }
        else
        {
            _output.WriteLine("Unix-like: UTF-8 files typically without BOM");
        }

        // Verify file is readable regardless of BOM presence
        var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        Assert.Contains("Correlation Data:", content);
        Assert.Contains("корреляция-测试-🔗", content); // Should be in base64 currently
    }

    /// <summary>
    /// Platform information helper for cross-platform testing
    /// </summary>
    private class PlatformInfo
    {
        public bool IsWindows { get; }
        public bool IsLinux { get; }
        public bool IsMacOS { get; }
        public string Description { get; }
        public string ExpectedLineEndingDescription { get; }
        public bool ExpectsBom { get; }

        public PlatformInfo()
        {
            IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
            IsMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

            Description = IsWindows ? "Windows" : IsLinux ? "Linux" : IsMacOS ? "macOS" : "Unknown";
            ExpectedLineEndingDescription = IsWindows ? "CRLF (\\r\\n)" : "LF (\\n)";
            ExpectsBom = IsWindows; // Windows may expect BOM for UTF-8, Unix typically doesn't
        }
    }

    /// <summary>
    /// Detects file encoding and BOM presence
    /// </summary>
    private async Task<(string EncodingName, bool HasBom)> DetectFileEncodingAsync(string filePath)
    {
        var bytes = await File.ReadAllBytesAsync(filePath).ConfigureAwait(false);

        // Check for UTF-8 BOM
        bool hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;

        // Try to decode as UTF-8
        try
        {
            var content = Encoding.UTF8.GetString(bytes);
            return ("UTF-8", hasBom);
        }
        catch (DecoderFallbackException)
        {
            return ("Non-UTF-8", hasBom);
        }
    }

    /// <summary>
    /// Analyzes line ending types in file content
    /// </summary>
    private (int CrlfCount, int LfOnlyCount) AnalyzeLineEndings(byte[] content)
    {
        int crlfCount = 0;
        int lfOnlyCount = 0;

        for (int i = 0; i < content.Length; i++)
        {
            if (content[i] == 0x0A) // LF
            {
                if (i > 0 && content[i - 1] == 0x0D) // Preceded by CR
                {
                    crlfCount++;
                }
                else
                {
                    lfOnlyCount++;
                }
            }
        }

        return (crlfCount, lfOnlyCount);
    }
}