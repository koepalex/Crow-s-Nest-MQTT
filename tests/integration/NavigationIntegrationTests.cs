using System;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using CrowsNestMqtt.BusinessLogic.Contracts;

namespace CrowsNestMqtt.Integration.Tests
{
    /// <summary>
    /// Integration tests for response topic navigation functionality.
    /// These tests verify end-to-end navigation behavior and MUST FAIL before implementation.
    /// </summary>
    public class NavigationIntegrationTests
    {
        [Fact]
        public async Task EndToEndNavigation_FromRequestToResponse_ShouldWork()
        {
            // Arrange
            var correlationService = CreateCorrelationService();
            var navigationService = CreateNavigationService();

            var requestMessageId = "req-001";
            var responseMessageId = "resp-001";
            var correlationData = Encoding.UTF8.GetBytes("nav-test-001");
            var responseTopic = "test/navigation/response";

            // Register request with correlation
            await correlationService.RegisterRequestAsync(requestMessageId, correlationData, responseTopic);

            // Link response to establish received status
            await correlationService.LinkResponseAsync(responseMessageId, correlationData, responseTopic);

            // Act
            var canNavigate = await navigationService.CanNavigateToResponseAsync(requestMessageId);
            var navigationResult = await navigationService.NavigateToResponseAsync(requestMessageId);

            // Assert
            Assert.True(canNavigate, "Should be able to navigate when response is received");
            Assert.True(navigationResult.Success, "Navigation should succeed");
            Assert.Equal(responseTopic, navigationResult.SelectedTopic);
            Assert.Equal(responseMessageId, navigationResult.SelectedMessageId);
            Assert.True(navigationResult.NavigationDuration > TimeSpan.Zero);
        }

        [Fact]
        public async Task Navigation_WithPendingResponse_ShouldIndicateNotReady()
        {
            // Arrange
            var correlationService = CreateCorrelationService();
            var navigationService = CreateNavigationService();

            var requestMessageId = "req-pending";
            var correlationData = Encoding.UTF8.GetBytes("pending-test");
            var responseTopic = "test/navigation/pending";

            // Register request without linking response (pending state)
            await correlationService.RegisterRequestAsync(requestMessageId, correlationData, responseTopic);

            // Act
            var canNavigate = await navigationService.CanNavigateToResponseAsync(requestMessageId);
            var navigationResult = await navigationService.NavigateToResponseAsync(requestMessageId);

            // Assert
            Assert.False(canNavigate, "Should not be able to navigate when response is pending");
            Assert.False(navigationResult.Success, "Navigation should fail for pending response");
            Assert.Equal(NavigationError.ResponseNotReceived, navigationResult.ErrorType);
            Assert.Contains("pending", navigationResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Navigation_WithUnsubscribedTopic_ShouldIndicateNotAvailable()
        {
            // Arrange
            var correlationService = CreateCorrelationService();
            var navigationService = CreateNavigationService();

            var requestMessageId = "req-unsubscribed";
            var correlationData = Encoding.UTF8.GetBytes("unsubscribed-test");
            var responseTopic = "test/navigation/unsubscribed";

            // Register request with unsubscribed response topic
            await correlationService.RegisterRequestAsync(requestMessageId, correlationData, responseTopic);

            // Act
            var canNavigate = await navigationService.CanNavigateToResponseAsync(requestMessageId);
            var navigationResult = await navigationService.NavigateToResponseAsync(requestMessageId);

            // Assert
            Assert.False(canNavigate, "Should not be able to navigate to unsubscribed topic");
            Assert.False(navigationResult.Success, "Navigation should fail for unsubscribed topic");
            Assert.Equal(NavigationError.ResponseTopicNotSubscribed, navigationResult.ErrorType);
            Assert.Contains("subscribed", navigationResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CommandPaletteIntegration_ShouldRegisterAndExecuteCommands()
        {
            // Arrange
            var correlationService = CreateCorrelationService();
            var navigationService = CreateNavigationService();

            var requestMessageId = "req-command";
            var responseMessageId = "resp-command";
            var correlationData = Encoding.UTF8.GetBytes("command-test");
            var responseTopic = "test/navigation/command";

            // Set up complete correlation
            await correlationService.RegisterRequestAsync(requestMessageId, correlationData, responseTopic);
            await correlationService.LinkResponseAsync(responseMessageId, correlationData, responseTopic);

            // Act
            var registrationResult = await navigationService.RegisterNavigationCommandAsync(requestMessageId);
            var availableCommands = await navigationService.GetAvailableNavigationCommandsAsync();
            var commandText = await navigationService.GetNavigationCommandAsync(requestMessageId);
            var executionResult = await navigationService.ExecuteNavigationCommandAsync(commandText!);

            // Assert
            Assert.True(registrationResult, "Should successfully register navigation command");
            Assert.Contains(availableCommands, cmd => cmd.RequestMessageId == requestMessageId);
            Assert.NotNull(commandText);
            Assert.StartsWith(":", commandText);
            Assert.True(executionResult.Success, "Command execution should succeed");
            Assert.Equal(responseMessageId, executionResult.SelectedMessageId);
        }

        [Fact]
        public async Task Navigation_WithMultipleResponses_ShouldSelectFirstResponse()
        {
            // Arrange
            var correlationService = CreateCorrelationService();
            var navigationService = CreateNavigationService();

            var requestMessageId = "req-multi";
            var responseMessageId1 = "resp-multi-1";
            var responseMessageId2 = "resp-multi-2";
            var correlationData = Encoding.UTF8.GetBytes("multi-response-test");
            var responseTopic = "test/navigation/multi";

            // Set up request with multiple responses
            await correlationService.RegisterRequestAsync(requestMessageId, correlationData, responseTopic);
            await correlationService.LinkResponseAsync(responseMessageId1, correlationData, responseTopic);
            await correlationService.LinkResponseAsync(responseMessageId2, correlationData, responseTopic);

            // Act
            var responseIds = await correlationService.GetResponseMessageIdsAsync(requestMessageId);
            var navigationResult = await navigationService.NavigateToResponseAsync(requestMessageId);

            // Assert
            Assert.Equal(2, responseIds.Count);
            Assert.Contains(responseMessageId1, responseIds);
            Assert.Contains(responseMessageId2, responseIds);
            Assert.True(navigationResult.Success, "Navigation should succeed with multiple responses");
            Assert.True(
                navigationResult.SelectedMessageId == responseMessageId1 ||
                navigationResult.SelectedMessageId == responseMessageId2,
                "Should select one of the available responses");
        }

        [Fact]
        public async Task NavigationTiming_ShouldBeWithinPerformanceRequirements()
        {
            // Arrange
            var correlationService = CreateCorrelationService();
            var navigationService = CreateNavigationService();

            var requestMessageId = "req-perf";
            var responseMessageId = "resp-perf";
            var correlationData = Encoding.UTF8.GetBytes("performance-test");
            var responseTopic = "test/navigation/performance";

            await correlationService.RegisterRequestAsync(requestMessageId, correlationData, responseTopic);
            await correlationService.LinkResponseAsync(responseMessageId, correlationData, responseTopic);

            // Act
            var startTime = DateTime.UtcNow;
            var navigationResult = await navigationService.NavigateToResponseAsync(requestMessageId);
            var endTime = DateTime.UtcNow;
            var totalDuration = endTime - startTime;

            // Assert
            Assert.True(navigationResult.Success, "Navigation should succeed");
            Assert.True(totalDuration.TotalMilliseconds < 100, "Navigation should complete within 100ms per plan.md");
            Assert.True(navigationResult.NavigationDuration.TotalMilliseconds > 0, "Duration should be measured");
            Assert.True(navigationResult.NavigationDuration <= totalDuration, "Measured duration should be realistic");
        }

        [Fact]
        public async Task NavigationEvents_ShouldBeRaised()
        {
            // Arrange
            var navigationService = CreateNavigationService();
            var eventRaised = false;
            NavigationCompletedEventArgs? capturedArgs = null;

            navigationService.NavigationCompleted += (sender, args) =>
            {
                eventRaised = true;
                capturedArgs = args;
            };

            var correlationService = CreateCorrelationService();
            var requestMessageId = "req-event";
            var responseMessageId = "resp-event";
            var correlationData = Encoding.UTF8.GetBytes("event-test");
            var responseTopic = "test/navigation/event";

            await correlationService.RegisterRequestAsync(requestMessageId, correlationData, responseTopic);
            await correlationService.LinkResponseAsync(responseMessageId, correlationData, responseTopic);

            // Act
            await navigationService.NavigateToResponseAsync(requestMessageId);

            // Assert
            Assert.True(eventRaised, "NavigationCompleted event should be raised");
            Assert.NotNull(capturedArgs);
            Assert.Equal(requestMessageId, capturedArgs.RequestMessageId);
            Assert.True(capturedArgs.Result.Success);
            Assert.False(capturedArgs.WasCommandTriggered); // Direct navigation, not command-triggered
        }

        [Fact]
        public async Task Navigation_WithInvalidRequestId_ShouldHandleGracefully()
        {
            // Arrange
            var navigationService = CreateNavigationService();
            var invalidRequestMessageId = "nonexistent-request";

            // Act
            var canNavigate = await navigationService.CanNavigateToResponseAsync(invalidRequestMessageId);
            var navigationResult = await navigationService.NavigateToResponseAsync(invalidRequestMessageId);
            var commandText = await navigationService.GetNavigationCommandAsync(invalidRequestMessageId);

            // Assert
            Assert.False(canNavigate, "Should not be able to navigate to nonexistent request");
            Assert.False(navigationResult.Success, "Navigation should fail for invalid request");
            Assert.Equal(NavigationError.RequestNotFound, navigationResult.ErrorType);
            Assert.Null(commandText, "Should not generate command for invalid request");
            Assert.Contains("not found", navigationResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CustomNavigationCommand_ShouldBeSupported()
        {
            // Arrange
            var correlationService = CreateCorrelationService();
            var navigationService = CreateNavigationService();

            var requestMessageId = "req-custom";
            var responseMessageId = "resp-custom";
            var correlationData = Encoding.UTF8.GetBytes("custom-command-test");
            var responseTopic = "test/navigation/custom";
            var customCommand = ":custom-goto-response";

            await correlationService.RegisterRequestAsync(requestMessageId, correlationData, responseTopic);
            await correlationService.LinkResponseAsync(responseMessageId, correlationData, responseTopic);

            // Act
            var registrationResult = await navigationService.RegisterNavigationCommandAsync(requestMessageId, customCommand);
            var retrievedCommand = await navigationService.GetNavigationCommandAsync(requestMessageId);
            var executionResult = await navigationService.ExecuteNavigationCommandAsync(customCommand);

            // Assert
            Assert.True(registrationResult, "Should accept custom command registration");
            Assert.Equal(customCommand, retrievedCommand);
            Assert.True(executionResult.Success, "Custom command execution should succeed");
            Assert.Equal(responseMessageId, executionResult.SelectedMessageId);
        }

        /// <summary>
        /// Factory method to create correlation service instance.
        /// This will fail until the actual implementation is created.
        /// </summary>
        private static IMessageCorrelationService CreateCorrelationService()
        {
            throw new NotImplementedException("MessageCorrelationService not yet implemented - this test should fail");
        }

        /// <summary>
        /// Factory method to create navigation service instance.
        /// This will fail until the actual implementation is created.
        /// </summary>
        private static IResponseNavigationService CreateNavigationService()
        {
            throw new NotImplementedException("ResponseNavigationService not yet implemented - this test should fail");
        }
    }
}