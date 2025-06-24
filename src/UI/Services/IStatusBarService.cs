namespace CrowsNestMqtt.UI.Services;

/// <summary>
/// Interface for displaying status messages to the user, potentially with a timeout.
/// </summary>
public interface IStatusBarService
{
    /// <summary>
    /// Shows a status message.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="duration">Optional duration after which the message might disappear.</param>
    void ShowStatus(string message, TimeSpan? duration = null);
}