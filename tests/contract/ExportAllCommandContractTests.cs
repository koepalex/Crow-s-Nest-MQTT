using CrowsNestMqtt.BusinessLogic.Commands;
using CrowsNestMqtt.BusinessLogic.Configuration;
using CrowsNestMqtt.BusinessLogic.Exporter;
using CrowsNestMqtt.BusinessLogic.Services;
using Xunit;

namespace CrowsNestMqtt.Contract.Tests;

/// <summary>
/// Contract tests for :export all command parsing functionality.
///
/// CRITICAL CONTRACT REQUIREMENTS:
/// - :export all → Exports all messages using configured settings
/// - :export all json /path → Exports with explicit format and path
/// - :export all txt /path → Exports with text format
/// - :export → Unchanged backward compatibility (existing behavior)
/// - Invalid formats (e.g., xml) → Returns failure with error message
///
/// These tests will FAIL initially because CommandParserService doesn't yet
/// recognize the "all" parameter in :export commands (T013 implementation pending).
/// </summary>
public class ExportAllCommandContractTests
{
    /// <summary>
    /// T002: Contract test for :export all with default settings.
    /// Expected to FAIL until T013 (Extend CommandParserService) is implemented.
    /// </summary>
    [Fact]
    public void ParseCommand_ExportAll_WithSettings_ReturnsSuccess()
    {
        // Arrange
        var parser = new CommandParserService();
        var settings = new SettingsData
        {
            ExportFormat = ExportTypes.json,
            ExportPath = "C:\\test\\exports"
        };

        // Act
        var result = parser.ParseInput(":export all", settings);

        // Assert - CONTRACT REQUIREMENT
        Assert.True(result.IsSuccess, "Command parsing should succeed for :export all");
        Assert.NotNull(result.ParsedCommand);
        var cmd = result.ParsedCommand!;
        Assert.Equal(CommandType.Export, cmd.Type);
        Assert.Equal(3, cmd.Arguments.Count);
        Assert.Equal("all", cmd.Arguments[0], ignoreCase: true);
        Assert.Equal("json", cmd.Arguments[1], ignoreCase: true);
        Assert.Equal("C:\\test\\exports", cmd.Arguments[2]);
    }

    /// <summary>
    /// T003: Contract test for :export all with explicit format and path.
    /// Expected to FAIL until T013 implementation.
    /// </summary>
    [Fact]
    public void ParseCommand_ExportAll_WithExplicitParams_ReturnsSuccess()
    {
        // Arrange
        var parser = new CommandParserService();
        var settings = new SettingsData();

        // Act
        var result = parser.ParseInput(":export all txt C:\\custom\\path", settings);

        // Assert - CONTRACT REQUIREMENT
        Assert.True(result.IsSuccess, "Command parsing should succeed for :export all with params");
        Assert.NotNull(result.ParsedCommand);
        var cmd = result.ParsedCommand!;
        Assert.Equal(CommandType.Export, cmd.Type);
        Assert.Equal(3, cmd.Arguments.Count);
        Assert.Equal("all", cmd.Arguments[0], ignoreCase: true);
        Assert.Equal("txt", cmd.Arguments[1], ignoreCase: true);
        Assert.Equal("C:\\custom\\path", cmd.Arguments[2]);
    }

    /// <summary>
    /// T004: Contract test for :export all with invalid format.
    /// Expected to FAIL until T013 implementation.
    /// </summary>
    [Fact]
    public void ParseCommand_ExportAll_InvalidFormat_ReturnsFailure()
    {
        // Arrange
        var parser = new CommandParserService();
        var settings = new SettingsData();

        // Act
        var result = parser.ParseInput(":export all xml C:\\path", settings);

        // Assert - CONTRACT REQUIREMENT
        // Invalid formats should be rejected with clear error message
        Assert.False(result.IsSuccess, "Invalid format 'xml' should cause failure");
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("format", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Valid formats are only: json, txt
        // xml, csv, pdf, etc. should all fail validation

        // NOTE: This test WILL FAIL because validation logic not implemented (T013)
    }

    /// <summary>
    /// T005: Contract test for backward compatibility - :export without "all".
    /// This test verifies existing :export behavior is preserved.
    /// MAY PASS initially if existing logic is correct, or FAIL if broken.
    /// </summary>
    [Fact]
    public void ParseCommand_Export_WithoutAll_UsesExistingLogic()
    {
        // Arrange
        var parser = new CommandParserService();
        var settings = new SettingsData();

        // Act
        var result = parser.ParseInput(":export json C:\\exports", settings);

        // Assert - BACKWARD COMPATIBILITY CONTRACT
        Assert.True(result.Success, "Existing :export command must still work");
        Assert.Equal("export", result.Command?.ToLowerInvariant());

        // Should NOT contain "all" parameter
        Assert.DoesNotContain(result.Arguments, arg => arg.Equals("all", StringComparison.OrdinalIgnoreCase));

        // Should contain format and path as before
        Assert.Contains("json", result.Arguments, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(result.Arguments, arg => arg.Contains("exports"));

        // NOTE: This test verifies backward compatibility.
        // It should PASS if existing CommandParserService correctly handles :export
    }

    /// <summary>
    /// Additional contract test: :export all with minimal args (format only).
    /// </summary>
    [Fact]
    public void ParseCommand_ExportAll_FormatOnly_ReturnsSuccess()
    {
        // Arrange
        var parser = new CommandParserService();
        var settings = new SettingsData
        {
            ExportFormat = ExportTypes.json,
            ExportPath = "C:\\defaults"
        };

        // Act
        var result = parser.ParseInput(":export all json", settings);

        // Assert
        Assert.True(result.IsSuccess, ":export all json should succeed (path from settings)");
        Assert.NotNull(result.ParsedCommand);
        var cmd = result.ParsedCommand!;
        Assert.Equal(CommandType.Export, cmd.Type);
        Assert.Equal(3, cmd.Arguments.Count);
        Assert.Equal("all", cmd.Arguments[0], ignoreCase: true);
        Assert.Equal("json", cmd.Arguments[1], ignoreCase: true);
        Assert.Equal("C:\\defaults", cmd.Arguments[2]);
    }

    /// <summary>
    /// Contract test: Case insensitivity for "all" parameter.
    /// </summary>
    [Fact]
    public void ParseCommand_ExportAll_CaseInsensitive_ReturnsSuccess()
    {
        // Arrange
        var parser = new CommandParserService();
        var settings = new SettingsData
        {
            ExportFormat = ExportTypes.json,
            ExportPath = "C:\\defaults"
        };

        // Act - Try various case combinations
        var resultLower = parser.ParseInput(":export all", settings);
        var resultUpper = parser.ParseInput(":export ALL", settings);
        var resultMixed = parser.ParseInput(":export All", settings);

        // Assert - Case insensitivity contract
        Assert.True(resultLower.IsSuccess, "Lowercase 'all' should work");
        Assert.True(resultUpper.IsSuccess, "Uppercase 'ALL' should work");
        Assert.True(resultMixed.IsSuccess, "Mixed case 'All' should work");
    }
}
