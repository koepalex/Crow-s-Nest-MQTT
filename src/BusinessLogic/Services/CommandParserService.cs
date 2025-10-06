namespace CrowsNestMqtt.BusinessLogic.Services;

using System.Text; // Added for StringBuilder
using CrowsNestMqtt.BusinessLogic.Commands;
using CrowsNestMqtt.BusinessLogic.Configuration;
using CrowsNestMqtt.BusinessLogic.Exporter;

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

    internal CommandResult ParseCommand(string input, SettingsData settingsData)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            // Treat empty input as a search for nothing (effectively clearing search)
            return CommandResult.SuccessSearch(string.Empty);
        }

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
                {
                    string serverPortPattern = @"^([a-zA-Z0-9][-a-zA-Z0-9.]*[a-zA-Z0-9]|\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}):(\d{1,5})$";
                    Func<string, bool> isValidServerPortFormat = (arg) => System.Text.RegularExpressions.Regex.IsMatch(arg, serverPortPattern);
                    Func<string, bool> isValidPortRange = (portStr) => int.TryParse(portStr, out int p) && p > 0 && p <= 65535;

                    if (arguments.Count == 0)
                    {
                        // :connect (use all from settings)
                        return CommandResult.SuccessCommand(new ParsedCommand(CommandType.Connect, new List<string>()));
                    }
                    else if (arguments.Count == 1)
                    {
                        // :connect <arg1>
                        // <arg1> must be server:port
                        if (isValidServerPortFormat(arguments[0]))
                        {
                            string[] sp = arguments[0].Split(':');
                            if (sp.Length == 2 && isValidPortRange(sp[1]))
                            {
                                return CommandResult.SuccessCommand(new ParsedCommand(CommandType.Connect, new List<string> { arguments[0] }));
                            }
                            else
                            {
                                return CommandResult.Failure("Invalid server:port format in :connect command. Port must be between 1 and 65535.");
                            }
                        }
                        else
                        {
                            return CommandResult.Failure("Invalid arguments for :connect. If one argument is provided, it must be in 'server:port' format.");
                        }
                    }
                    else if (arguments.Count == 2)
                    {
                        // :connect <arg1> <arg2>
                        // <arg1> must be server:port, <arg2> is username
                        if (isValidServerPortFormat(arguments[0]))
                        {
                             string[] sp = arguments[0].Split(':');
                             if (sp.Length == 2 && isValidPortRange(sp[1]))
                             {
                                return CommandResult.SuccessCommand(new ParsedCommand(CommandType.Connect, new List<string> { arguments[0], arguments[1] }));
                             }
                             else
                             {
                                return CommandResult.Failure("Invalid server:port format in :connect command. Port must be between 1 and 65535.");
                             }
                        }
                        else
                        {
                             return CommandResult.Failure("Invalid arguments for :connect. First argument must be in 'server:port' format when providing username.");
                        }
                    }
                    else if (arguments.Count == 3)
                    {
                        // :connect <arg1> <arg2> <arg3>
                        // <arg1> server:port, <arg2> username, <arg3> password
                        if (isValidServerPortFormat(arguments[0]))
                        {
                            string[] sp = arguments[0].Split(':');
                            if (sp.Length == 2 && isValidPortRange(sp[1]))
                            {
                                return CommandResult.SuccessCommand(new ParsedCommand(CommandType.Connect, new List<string> { arguments[0], arguments[1], arguments[2] }));
                            }
                            else
                            {
                                return CommandResult.Failure("Invalid server:port format in :connect command. Port must be between 1 and 65535.");
                            }
                        }
                        else
                        {
                            return CommandResult.Failure("Invalid arguments for :connect. First argument must be in 'server:port' format when providing username and password.");
                        }
                    }
                    else // arguments.Count > 3
                    {
                        return CommandResult.Failure("Invalid arguments for :connect. Too many arguments. Expected: :connect [<server:port>] [<username>] [<password>]");
                    }
                }

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
                else if (arguments.Count == 0 && !string.IsNullOrEmpty(settingsData.ExportPath) && (settingsData.ExportFormat == ExportTypes.json || settingsData.ExportFormat == ExportTypes.txt))
                {
#pragma warning disable CS8601 // Possible null reference assignment.
                    return CommandResult.SuccessCommand(new ParsedCommand(CommandType.Export, [settingsData.ExportFormat!.ToString(), settingsData.ExportPath]));
#pragma warning restore CS8601 // Possible null reference assignment.
                }
                return CommandResult.Failure("Invalid arguments for :export. Expected: :export <format:{json|txt}> <filepath>");

            case "filter":
                // TODO: Implement regex validation
                if (arguments.Count <= 1) // Allow zero arguments for clearing the filter
                {
                    return CommandResult.SuccessCommand(new ParsedCommand(CommandType.Filter, arguments));
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
                if (arguments.Count <= 1)
                {
                    return CommandResult.SuccessCommand(new ParsedCommand(CommandType.Search, arguments));
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
                    else if (viewType == "image")
                    {
                        return CommandResult.SuccessCommand(new ParsedCommand(CommandType.ViewImage, arguments));
                    }
                    else if (viewType == "video")
                    {
                        return CommandResult.SuccessCommand(new ParsedCommand(CommandType.ViewVideo, arguments));
                    }
                    else if (viewType == "hex")
                    {
                        return CommandResult.SuccessCommand(new ParsedCommand(CommandType.ViewHex, arguments));
                    }
                }
                return CommandResult.Failure("Invalid arguments for :view. Expected: :view <raw|json|image|video|hex>");

            case "setuser":
                if (arguments.Count == 1)
                {
                    return CommandResult.SuccessCommand(new ParsedCommand(CommandType.SetUser, arguments));
                }
                return CommandResult.Failure("Invalid arguments for :setuser. Expected: :setuser <username>");

            case "setpass":
                if (arguments.Count == 1)
                {
                    return CommandResult.SuccessCommand(new ParsedCommand(CommandType.SetPassword, arguments));
                }
                return CommandResult.Failure("Invalid arguments for :setpass. Expected: :setpass <password>");

            case "setauthmode":
                if (arguments.Count == 1)
                {
                    string mode = arguments[0].ToLowerInvariant();
                    if (mode == "anonymous" || mode == "userpass" || mode == "enhanced")
                    {
                        return CommandResult.SuccessCommand(new ParsedCommand(CommandType.SetAuthMode, arguments));
                    }
                }
                return CommandResult.Failure("Invalid arguments for :setauthmode. Expected: :setauthmode <anonymous|userpass|enhanced>");

            case "setauthmethod":
                if (arguments.Count == 1)
                {
                    return CommandResult.SuccessCommand(new ParsedCommand(CommandType.SetAuthMethod, arguments));
                }
                return CommandResult.Failure("Invalid arguments for :setauthmethod. Expected: :setauthmethod <method>");

            case "setauthdata":
                if (arguments.Count == 1)
                {
                    return CommandResult.SuccessCommand(new ParsedCommand(CommandType.SetAuthData, arguments));
                }
                return CommandResult.Failure("Invalid arguments for :setauthdata. Expected: :setauthdata <data>");

            case "setusetls":
                if (arguments.Count == 1)
                {
                    var arg = arguments[0].ToLowerInvariant();
                    if (arg == "true" || arg == "false")
                    {
                        return CommandResult.SuccessCommand(new ParsedCommand(CommandType.SetUseTls, arguments));
                    }
                    return CommandResult.Failure("Invalid argument for :setusetls. Expected: :setusetls <true|false>");
                }
                return CommandResult.Failure("Invalid arguments for :setusetls. Expected: :setusetls <true|false>");

            case "settings":
                if (arguments.Count == 0)
                {
                    return CommandResult.SuccessCommand(new ParsedCommand(CommandType.Settings, arguments));
                }
                return CommandResult.Failure("Invalid arguments for :settings. Expected: :settings");

            case "deletetopic":
                // :deletetopic [topic-pattern] [--confirm]
                if (arguments.Count == 0)
                {
                    // Use selected topic from UI
                    return CommandResult.SuccessCommand(new ParsedCommand(CommandType.DeleteTopic, arguments));
                }
                else if (arguments.Count == 1)
                {
                    // Topic pattern specified
                    return CommandResult.SuccessCommand(new ParsedCommand(CommandType.DeleteTopic, arguments));
                }
                else if (arguments.Count == 2 && arguments[1].ToLowerInvariant() == "--confirm")
                {
                    // Topic pattern with confirmation flag
                    return CommandResult.SuccessCommand(new ParsedCommand(CommandType.DeleteTopic, arguments));
                }
                return CommandResult.Failure("Invalid arguments for :deletetopic. Expected: :deletetopic [topic-pattern] [--confirm]");

            case "gotoresponse":
                // :gotoresponse
                if (arguments.Count == 0)
                {
                    // Use selected message from UI
                    return CommandResult.SuccessCommand(new ParsedCommand(CommandType.GotoResponse, arguments));
                }
                return CommandResult.Failure("Invalid arguments for :gotoresponse. Expected: :gotoresponse");

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
