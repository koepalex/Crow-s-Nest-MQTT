using CrowsNestMqtt.BusinessLogic.Configuration;
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
            ExportFormat = CrowsNestMqtt.BusinessLogic.Exporter.ExportTypes.Json,
            ExportPath = "C:\\test\\exports"
        };

        // Act
        var result = parser.ParseInput(":export all", settings);

        // Assert - CONTRACT REQUIREMENT
        // When "all" parameter is present, command should succeed
        // and include "all" as first argument
        Assert.True(result.Success, "Command parsing should succeed for :export all");
        Assert.Equal("export", result.Command?.ToLowerInvariant());

        // The arguments should include "all" indicator
        // Exact format TBD during implementation, but "all" must be present
        Assert.Contains(result.Arguments, arg => arg.Equals("all", StringComparison.OrdinalIgnoreCase));

        // NOTE: This test WILL FAIL because CommandParserService
        // doesn't yet parse the "all" parameter (implementation in T013)
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
        Assert.True(result.Success, "Command parsing should succeed for :export all with params");
        Assert.Equal("export", result.Command?.ToLowerInvariant());

        // Arguments should be: ["all", "txt", "C:\\custom\\path"]
        Assert.Contains("all", result.Arguments, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("txt", result.Arguments, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(result.Arguments, arg => arg.Contains("custom"));

        // NOTE: This test WILL FAIL because :export all parsing not implemented (T013)
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
        Assert.False(result.Success, "Invalid format 'xml' should cause failure");
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
        var settings = new SettingsData();

        // Act
        var result = parser.ParseInput(":export all json", settings);

        // Assert
        Assert.True(result.Success, ":export all json should succeed (path from settings)");
        Assert.Contains("all", result.Arguments, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("json", result.Arguments, StringComparer.OrdinalIgnoreCase);

        // NOTE: This test WILL FAIL - not implemented (T013)
    }

    /// <summary>
    /// Contract test: Case insensitivity for "all" parameter.
    /// </summary>
    [Fact]
    public void ParseCommand_ExportAll_CaseInsensitive_ReturnsSuccess()
    {
        // Arrange
        var parser = new CommandParserService();
        var settings = new SettingsData();

        // Act - Try various case combinations
        var resultLower = parser.ParseInput(":export all", settings);
        var resultUpper = parser.ParseInput(":export ALL", settings);
        var resultMixed = parser.ParseInput(":export All", settings);

        // Assert - Case insensitivity contract
        Assert.True(resultLower.Success, "Lowercase 'all' should work");
        Assert.True(resultUpper.Success, "Uppercase 'ALL' should work");
        Assert.True(resultMixed.Success, "Mixed case 'All' should work");

        // NOTE: This test WILL FAIL - not implemented (T013)
    }
}
