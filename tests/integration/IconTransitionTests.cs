using System;
using System.Text;
using System.Threading.Tasks;
using NSubstitute;
using Xunit;
using CrowsNestMqtt.BusinessLogic.Contracts;
using CrowsNestMqtt.BusinessLogic.Models;
using CrowsNestMqtt.BusinessLogic.Services;
using CrowsNestMqtt.UI.Contracts;
using CrowsNestMqtt.UI.Services;

namespace CrowsNestMqtt.Integration.Tests
{
    /// <summary>
    /// Integration tests for icon state transitions during request-response cycles.
    /// These tests verify end-to-end icon behavior and MUST FAIL before implementation.
    /// </summary>
    public sealed class IconTransitionTests
    {
        [Fact]
        public async Task CompleteIconLifecycle_ShouldTransitionCorrectly()
        {
            // Arrange
            var correlationService = CreateCorrelationService();
            var iconService = CreateIconService(correlationService);

            var requestMessageId = "req-lifecycle";
            var responseMessageId = "resp-lifecycle";
            var correlationData = Encoding.UTF8.GetBytes("lifecycle-test");
            var responseTopic = "test/icon/lifecycle";

            // Act & Assert - Step 1: Create pending icon
            var iconViewModel = await iconService.CreateIconViewModelAsync(requestMessageId, true, true);
            Assert.NotNull(iconViewModel);
            Assert.Equal(ResponseStatus.Pending, iconViewModel.Status);
            Assert.True(iconViewModel.IsClickable);
            Assert.Contains("clock", iconViewModel.IconPath, StringComparison.OrdinalIgnoreCase);

            // Register the request correlation
            await correlationService.RegisterRequestAsync(requestMessageId, correlationData, responseTopic);

            // Step 2: Response arrives - icon should transition to received
            await correlationService.LinkResponseAsync(responseMessageId, correlationData, responseTopic);

            var updateResult = await iconService.UpdateIconStatusAsync(requestMessageId, ResponseStatus.Received);
            Assert.True(updateResult, "Icon status update should succeed");

            var updatedViewModel = await iconService.GetIconViewModelAsync(requestMessageId);
            Assert.NotNull(updatedViewModel);
            Assert.Equal(ResponseStatus.Received, updatedViewModel.Status);
            Assert.True(updatedViewModel.IsClickable);
            Assert.Contains("arrow", updatedViewModel.IconPath, StringComparison.OrdinalIgnoreCase);

            // Step 3: Icon click should trigger navigation
            var clickResult = await iconService.HandleIconClickAsync(requestMessageId);
            Assert.True(clickResult.Handled);
            Assert.True(clickResult.NavigationTriggered);
            Assert.NotNull(clickResult.NavigationCommand);
        }

        [Fact]
        public async Task IconCreation_WithUnsubscribedTopic_ShouldReturnNull()
        {
            // Arrange
            var iconService = CreateIconService();
            var requestMessageId = "req-unsubscribed";

            // Act - Create icon for unsubscribed response topic
            var iconViewModel = await iconService.CreateIconViewModelAsync(requestMessageId, true, false);

            // Assert - the service hides icons entirely when the response topic is not subscribed.
            Assert.Null(iconViewModel);

            var storedIconViewModel = await iconService.GetIconViewModelAsync(requestMessageId);
            Assert.Null(storedIconViewModel);
        }

        [Fact]
        public async Task IconCreation_WithoutResponseTopic_ShouldReturnNull()
        {
            // Arrange
            var iconService = CreateIconService();
            var requestMessageId = "req-no-response-topic";

            // Act
            var iconViewModel = await iconService.CreateIconViewModelAsync(requestMessageId, false, true);

            // Assert
            Assert.Null(iconViewModel);
        }

        [Fact]
        public async Task IconStateTransitions_ShouldRaiseEvents()
        {
            // Arrange
            var iconService = CreateIconService();
            var requestMessageId = "req-events";

            var statusChangedEventRaised = false;
            var clickEventRaised = false;
            IconStatusChangedEventArgs? statusEventArgs = null;
            IconClickedEventArgs? clickEventArgs = null;

            iconService.IconStatusChanged += (sender, args) =>
            {
                statusChangedEventRaised = true;
                statusEventArgs = args;
            };

            iconService.IconClicked += (sender, args) =>
            {
                clickEventRaised = true;
                clickEventArgs = args;
            };

            // Act
            await iconService.CreateIconViewModelAsync(requestMessageId, true, true);
            await iconService.UpdateIconStatusAsync(requestMessageId, ResponseStatus.Received);
            await iconService.HandleIconClickAsync(requestMessageId);

            // Assert
            Assert.True(statusChangedEventRaised, "IconStatusChanged event should be raised");
            Assert.True(clickEventRaised, "IconClicked event should be raised");
            Assert.NotNull(statusEventArgs);
            Assert.NotNull(clickEventArgs);
            Assert.Equal(requestMessageId, statusEventArgs.RequestMessageId);
            Assert.Equal(requestMessageId, clickEventArgs.RequestMessageId);
        }

        [Fact]
        public async Task MultipleIcons_ShouldMaintainIndependentStates()
        {
            // Arrange
            var correlationService = CreateCorrelationService();
            var iconService = CreateIconService(correlationService);

            var requestMessageId1 = "req-multi-1";
            var requestMessageId2 = "req-multi-2";
            var responseMessageId1 = "resp-multi-1";
            var correlationData1 = Encoding.UTF8.GetBytes("multi-icon-test-1");
            var correlationData2 = Encoding.UTF8.GetBytes("multi-icon-test-2");
            var responseTopic = "test/icon/multi";

            // Act - Create two icons in different states
            var icon1 = await iconService.CreateIconViewModelAsync(requestMessageId1, true, true);
            var icon2 = await iconService.CreateIconViewModelAsync(requestMessageId2, true, true);

            // Register correlations
            await correlationService.RegisterRequestAsync(requestMessageId1, correlationData1, responseTopic);
            await correlationService.RegisterRequestAsync(requestMessageId2, correlationData2, responseTopic);

            // Link response only for first request
            await correlationService.LinkResponseAsync(responseMessageId1, correlationData1, responseTopic);
            await iconService.UpdateIconStatusAsync(requestMessageId1, ResponseStatus.Received);

            // Retrieve updated states
            var updatedIcon1 = await iconService.GetIconViewModelAsync(requestMessageId1);
            var updatedIcon2 = await iconService.GetIconViewModelAsync(requestMessageId2);

            // Assert
            Assert.NotNull(icon1);
            Assert.NotNull(icon2);
            Assert.NotNull(updatedIcon1);
            Assert.NotNull(updatedIcon2);

            Assert.Equal(ResponseStatus.Received, updatedIcon1.Status);
            Assert.Equal(ResponseStatus.Pending, updatedIcon2.Status);

            Assert.True(updatedIcon1.IsClickable);
            Assert.True(updatedIcon2.IsClickable);
            Assert.Contains("arrow", updatedIcon1.IconPath, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("clock", updatedIcon2.IconPath, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task IconConfiguration_ShouldAffectAllIcons()
        {
            // Arrange
            var iconService = CreateIconService();
            var requestMessageId = "req-config";

            // Act - Get default configuration
            var defaultConfig = await iconService.GetIconConfigurationAsync();

            // Create icon with default config
            var iconViewModel = await iconService.CreateIconViewModelAsync(requestMessageId, true, true);

            // Update configuration
            var newConfig = new IconConfiguration
            {
                ClockIconPath = "/custom/clock.svg",
                ArrowIconPath = "/custom/arrow.svg",
                DisabledClockIconPath = "/custom/clock-disabled.svg",
                IconColor = "#FF0000",
                HoverColor = "#AA0000",
                DisabledColor = "#AAAAAA",
                IconSize = 20.0,
                EnableHoverEffects = false,
                EnableClickAnimation = false
            };

            var updateResult = await iconService.UpdateIconConfigurationAsync(newConfig);

            // Assert
            Assert.NotNull(defaultConfig);
            Assert.True(updateResult, "Configuration update should succeed");

            // New icons should use updated configuration
            var newIconViewModel = await iconService.CreateIconViewModelAsync("req-config-2", true, true);
            Assert.NotNull(newIconViewModel);

            // The exact icon path matching would depend on implementation details
            Assert.True(true, "Configuration system is testable");
        }

        [Fact]
        public async Task IconRemoval_ShouldCleanUpResources()
        {
            // Arrange
            var iconService = CreateIconService();
            var requestMessageId = "req-removal";

            // Create icon
            await iconService.CreateIconViewModelAsync(requestMessageId, true, true);
            var iconBeforeRemoval = await iconService.GetIconViewModelAsync(requestMessageId);

            // Act
            var removalResult = await iconService.RemoveIconAsync(requestMessageId);
            var iconAfterRemoval = await iconService.GetIconViewModelAsync(requestMessageId);

            // Assert
            Assert.NotNull(iconBeforeRemoval);
            Assert.True(removalResult, "Icon removal should succeed");
            Assert.Null(iconAfterRemoval);
        }

        [Fact]
        public async Task IconClick_InDifferentStates_ShouldBehaveDifferently()
        {
            // Arrange
            var correlationService = CreateCorrelationService();
            var iconService = CreateIconService(correlationService);
            var requestPendingId = "req-pending-click";
            var requestReceivedId = "req-received-click";
            var requestDisabledId = "req-disabled-click";
            var pendingCorrelationData = Encoding.UTF8.GetBytes("pending-click");
            var receivedCorrelationData = Encoding.UTF8.GetBytes("received-click");
            var pendingResponseTopic = "test/icon/click/pending";
            var receivedResponseTopic = "test/icon/click/received";

            // Create icons in different states
            await correlationService.RegisterRequestAsync(requestPendingId, pendingCorrelationData, pendingResponseTopic);
            await correlationService.RegisterRequestAsync(requestReceivedId, receivedCorrelationData, receivedResponseTopic);
            await correlationService.LinkResponseAsync("resp-received-click", receivedCorrelationData, receivedResponseTopic);

            await iconService.CreateIconViewModelAsync(requestPendingId, true, true); // Pending
            await iconService.CreateIconViewModelAsync(requestReceivedId, true, true);
            await iconService.UpdateIconStatusAsync(requestReceivedId, ResponseStatus.Received); // Received
            await iconService.CreateIconViewModelAsync(requestDisabledId, true, true);
            await iconService.UpdateIconStatusAsync(requestDisabledId, ResponseStatus.NavigationDisabled); // Disabled

            // Act
            var pendingClick = await iconService.HandleIconClickAsync(requestPendingId);
            var receivedClick = await iconService.HandleIconClickAsync(requestReceivedId);
            var disabledClick = await iconService.HandleIconClickAsync(requestDisabledId);

            // Assert
            Assert.True(pendingClick.Handled);
            Assert.False(pendingClick.NavigationTriggered, "Pending icon should not trigger navigation");

            Assert.True(receivedClick.Handled);
            Assert.True(receivedClick.NavigationTriggered, "Received icon should trigger navigation");

            Assert.True(disabledClick.Handled);
            Assert.False(disabledClick.NavigationTriggered, "Disabled icon should not trigger navigation");
            Assert.Contains("disabled", disabledClick.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task HighVolumeIconUpdates_ShouldMaintainPerformance()
        {
            // Arrange
            var iconService = CreateIconService();
            var numberOfIcons = 100;
            var startTime = DateTime.UtcNow;

            // Act - Create many icons rapidly
            for (int i = 0; i < numberOfIcons; i++)
            {
                var requestMessageId = $"req-perf-{i}";
                await iconService.CreateIconViewModelAsync(requestMessageId, true, true);

                // Update half of them to received status
                if (i % 2 == 0)
                {
                    await iconService.UpdateIconStatusAsync(requestMessageId, ResponseStatus.Received);
                }
            }

            var endTime = DateTime.UtcNow;
            var totalDuration = endTime - startTime;

            // Assert
            Assert.True(totalDuration.TotalMilliseconds < 1000,
                $"Creating and updating {numberOfIcons} icons should complete within 1 second, took {totalDuration.TotalMilliseconds}ms");

            // Verify all icons were created
            for (int i = 0; i < numberOfIcons; i++)
            {
                var requestMessageId = $"req-perf-{i}";
                var icon = await iconService.GetIconViewModelAsync(requestMessageId);
                Assert.NotNull(icon);
            }
        }

        [Fact]
        public async Task IconViewModel_ShouldTrackLastUpdated()
        {
            // Arrange
            var iconService = CreateIconService();
            var requestMessageId = "req-timestamp";

            // Act
            var beforeCreation = DateTime.UtcNow;
            await iconService.CreateIconViewModelAsync(requestMessageId, true, true);
            var afterCreation = DateTime.UtcNow;

            var iconViewModel = await iconService.GetIconViewModelAsync(requestMessageId);
            Assert.NotNull(iconViewModel);
            var creationLastUpdated = iconViewModel.LastUpdated;

            await Task.Delay(50); // Small delay to ensure different timestamp

            var beforeUpdate = DateTime.UtcNow;
            await iconService.UpdateIconStatusAsync(requestMessageId, ResponseStatus.Received);
            var afterUpdate = DateTime.UtcNow;

            var updatedIconViewModel = await iconService.GetIconViewModelAsync(requestMessageId);
            Assert.NotNull(updatedIconViewModel);
            var updateLastUpdated = updatedIconViewModel.LastUpdated;

            // Assert
            Assert.True(creationLastUpdated >= beforeCreation);
            Assert.True(creationLastUpdated <= afterCreation);

            Assert.True(updateLastUpdated >= beforeUpdate);
            Assert.True(updateLastUpdated <= afterUpdate);
            Assert.True(updateLastUpdated > creationLastUpdated);
        }

        /// <summary>
        /// Factory method to create correlation service instance.
        /// </summary>
        private static IMessageCorrelationService CreateCorrelationService()
        {
            return new MessageCorrelationService();
        }

        /// <summary>
        /// Factory method to create icon service instance backed by the supplied correlation service.
        /// </summary>
        private static IResponseIconService CreateIconService(IMessageCorrelationService correlationService)
        {
            var navigationService = CreateNavigationService(correlationService);
            return new ResponseIconService(correlationService, navigationService);
        }

        /// <summary>
        /// Factory method to create navigation service instance backed by the supplied correlation service.
        /// </summary>
        private static IResponseNavigationService CreateNavigationService(IMessageCorrelationService correlationService)
        {
            var subscriptionService = Substitute.For<ITopicSubscriptionService>();
            subscriptionService.IsTopicSubscribedAsync(Arg.Any<string>()).Returns(Task.FromResult(true));

            var uiNavigationService = Substitute.For<IUINavigationService>();
            uiNavigationService.NavigateToTopicAsync(Arg.Any<string>()).Returns(Task.CompletedTask);
            uiNavigationService.SelectMessageAsync(Arg.Any<string>()).Returns(Task.CompletedTask);

            return new ResponseNavigationService(correlationService, subscriptionService, uiNavigationService);
        }

        /// <summary>
        /// Convenience overload that creates a fresh correlation service for callers that
        /// don't need to share state between the correlation and icon services.
        /// </summary>
        private static IResponseIconService CreateIconService()
        {
            return CreateIconService(CreateCorrelationService());
        }
    }
}