using System;
using System.Collections.Generic;
using System.Linq;
using System.Text; // Added for StringBuilder
using CrowsNestMqtt.Businesslogic.Commands;
using CrowsNestMqtt.Businesslogic.Configuration;

namespace CrowsNestMqtt.Businesslogic.Services;

/// <summary>
/// Service responsible for parsing user input into commands or search terms.
/// </summary>
public class CommandParserService : ICommandParserService
{
    private const char CommandPrefix = ':';

    /// <inheritdoc />
    public CommandResult ParseInput(string input, SettingsData settingsData)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            // Treat empty input as a search for nothing (effectively clearing search)
            return CommandResult.SuccessSearch(string.Empty);
        }

        input = input.Trim();

        if (input.StartsWith(CommandPrefix))
        {
            return ParseCommand(input, settingsData);
        }
        else
        {
            // Input doesn't start with prefix, treat as search term
            return CommandResult.SuccessSearch(input);
        }
    }

    private CommandResult ParseCommand(string input, SettingsData settingsData)
    {
        // Remove prefix and parse command + arguments using helper
        var parts = SplitArguments(input.Substring(1)).ToList();

        if (parts.Count == 0)
        {
            return CommandResult.Failure("Empty command.");
        }

        var commandKeyword = parts[0].ToLowerInvariant();
        var arguments = parts.Skip(1).ToList(); // Materialize arguments

        switch (commandKeyword)
        {
            case "connect":
                // TODO: Implement more robust argument validation for :connect <server_address:port> (e.g., Uri parsing)
                if (arguments.Count == 1)
                {
                    return CommandResult.SuccessCommand(new ParsedCommand(CommandType.Connect, arguments));
                } 
                else if (!string.IsNullOrEmpty(settingsData.Hostname))
                {
                    return CommandResult.SuccessCommand(new ParsedCommand(CommandType.Connect, [$"{settingsData.Hostname}:{settingsData.Port}"]));
                }
                return CommandResult.Failure("Invalid arguments for :connect. Expected: :connect <server_address:port>");

            case "disconnect":
                if (arguments.Count == 0)
                {
                    return CommandResult.SuccessCommand(new ParsedCommand(CommandType.Disconnect, arguments));
                }
                return CommandResult.Failure("Invalid arguments for :disconnect. Expected: :disconnect");

            case "export":
                // TODO: Implement more robust file path validation
                if (arguments.Count == 2)
                {
                    // Basic validation for format
                    string format = arguments[0].ToLowerInvariant();
                    if (format != "json" && format != "txt") 
                    {
                        return CommandResult.Failure("Invalid format for :export. Expected 'json' or 'txt'.");
                    }
                    // Argument 1 is filepath
                    return CommandResult.SuccessCommand(new ParsedCommand(CommandType.Export, arguments));
                }
                else if (!string.IsNullOrEmpty(settingsData.ExportPath) && (settingsData.ExportFormat == Exporter.ExportTypes.Json || settingsData.ExportFormat == Exporter.ExportTypes.Text))
                {
#pragma warning disable CS8601 // Possible null reference assignment.
                    return CommandResult.SuccessCommand(new ParsedCommand(CommandType.Export, [settingsData.ExportFormat!.ToString(), settingsData.ExportPath]));
#pragma warning restore CS8601 // Possible null reference assignment.
                }
                return CommandResult.Failure("Invalid arguments for :export. Expected: :export <format:{json|txt}> <filepath>");

            case "filter":
                // TODO: Implement regex validation
                if (arguments.Count >= 0) // Allow zero arguments for clearing the filter
                {
                	// If arguments exist, join them; otherwise, pass null/empty to indicate clearing
                	string? pattern = arguments.Count > 0 ? string.Join(" ", arguments) : null;
                	// Pass the pattern (or null) as the single argument
                	return CommandResult.SuccessCommand(new ParsedCommand(CommandType.Filter, pattern != null ? new List<string> { pattern } : new List<string>()));
                }
                // This part should technically not be reached if Count >= 0 is allowed, but keep for safety.
                return CommandResult.Failure("Invalid arguments for :filter. Expected: :filter [pattern]");

            case "clear":
                if (arguments.Count == 0)
                {
                    return CommandResult.SuccessCommand(new ParsedCommand(CommandType.Clear, arguments));
                }
                return CommandResult.Failure("Invalid arguments for :clear_messages. Expected: :clear_messages");

            case "help":
                // Allow 0 or 1 argument (:help or :help <command>)
                if (arguments.Count <= 1)
                {
                    return CommandResult.SuccessCommand(new ParsedCommand(CommandType.Help, arguments));
                }
                return CommandResult.Failure("Invalid arguments for :help. Expected: :help [command_name]");

            case "pause":
                if (arguments.Count == 0)
                {
                    return CommandResult.SuccessCommand(new ParsedCommand(CommandType.Pause, arguments));
                }
                return CommandResult.Failure("Invalid arguments for :pause. Expected: :pause");

            case "resume":
                if (arguments.Count == 0)
                {
                    return CommandResult.SuccessCommand(new ParsedCommand(CommandType.Resume, arguments));
                }
                return CommandResult.Failure("Invalid arguments for :resume. Expected: :resume");

            case "copy":
                if (arguments.Count == 0)
                {
                    return CommandResult.SuccessCommand(new ParsedCommand(CommandType.Copy, arguments));
                }
                return CommandResult.Failure("Invalid arguments for :copy. Expected: :copy");

            case "search":
                // Allow zero arguments to clear the search
                if (arguments.Count >= 0)
                {
                    // Join arguments if any, otherwise pass empty string to clear
                    string searchTerm = arguments.Count > 0 ? string.Join(" ", arguments) : string.Empty;
                    return CommandResult.SuccessCommand(new ParsedCommand(CommandType.Search, new List<string> { searchTerm }));
                }
                // This part should not be reachable if Count >= 0 is allowed
                // This part should not be reachable if Count >= 0 is allowed
                return CommandResult.Failure("Invalid arguments for :search. Expected: :search [term]");

            case "expand":
                if (arguments.Count == 0)
                {
                    return CommandResult.SuccessCommand(new ParsedCommand(CommandType.Expand, arguments));
                }
                return CommandResult.Failure("Invalid arguments for :expand. Expected: :expand");

            case "collapse":
                if (arguments.Count == 0)
                {
                    return CommandResult.SuccessCommand(new ParsedCommand(CommandType.Collapse, arguments));
                }
                return CommandResult.Failure("Invalid arguments for :collapse. Expected: :collapse");

            case "view":
                if (arguments.Count == 1)
                {
                    var viewType = arguments[0].ToLowerInvariant();
                    if (viewType == "raw")
                    {
                        return CommandResult.SuccessCommand(new ParsedCommand(CommandType.ViewRaw, arguments));
                    }
                    else if (viewType == "json")
                    {
                        return CommandResult.SuccessCommand(new ParsedCommand(CommandType.ViewJson, arguments));
                    }
                }
                return CommandResult.Failure("Invalid arguments for :view. Expected: :view <raw|json>");

            default:
                return CommandResult.Failure($"Unknown command: '{commandKeyword}'");
        }
    }

    // Helper method to split arguments, handling basic double quotes
    private static IEnumerable<string> SplitArguments(string commandLine) // Corrected '<' and '>'
    {
        var parts = new List<string>(); // Corrected '<' and '>'
        var currentPart = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < commandLine.Length; i++) // Corrected '<'
        {
            char c = commandLine[i];

            if (c == '"')
            {
                // Toggle quote state. Don't add quote character to the part.
                inQuotes = !inQuotes;
            }
            else if (c == ' ' && !inQuotes) // Corrected '&&'
            {
                // If not in quotes, space is a separator
                if (currentPart.Length > 0) // Corrected '>'
                {
                    parts.Add(currentPart.ToString());
                    currentPart.Clear();
                }
            }
            else
            {
                // Add character to the current part
                currentPart.Append(c);
            }
        }

        // Add the last part if it's not empty
        if (currentPart.Length > 0) // Corrected '>'
        {
            parts.Add(currentPart.ToString());
        }

        // Return non-empty parts only (already handled by logic, but good practice)
        return parts.Where(p => !string.IsNullOrWhiteSpace(p));
    }
}