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
                else if (!string.IsNullOrEmpty(settingsData.ExportPath) && (settingsData.ExportFormat == "json" || settingsData.ExportFormat == "txt"))
                {
                    return CommandResult.SuccessCommand(new ParsedCommand(CommandType.Export, [settingsData.ExportFormat, settingsData.ExportPath]));
                }
                return CommandResult.Failure("Invalid arguments for :export. Expected: :export <format:{json|txt}> <filepath>");

            case "filter":
                // TODO: Implement regex validation
                if (arguments.Count >= 1) // Pattern is the rest of the input
                {
                    // Re-join arguments in case pattern contains spaces (handled by SplitArguments now)
                    string pattern = string.Join(" ", arguments);
                    return CommandResult.SuccessCommand(new ParsedCommand(CommandType.Filter, new List<string> { pattern }));
                }
                return CommandResult.Failure("Invalid arguments for :filter. Expected: :filter <regex_pattern>");

            case "clear":
                if (arguments.Count == 0)
                {
                    return CommandResult.SuccessCommand(new ParsedCommand(CommandType.Clear, arguments));
                }
                return CommandResult.Failure("Invalid arguments for :clear_messages. Expected: :clear_messages");

            case "help":
                if (arguments.Count == 0)
                {
                    return CommandResult.SuccessCommand(new ParsedCommand(CommandType.Help, arguments));
                }
                return CommandResult.Failure("Invalid arguments for :help. Expected: :help");

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