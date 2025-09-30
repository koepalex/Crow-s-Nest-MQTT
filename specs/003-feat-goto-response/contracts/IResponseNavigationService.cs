using System;
using System.Threading.Tasks;

namespace CrowsNestMQTT.BusinessLogic.Contracts
{
    /// <summary>
    /// Service contract for navigating from request messages to their corresponding responses.
    /// Handles topic navigation, message selection, and UI interaction for response discovery.
    /// </summary>
    public interface IResponseNavigationService
    {
        /// <summary>
        /// Navigates to the response topic and selects the specific response message
        /// corresponding to the given request message.
        /// </summary>
        /// <param name="requestMessageId">Unique identifier for the originating request message</param>
        /// <returns>Navigation result indicating success and selected message details</returns>
        Task<NavigationResult> NavigateToResponseAsync(string requestMessageId);

        /// <summary>
        /// Checks if navigation to response is currently possible for the given request.
        /// Used to determine if arrow icon should be enabled or disabled.
        /// </summary>
        /// <param name="requestMessageId">Unique identifier for the request message</param>
        /// <returns>True if navigation is possible, false otherwise</returns>
        Task<bool> CanNavigateToResponseAsync(string requestMessageId);

        /// <summary>
        /// Gets the navigation command text that would be executed for the given request.
        /// Used for command palette integration and user feedback.
        /// </summary>
        /// <param name="requestMessageId">Unique identifier for the request message</param>
        /// <returns>Command text (e.g., ":goto response/topic/name") or null if navigation not available</returns>
        Task<string?> GetNavigationCommandAsync(string requestMessageId);

        /// <summary>
        /// Registers a colon-prefixed command for navigating to response messages.
        /// Enables keyboard-driven navigation through the command palette.
        /// </summary>
        /// <param name="requestMessageId">Request message to navigate from</param>
        /// <param name="commandText">Custom command text (optional, auto-generated if null)</param>
        /// <returns>True if command was registered successfully</returns>
        Task<bool> RegisterNavigationCommandAsync(string requestMessageId, string? commandText = null);

        /// <summary>
        /// Executes navigation using a colon-prefixed command string.
        /// Supports commands like ":goto response" or ":gotoresponse [message-id]".
        /// </summary>
        /// <param name="command">Command string starting with colon</param>
        /// <returns>Navigation result or error details</returns>
        Task<NavigationResult> ExecuteNavigationCommandAsync(string command);

        /// <summary>
        /// Gets all available navigation commands for currently visible request messages.
        /// Used by command palette for discovery and auto-completion.
        /// </summary>
        /// <returns>Collection of available navigation commands with descriptions</returns>
        Task<NavigationCommand[]> GetAvailableNavigationCommandsAsync();

        /// <summary>
        /// Event raised when navigation is completed, successful or failed.
        /// UI components can subscribe for feedback and logging purposes.
        /// </summary>
        event EventHandler<NavigationCompletedEventArgs> NavigationCompleted;
    }

    /// <summary>
    /// Result of a navigation operation including success status and selected message details.
    /// </summary>
    public record NavigationResult
    {
        public bool Success { get; init; }
        public string? SelectedMessageId { get; init; }
        public string? SelectedTopic { get; init; }
        public string? ErrorMessage { get; init; }
        public NavigationError? ErrorType { get; init; }
        public DateTime NavigatedAt { get; init; } = DateTime.UtcNow;
        public TimeSpan NavigationDuration { get; init; }
    }

    /// <summary>
    /// Types of navigation errors that can occur during response navigation.
    /// </summary>
    public enum NavigationError
    {
        RequestNotFound,
        NoCorrelationData,
        ResponseTopicNotSubscribed,
        ResponseNotReceived,
        MultipleResponsesAmbiguous,
        TopicNavigationFailed,
        MessageSelectionFailed
    }

    /// <summary>
    /// Represents a navigation command available in the command palette.
    /// </summary>
    public record NavigationCommand
    {
        public string Command { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string RequestMessageId { get; init; } = string.Empty;
        public string ResponseTopic { get; init; } = string.Empty;
        public bool IsEnabled { get; init; } = true;
    }

    /// <summary>
    /// Event arguments for navigation completion notifications.
    /// </summary>
    public class NavigationCompletedEventArgs : EventArgs
    {
        public NavigationResult Result { get; init; } = new();
        public string RequestMessageId { get; init; } = string.Empty;
        public string? Command { get; init; }
        public bool WasCommandTriggered { get; init; }
    }
}