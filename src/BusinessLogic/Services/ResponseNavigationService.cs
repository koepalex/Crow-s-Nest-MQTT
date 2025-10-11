using System;
using System.Threading.Tasks;
using CrowsNestMqtt.BusinessLogic.Contracts;
using CrowsNestMqtt.BusinessLogic.Models;

namespace CrowsNestMqtt.BusinessLogic.Services
{
    /// <summary>
    /// Implementation of IResponseNavigationService for navigating to response messages.
    /// Coordinates with correlation service and UI navigation to provide seamless navigation.
    /// </summary>
    public class ResponseNavigationService : IResponseNavigationService
    {
        private readonly IMessageCorrelationService _correlationService;
        private readonly ITopicSubscriptionService _subscriptionService;
        private readonly IUINavigationService _uiNavigationService;

        /// <inheritdoc />
        public event EventHandler<NavigationCompletedEventArgs>? NavigationCompleted;

        public ResponseNavigationService(
            IMessageCorrelationService correlationService,
            ITopicSubscriptionService subscriptionService,
            IUINavigationService uiNavigationService)
        {
            _correlationService = correlationService ?? throw new ArgumentNullException(nameof(correlationService));
            _subscriptionService = subscriptionService ?? throw new ArgumentNullException(nameof(subscriptionService));
            _uiNavigationService = uiNavigationService ?? throw new ArgumentNullException(nameof(uiNavigationService));
        }

        /// <inheritdoc />
        public async Task<NavigationResult> NavigateToResponseAsync(string requestMessageId)
        {
            var startTime = DateTime.UtcNow;

            if (string.IsNullOrEmpty(requestMessageId))
            {
                return new NavigationResult
                {
                    Success = false,
                    ErrorMessage = "Request message ID cannot be null or empty",
                    ErrorType = NavigationError.RequestNotFound,
                    NavigatedAt = DateTime.UtcNow,
                    NavigationDuration = DateTime.UtcNow - startTime
                };
            }

            try
            {
                // Get correlation details
                var responseTopic = await _correlationService.GetResponseTopicAsync(requestMessageId);
                if (string.IsNullOrEmpty(responseTopic))
                {
                    var result = new NavigationResult
                    {
                        Success = false,
                        ErrorMessage = "No correlation found for request message",
                        ErrorType = NavigationError.NoCorrelationData,
                        NavigatedAt = DateTime.UtcNow,
                        NavigationDuration = DateTime.UtcNow - startTime
                    };
                    RaiseNavigationCompleted(requestMessageId, result, null);
                    return result;
                }

                // Check if topic is accessible
                var isSubscribed = await _subscriptionService.IsTopicSubscribedAsync(responseTopic);
                if (!isSubscribed)
                {
                    var result = new NavigationResult
                    {
                        Success = false,
                        ErrorMessage = $"Response topic '{responseTopic}' is not subscribed",
                        ErrorType = NavigationError.ResponseTopicNotSubscribed,
                        SelectedTopic = responseTopic,
                        NavigatedAt = DateTime.UtcNow,
                        NavigationDuration = DateTime.UtcNow - startTime
                    };
                    RaiseNavigationCompleted(requestMessageId, result, null);
                    return result;
                }

                // Get response status
                var status = await _correlationService.GetResponseStatusAsync(requestMessageId);

                NavigationResult navigationResult;

                if (status == ResponseStatus.Received)
                {
                    // Navigate to specific response messages
                    var responseMessageIds = await _correlationService.GetResponseMessageIdsAsync(requestMessageId);
                    if (responseMessageIds.Count > 0)
                    {
                        // Navigate to the latest response message
                        var latestResponseId = responseMessageIds[responseMessageIds.Count - 1];
                        await _uiNavigationService.NavigateToTopicAsync(responseTopic);
                        await _uiNavigationService.SelectMessageAsync(latestResponseId);

                        navigationResult = new NavigationResult
                        {
                            Success = true,
                            SelectedMessageId = latestResponseId,
                            SelectedTopic = responseTopic,
                            NavigatedAt = DateTime.UtcNow,
                            NavigationDuration = DateTime.UtcNow - startTime
                        };
                    }
                    else
                    {
                        // Status says received but no response IDs found
                        await _uiNavigationService.NavigateToTopicAsync(responseTopic);
                        navigationResult = new NavigationResult
                        {
                            Success = true,
                            SelectedTopic = responseTopic,
                            NavigatedAt = DateTime.UtcNow,
                            NavigationDuration = DateTime.UtcNow - startTime
                        };
                    }
                }
                else if (status == ResponseStatus.Pending)
                {
                    navigationResult = new NavigationResult
                    {
                        Success = false,
                        ErrorMessage = "Response not yet received",
                        ErrorType = NavigationError.ResponseNotReceived,
                        SelectedTopic = responseTopic,
                        NavigatedAt = DateTime.UtcNow,
                        NavigationDuration = DateTime.UtcNow - startTime
                    };
                }
                else
                {
                    navigationResult = new NavigationResult
                    {
                        Success = false,
                        ErrorMessage = "Navigation is disabled for this correlation",
                        ErrorType = NavigationError.TopicNavigationFailed,
                        NavigatedAt = DateTime.UtcNow,
                        NavigationDuration = DateTime.UtcNow - startTime
                    };
                }

                RaiseNavigationCompleted(requestMessageId, navigationResult, null);
                return navigationResult;
            }
            catch (Exception ex)
            {
                var result = new NavigationResult
                {
                    Success = false,
                    ErrorMessage = $"Navigation failed: {ex.Message}",
                    ErrorType = NavigationError.TopicNavigationFailed,
                    NavigatedAt = DateTime.UtcNow,
                    NavigationDuration = DateTime.UtcNow - startTime
                };
                RaiseNavigationCompleted(requestMessageId, result, null);
                return result;
            }
        }

        /// <inheritdoc />
        public async Task<bool> CanNavigateToResponseAsync(string requestMessageId)
        {
            if (string.IsNullOrEmpty(requestMessageId))
                return false;

            try
            {
                // Check if correlation exists
                var responseTopic = await _correlationService.GetResponseTopicAsync(requestMessageId);
                if (string.IsNullOrEmpty(responseTopic))
                    return false;

                // Check if topic is accessible
                var isSubscribed = await _subscriptionService.IsTopicSubscribedAsync(responseTopic);
                if (!isSubscribed)
                    return false;

                // Check if status allows navigation
                var status = await _correlationService.GetResponseStatusAsync(requestMessageId);
                return status == ResponseStatus.Received;
            }
            catch
            {
                return false;
            }
        }

        /// <inheritdoc />
        public Task<string?> GetNavigationCommandAsync(string requestMessageId)
        {
            if (string.IsNullOrEmpty(requestMessageId))
                return Task.FromResult<string?>(null);

            return Task.FromResult<string?>($":gotoresponse {requestMessageId}");
        }

        /// <inheritdoc />
        public Task<bool> RegisterNavigationCommandAsync(string requestMessageId, string? commandText = null)
        {
            // Not implemented yet - command registration would be handled by command palette service
            return Task.FromResult(false);
        }

        /// <inheritdoc />
        public async Task<NavigationResult> ExecuteNavigationCommandAsync(string command)
        {
            if (string.IsNullOrEmpty(command))
            {
                return new NavigationResult
                {
                    Success = false,
                    ErrorMessage = "Command cannot be null or empty",
                    ErrorType = NavigationError.RequestNotFound
                };
            }

            // Parse command like ":gotoresponse message-id"
            var parts = command.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                return new NavigationResult
                {
                    Success = false,
                    ErrorMessage = "Invalid command format. Expected: :gotoresponse <message-id>",
                    ErrorType = NavigationError.RequestNotFound
                };
            }

            var requestMessageId = parts[1];
            return await NavigateToResponseAsync(requestMessageId);
        }

        /// <inheritdoc />
        public Task<NavigationCommand[]> GetAvailableNavigationCommandsAsync()
        {
            // Not implemented yet - would query correlation service for all active correlations
            return Task.FromResult(Array.Empty<NavigationCommand>());
        }

        /// <summary>
        /// Raises the NavigationCompleted event.
        /// </summary>
        private void RaiseNavigationCompleted(string requestMessageId, NavigationResult result, string? command)
        {
            NavigationCompleted?.Invoke(this, new NavigationCompletedEventArgs
            {
                RequestMessageId = requestMessageId,
                Result = result,
                Command = command,
                WasCommandTriggered = !string.IsNullOrEmpty(command)
            });
        }
    }

    /// <summary>
    /// Interface for topic subscription checking (dependency).
    /// This would be implemented by the MQTT connection service.
    /// </summary>
    public interface ITopicSubscriptionService
    {
        /// <summary>
        /// Checks if a topic is currently subscribed.
        /// </summary>
        /// <param name="topicName">The topic name to check.</param>
        /// <returns>True if subscribed, false otherwise.</returns>
        Task<bool> IsTopicSubscribedAsync(string topicName);
    }

    /// <summary>
    /// Interface for UI navigation operations (dependency).
    /// This would be implemented by the UI layer.
    /// </summary>
    public interface IUINavigationService
    {
        /// <summary>
        /// Navigates to a specific topic in the UI.
        /// </summary>
        /// <param name="topicName">The topic to navigate to.</param>
        Task NavigateToTopicAsync(string topicName);

        /// <summary>
        /// Selects a specific message in the current topic view.
        /// </summary>
        /// <param name="messageId">The message ID to select.</param>
        Task SelectMessageAsync(string messageId);
    }
}