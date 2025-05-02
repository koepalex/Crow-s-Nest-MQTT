namespace CrowsNestMqtt.BusinessLogic.Commands;

/// <summary>
/// Represents a command parsed from user input, including its type and arguments.
/// </summary>
public class ParsedCommand
{
    /// <summary>
    /// Gets the type of the command.
    /// </summary>
    public CommandType Type { get; }

    /// <summary>
    /// Gets the list of arguments provided with the command.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParsedCommand"/> class.
    /// </summary>
    /// <param name="type">The type of the command.</param>
    /// <param name="arguments">The arguments associated with the command.</param>
    public ParsedCommand(CommandType type, IReadOnlyList<string> arguments)
    {
        Type = type;
        Arguments = arguments ?? new List<string>();
    }
}