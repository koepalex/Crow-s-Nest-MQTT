#nullable enable
namespace CrowsNestMqtt.BusinessLogic.Commands;

using System;

/// <summary>
/// Represents the result of parsing user input.
/// It can indicate success (with a parsed command or a search term) or failure (with an error message).
/// </summary>
public class CommandResult
{
    /// <summary> Gets a value indicating whether the parsing was successful. </summary>
    public bool IsSuccess { get; }

    /// <summary> Gets the parsed command if the input was identified as a valid command. Null otherwise. </summary>
    public ParsedCommand? ParsedCommand { get; }

    /// <summary> Gets the search term if the input was not identified as a command. Null otherwise. </summary>
    public string? SearchTerm { get; }

    /// <summary> Gets the error message if parsing failed. Null otherwise. </summary>
    public string? ErrorMessage { get; }

    // Private constructor to force use of factory methods
    private CommandResult(bool isSuccess, ParsedCommand? parsedCommand, string? searchTerm, string? errorMessage)
    {
        if (isSuccess && parsedCommand == null && searchTerm == null)
            throw new ArgumentException("Successful result must have either a ParsedCommand or a SearchTerm.");
        if (!isSuccess && string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("Failed result must have an ErrorMessage.");
        if (isSuccess && !string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("Successful result cannot have an ErrorMessage.");
        if (parsedCommand != null && searchTerm != null)
            throw new ArgumentException("Result cannot be both a command and a search term.");


        IsSuccess = isSuccess;
        ParsedCommand = parsedCommand;
        SearchTerm = searchTerm;
        ErrorMessage = errorMessage;
    }

    /// <summary> Creates a success result representing a parsed command. </summary>
    public static CommandResult SuccessCommand(ParsedCommand command) =>
        new(true, command ?? throw new ArgumentNullException(nameof(command)), null, null);

    /// <summary> Creates a success result representing a search term. </summary>
    public static CommandResult SuccessSearch(string searchTerm) =>
        new(true, null, searchTerm ?? throw new ArgumentNullException(nameof(searchTerm)), null);

    /// <summary> Creates a failure result with an error message. </summary>
    public static CommandResult Failure(string message) =>
        new(false, null, null, message ?? throw new ArgumentNullException(nameof(message)));
}
#nullable restore