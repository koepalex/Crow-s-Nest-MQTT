using System.Threading;
using System.Threading.Tasks;

namespace CrowsNestMqtt.UI.Commands;

/// <summary>
/// Interface for processing UI commands.
/// Provides command execution capabilities for the application.
/// </summary>
public interface ICommandProcessor
{
    /// <summary>
    /// Represents the result of a command execution.
    /// </summary>
    /// <param name="Success">Whether the command executed successfully</param>
    /// <param name="Message">Message describing the result or error</param>
    public record CommandExecutionResult(bool Success, string Message);

    /// <summary>
    /// Executes a command with the given arguments.
    /// </summary>
    /// <param name="command">The command to execute</param>
    /// <param name="arguments">Arguments for the command</param>
    /// <param name="cancellationToken">Token for cancelling the operation</param>
    /// <returns>Result of the command execution</returns>
    Task<CommandExecutionResult> ExecuteAsync(string command, string[] arguments, CancellationToken cancellationToken = default);
}