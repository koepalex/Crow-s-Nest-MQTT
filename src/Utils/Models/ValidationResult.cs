using System.Collections.Generic;

namespace CrowsNestMqtt.Utils.Models;

/// <summary>
/// Result of a validation operation.
/// Contains information about whether validation passed and any error messages.
/// </summary>
public record ValidationResult
{
    /// <summary>
    /// Whether the validation passed successfully.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// List of error messages if validation failed.
    /// Empty if validation passed.
    /// </summary>
    public IList<string> ErrorMessages { get; init; } = new List<string>();

    /// <summary>
    /// List of warning messages that don't prevent operation but should be noted.
    /// </summary>
    public IList<string> WarningMessages { get; init; } = new List<string>();

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <returns>ValidationResult with IsValid = true</returns>
    public static ValidationResult Success() => new() { IsValid = true };

    /// <summary>
    /// Creates a failed validation result with error messages.
    /// </summary>
    /// <param name="errorMessages">List of error messages</param>
    /// <returns>ValidationResult with IsValid = false and provided errors</returns>
    public static ValidationResult Failure(params string[] errorMessages) => new()
    {
        IsValid = false,
        ErrorMessages = errorMessages.ToList()
    };

    /// <summary>
    /// Creates a successful validation result with warning messages.
    /// </summary>
    /// <param name="warningMessages">List of warning messages</param>
    /// <returns>ValidationResult with IsValid = true and provided warnings</returns>
    public static ValidationResult SuccessWithWarnings(params string[] warningMessages) => new()
    {
        IsValid = true,
        WarningMessages = warningMessages.ToList()
    };
}