using System;
using System.Threading.Tasks;
using CrowsNestMqtt.BusinessLogic.Models;
using CrowsNestMqtt.UI.ViewModels; // Added for ResponseIconViewModel

namespace CrowsNestMqtt.UI.Contracts
{
    /// <summary>
    /// Service contract for managing response status icons in the UI.
    /// Handles icon rendering, state transitions, and user interaction for request-response patterns.
    /// </summary>
    public interface IResponseIconService
    {
        /// <summary>
        /// Creates an icon view model for a request message with response-topic metadata.
        /// Returns null for messages without response expectations.
        /// </summary>
        /// <param name="requestMessageId">Unique identifier for the request message</param>
        /// <param name="hasResponseTopic">Whether the message contains response-topic metadata</param>
        /// <param name="isResponseTopicSubscribed">Whether the response topic is currently subscribed</param>
        /// <returns>Icon view model or null if no icon should be displayed</returns>
        Task<ResponseIconViewModel?> CreateIconViewModelAsync(string requestMessageId, bool hasResponseTopic, bool isResponseTopicSubscribed);

        /// <summary>
        /// Updates the icon status for a request message based on correlation state changes.
        /// Automatically transitions between pending, received, and disabled states.
        /// </summary>
        /// <param name="requestMessageId">Unique identifier for the request message</param>
        /// <param name="newStatus">New response status to display</param>
        /// <returns>True if icon was updated successfully</returns>
        Task<bool> UpdateIconStatusAsync(string requestMessageId, ResponseStatus newStatus);

        /// <summary>
        /// Handles icon click events and triggers navigation to response messages.
        /// Only enabled for icons in "Received" status.
        /// </summary>
        /// <param name="requestMessageId">Request message associated with the clicked icon</param>
        /// <returns>Click handling result</returns>
        Task<IconClickResult> HandleIconClickAsync(string requestMessageId);

        /// <summary>
        /// Gets the current icon view model for a request message.
        /// Used for UI binding and state synchronization.
        /// </summary>
        /// <param name="requestMessageId">Unique identifier for the request message</param>
        /// <returns>Current icon view model or null if no icon exists</returns>
        Task<ResponseIconViewModel?> GetIconViewModelAsync(string requestMessageId);

        /// <summary>
        /// Removes the icon view model for a request message.
        /// Called when messages are cleared or filtered out of view.
        /// </summary>
        /// <param name="requestMessageId">Unique identifier for the request message</param>
        /// <returns>True if icon was removed successfully</returns>
        Task<bool> RemoveIconAsync(string requestMessageId);

        /// <summary>
        /// Gets icon configuration including paths, colors, and styling information.
        /// Used for theme support and customization.
        /// </summary>
        /// <returns>Current icon configuration settings</returns>
        Task<IconConfiguration> GetIconConfigurationAsync();

        /// <summary>
        /// Updates icon configuration for theme changes or user customization.
        /// </summary>
        /// <param name="configuration">New icon configuration</param>
        /// <returns>True if configuration was applied successfully</returns>
        Task<bool> UpdateIconConfigurationAsync(IconConfiguration configuration);

        /// <summary>
        /// Event raised when an icon's status changes for UI animation and feedback.
        /// </summary>
        event EventHandler<IconStatusChangedEventArgs> IconStatusChanged;

        /// <summary>
        /// Event raised when an icon is clicked, before navigation is attempted.
        /// UI can subscribe for user feedback like hover effects or loading indicators.
        /// </summary>
        event EventHandler<IconClickedEventArgs> IconClicked;
    }

    /// <summary>
    /// Result of an icon click operation.
    /// </summary>
    public record IconClickResult
    {
        public bool Handled { get; init; }
        public bool NavigationTriggered { get; init; }
        public string? ErrorMessage { get; init; }
        public string? NavigationCommand { get; init; }
    }

    /// <summary>
    /// Configuration for icon appearance and behavior.
    /// </summary>
    public record IconConfiguration
    {
        public string ClockIconPath { get; init; } = string.Empty;
        public string ArrowIconPath { get; init; } = string.Empty;
        public string DisabledClockIconPath { get; init; } = string.Empty;
        public string IconColor { get; init; } = "#666666";
        public string HoverColor { get; init; } = "#333333";
        public string DisabledColor { get; init; } = "#CCCCCC";
        public double IconSize { get; init; } = 16.0;
        public bool EnableHoverEffects { get; init; } = true;
        public bool EnableClickAnimation { get; init; } = true;
    }

    /// <summary>
    /// Event arguments for icon status change notifications.
    /// </summary>
    public class IconStatusChangedEventArgs : EventArgs
    {
        public string RequestMessageId { get; init; } = string.Empty;
        public ResponseStatus OldStatus { get; init; }
        public ResponseStatus NewStatus { get; init; }
        public string IconPath { get; init; } = string.Empty;
        public DateTime ChangedAt { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Event arguments for icon click notifications.
    /// </summary>
    public class IconClickedEventArgs : EventArgs
    {
        public string RequestMessageId { get; init; } = string.Empty;
        public ResponseStatus CurrentStatus { get; init; }
        public bool IsNavigationEnabled { get; init; }
        public DateTime ClickedAt { get; init; } = DateTime.UtcNow;
    }
}