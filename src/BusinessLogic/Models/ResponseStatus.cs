using System;

namespace CrowsNestMqtt.BusinessLogic.Models
{
    /// <summary>
    /// Defines the visual state for request messages in the UI.
    /// Determines icon visibility and navigation behavior for response indicators.
    /// </summary>
    public enum ResponseStatus
    {
        /// <summary>
        /// No response icon shown - message is not a request-response message
        /// or response topic is not subscribed.
        /// </summary>
        Hidden,

        /// <summary>
        /// Clock icon shown - request message sent, awaiting response.
        /// Icon is clickable but navigation may show "no responses yet" state.
        /// </summary>
        Pending,

        /// <summary>
        /// Arrow icon shown - response message(s) received and correlated.
        /// Icon is clickable and will navigate to the response topic with message selection.
        /// </summary>
        Received,

        /// <summary>
        /// Disabled state - correlation has expired or response topic is no longer subscribed.
        /// Icon may be shown but is not clickable, or may be hidden entirely.
        /// </summary>
        NavigationDisabled
    }

    /// <summary>
    /// Extension methods for ResponseStatus enum to provide additional functionality.
    /// </summary>
    public static class ResponseStatusExtensions
    {
        /// <summary>
        /// Determines if the response status should show an icon in the UI.
        /// </summary>
        /// <param name="status">The response status to check.</param>
        /// <returns>True if an icon should be displayed, false otherwise.</returns>
        public static bool ShouldShowIcon(this ResponseStatus status)
        {
            return status switch
            {
                ResponseStatus.Hidden => false,
                ResponseStatus.Pending => true,
                ResponseStatus.Received => true,
                ResponseStatus.NavigationDisabled => true, // May show disabled icon
                _ => false
            };
        }

        /// <summary>
        /// Determines if the response icon should be clickable.
        /// </summary>
        /// <param name="status">The response status to check.</param>
        /// <returns>True if the icon should be clickable, false otherwise.</returns>
        public static bool IsClickable(this ResponseStatus status)
        {
            return status switch
            {
                ResponseStatus.Hidden => false,
                ResponseStatus.Pending => true,
                ResponseStatus.Received => true,
                ResponseStatus.NavigationDisabled => false,
                _ => false
            };
        }

        /// <summary>
        /// Gets the appropriate icon type for the response status.
        /// </summary>
        /// <param name="status">The response status to get icon for.</param>
        /// <returns>String identifier for the icon type.</returns>
        public static string GetIconType(this ResponseStatus status)
        {
            return status switch
            {
                ResponseStatus.Hidden => string.Empty,
                ResponseStatus.Pending => "clock",
                ResponseStatus.Received => "arrow",
                ResponseStatus.NavigationDisabled => "disabled",
                _ => string.Empty
            };
        }

        /// <summary>
        /// Determines the appropriate tooltip text for the response status.
        /// </summary>
        /// <param name="status">The response status to get tooltip for.</param>
        /// <returns>Tooltip text for the status.</returns>
        public static string GetTooltipText(this ResponseStatus status)
        {
            return status switch
            {
                ResponseStatus.Hidden => string.Empty,
                ResponseStatus.Pending => "Click to navigate to response topic (no responses yet)",
                ResponseStatus.Received => "Click to navigate to response message",
                ResponseStatus.NavigationDisabled => "Navigation disabled - topic not subscribed or correlation expired",
                _ => string.Empty
            };
        }

        /// <summary>
        /// Checks if this status represents a state where responses have been received.
        /// </summary>
        /// <param name="status">The response status to check.</param>
        /// <returns>True if responses have been received, false otherwise.</returns>
        public static bool HasResponses(this ResponseStatus status)
        {
            return status == ResponseStatus.Received;
        }

        /// <summary>
        /// Checks if this status represents a pending state waiting for responses.
        /// </summary>
        /// <param name="status">The response status to check.</param>
        /// <returns>True if in pending state, false otherwise.</returns>
        public static bool IsPending(this ResponseStatus status)
        {
            return status == ResponseStatus.Pending;
        }
    }
}